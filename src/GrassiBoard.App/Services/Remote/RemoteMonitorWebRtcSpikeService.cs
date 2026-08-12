#if REMOTE_MONITOR_SPIKE
using System.Text.Json;
using Concentus;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace GrassiBoard.Services.Remote;

/// <summary>
/// v1.2 technology spike: validates synthetic tone, Windows loopback, the
/// ABI-9 native Soundboard/processed-Voice source taps, and the independent
/// Remote Monitor mix including direct Media with duplicate prevention over
/// WebRTC/Opus. My Voice is explicit opt-in and monitor-only gains never
/// modify Program gain/master/VB-CABLE routing.
/// </summary>
internal sealed class RemoteMonitorWebRtcSpikeService : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, SpikeSession> _sessions = [];
    private readonly NativeAudioEngine _nativeEngine;
    private readonly MediaDeckService _mediaDeck;

    public RemoteMonitorWebRtcSpikeService(NativeAudioEngine nativeEngine, MediaDeckService mediaDeck)
    {
        _nativeEngine = nativeEngine;
        _mediaDeck = mediaDeck;
    }

    public async Task<RemoteCommandResult> HandleOfferAsync(
        Guid clientId,
        JsonElement payload,
        Func<string, object, Task> sendEventAsync)
    {
        if (!TryGetString(payload, "sdp", out string sdp))
            return RemoteCommandResult.Fail("monitor_spike_invalid_offer", "The WebRTC offer is missing its SDP payload.");

        string requestedSource = TryGetOptionalString(payload, "source") ?? "monitor-mix";
        if (requestedSource is not ("monitor-mix" or "windows-loopback" or "synthetic-sine" or "soundboard-tap"))
            return RemoteCommandResult.Fail("monitor_spike_invalid_source", "The requested Remote Monitor source is not supported by this spike build.");

        await CloseAsync(clientId, "Replacing previous monitor spike session");

        if (SourceUsesNativeSoundboardTap(requestedSource))
        {
            lock (_gate)
            {
                if (_sessions.Values.Any(existing => SourceUsesNativeSoundboardTap(existing.Source)))
                    return RemoteCommandResult.Fail(
                        "monitor_spike_tap_in_use",
                        "The ABI-9 Soundboard tap currently supports one Remote Monitor listener at a time.");
            }
        }

        SpikeSession session;
        try
        {
            session = new SpikeSession(sendEventAsync, requestedSource, _nativeEngine, _mediaDeck);
        }
        catch (Exception exception)
        {
            return RemoteCommandResult.Fail("monitor_spike_create_failed", $"Could not create the WebRTC test session: {exception.Message}");
        }

        if (requestedSource == "monitor-mix")
        {
            RemoteCommandResult settingsResult = session.ApplyMixSettings(payload);
            if (!settingsResult.Success)
            {
                await session.CloseAsync("Invalid initial monitor mix settings");
                return settingsResult;
            }
        }

        lock (_gate) _sessions[clientId] = session;

        try
        {
            RemoteCommandResult result = await session.AcceptOfferAsync(sdp);
            if (!result.Success)
            {
                await CloseAsync(clientId, "Offer negotiation failed");
                return result;
            }
            return RemoteCommandResult.Ok();
        }
        catch (Exception exception)
        {
            await CloseAsync(clientId, "Offer exception");
            return RemoteCommandResult.Fail("monitor_spike_offer_failed", $"WebRTC offer negotiation failed: {exception.Message}");
        }
    }

    public Task<RemoteCommandResult> HandleIceCandidateAsync(Guid clientId, JsonElement payload)
    {
        SpikeSession? session;
        lock (_gate) _sessions.TryGetValue(clientId, out session);
        // Current Android spike clients bundle their host ICE candidates into the
        // SDP offer. Ignore stale/pre-session trickle candidates from an older
        // cached client so they cannot mask the real offer-negotiation result.
        if (session is null)
            return Task.FromResult(RemoteCommandResult.Ok());

        if (!TryGetString(payload, "candidate", out string candidate))
            return Task.FromResult(RemoteCommandResult.Ok()); // End-of-candidates is harmless for this LAN spike.

        string? sdpMid = TryGetOptionalString(payload, "sdpMid");
        ushort sdpMLineIndex = 0;
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("sdpMLineIndex", out JsonElement indexElement) &&
            indexElement.ValueKind == JsonValueKind.Number &&
            indexElement.TryGetInt32(out int parsedIndex) && parsedIndex is >= 0 and <= ushort.MaxValue)
        {
            sdpMLineIndex = (ushort)parsedIndex;
        }

        string? usernameFragment = TryGetOptionalString(payload, "usernameFragment");
        return Task.FromResult(session.AddIceCandidate(candidate, sdpMid, sdpMLineIndex, usernameFragment));
    }

    public async Task RebindClientAsync(Guid clientId, Func<string, object, Task> sendEventAsync)
    {
        SpikeSession? session;
        lock (_gate) _sessions.TryGetValue(clientId, out session);
        if (session is null) return;

        session.RebindEventSink(sendEventAsync);
        await session.PublishCurrentStateAsync("Remote control reconnected; the existing monitor media session was preserved.");
    }

    public async Task<RemoteCommandResult> StopAsync(Guid clientId)
    {
        await CloseAsync(clientId, "Stopped by GrassiMote");
        return RemoteCommandResult.Ok();
    }

    public async Task<RemoteCommandResult> HandleMixSettingsAsync(Guid clientId, JsonElement payload)
    {
        SpikeSession? session;
        lock (_gate) _sessions.TryGetValue(clientId, out session);
        if (session is null)
            return RemoteCommandResult.Fail("monitor_spike_no_session", "Start Remote Monitor before changing monitor mix levels.");

        RemoteCommandResult result = session.ApplyMixSettings(payload);
        if (result.Success)
            await session.PublishCurrentStateAsync(null);
        return result;
    }

    public async Task CloseAsync(Guid clientId, string reason)
    {
        SpikeSession? session = null;
        lock (_gate)
        {
            if (_sessions.Remove(clientId, out SpikeSession? removed)) session = removed;
        }
        if (session is not null) await session.CloseAsync(reason);
    }

    public async Task CloseAllAsync(string reason)
    {
        SpikeSession[] sessions;
        lock (_gate)
        {
            sessions = _sessions.Values.ToArray();
            _sessions.Clear();
        }
        foreach (SpikeSession session in sessions) await session.CloseAsync(reason);
    }

    public async ValueTask DisposeAsync() => await CloseAllAsync("Remote Monitor spike disposed");

    private static bool SourceUsesNativeSoundboardTap(string source) =>
        source is "soundboard-tap" or "monitor-mix";

    private static bool TryGetString(JsonElement payload, string name, out string value)
    {
        value = string.Empty;
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(name, out JsonElement element) ||
            element.ValueKind != JsonValueKind.String)
            return false;
        value = element.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string? TryGetOptionalString(JsonElement payload, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(name, out JsonElement element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    private sealed class SpikeSession
    {
        private const int FrameMilliseconds = 20;
        private const int LoopbackOpusBitrate = 128000;
        private const int NativeTapOpusBitrate = 128000;
        private const int MonitorMixOpusBitrate = 128000;
        private const int NativeTapSampleRate = 48000;
        private const int NativeTapChannels = 2;
        private const int MaxOpusPacketBytes = 1275;
        private const int MixLoopbackPrebufferFrames = 2;
        private const int MixLoopbackHighWaterFrames = 4;
        private const int MixMediaPrebufferFrames = 2;
        private const float MixLimiterCeiling = 0.98F;
        private const float MixLimiterReleasePerFrame = 0.08F;

        private readonly RTCPeerConnection _peer;
        private readonly AudioEncoder _encoder;
        private readonly AudioExtrasSource? _toneSource;
        private readonly NativeAudioEngine _nativeEngine;
        private readonly MediaDeckService _mediaDeck;
        private readonly object _eventSinkGate = new();
        private Func<string, object, Task> _sendEventAsync;
        private readonly string _source;

        private readonly object _loopbackGate = new();
        private float _mixWindowsGain = 0.90F;
        private float _mixSoundboardGain = 0.70F;
        private float _mixMediaGain = 0.70F;
        private float _mixVoiceGain = 0.10F;
        private float _mixMasterGain = 0.85F;
        private int _mixVoiceEnabled;
        private int _mediaDuplicateSuppressed;

        private MMDeviceEnumerator? _deviceEnumerator;
        private MMDevice? _loopbackDevice;
        private WasapiLoopbackCapture? _loopbackCapture;
        private BufferedWaveProvider? _loopbackBuffer;
        private IOpusEncoder? _loopbackOpusEncoder;
        private IOpusEncoder? _nativeTapOpusEncoder;
        private CancellationTokenSource? _nativeTapCts;
        private Task? _nativeTapTask;
        private CancellationTokenSource? _deviceWatchCts;
        private Task? _deviceWatchTask;
        private AudioFormat? _negotiatedFormat;
        private string? _deviceName;
        private string? _deviceId;
        private int _captureSampleRate;
        private int _captureChannels;
        private int _audioStarted;
        private int _closed;

        public SpikeSession(Func<string, object, Task> sendEventAsync, string source, NativeAudioEngine nativeEngine, MediaDeckService mediaDeck)
        {
            _sendEventAsync = sendEventAsync;
            _source = source;
            _nativeEngine = nativeEngine;
            _mediaDeck = mediaDeck;
            _encoder = new AudioEncoder(includeOpus: true);

            List<AudioFormat> formats;
            if (_source == "synthetic-sine")
            {
                _toneSource = new AudioExtrasSource(_encoder, new AudioSourceOptions
                {
                    AudioSource = AudioSourcesEnum.SineWave
                })
                {
                    AudioSamplePeriodMilliseconds = FrameMilliseconds
                };
                _toneSource.RestrictFormats(format => format.Codec == AudioCodecsEnum.OPUS);
                formats = _toneSource.GetAudioSourceFormats();
            }
            else
            {
                formats = _encoder.SupportedFormats
                    .Where(format => format.Codec == AudioCodecsEnum.OPUS)
                    .ToList();
            }

            if (formats.Count == 0)
                throw new InvalidOperationException("The selected WebRTC package did not expose an Opus encoder.");

            // No STUN/TURN configuration: this gate tests host ICE on the same LAN only.
            _peer = new RTCPeerConnection();
            _peer.addTrack(new MediaStreamTrack(formats, MediaStreamStatusEnum.SendOnly));
            if (_toneSource is not null) _toneSource.OnAudioSourceEncodedSample += _peer.SendAudio;
            _peer.OnAudioFormatsNegotiated += OnAudioFormatsNegotiated;
            _peer.onicecandidate += OnIceCandidate;
            _peer.onconnectionstatechange += OnConnectionStateChanged;
            _peer.oniceconnectionstatechange += state => FireAndForgetStateAsync("ice", state.ToString());
        }

        public string Source => _source;

        public async Task<RemoteCommandResult> AcceptOfferAsync(string sdp)
        {
            await SendStateAsync("negotiating", "Applying browser offer");
            var offer = new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = sdp };
            SetDescriptionResultEnum setResult = _peer.setRemoteDescription(offer);
            if (setResult != SetDescriptionResultEnum.OK)
                return RemoteCommandResult.Fail("monitor_spike_sdp_rejected", $"The browser SDP was rejected: {setResult}.");

            RTCSessionDescriptionInit answer = _peer.createAnswer();
            await _peer.setLocalDescription(answer);
            await _sendEventAsync("monitor.spike.answer", new { type = "answer", sdp = answer.sdp });
            await SendStateAsync("negotiating", "Answer sent; waiting for ICE/DTLS");
            return RemoteCommandResult.Ok();
        }

        public RemoteCommandResult AddIceCandidate(string candidate, string? sdpMid, ushort sdpMLineIndex, string? usernameFragment)
        {
            if (Volatile.Read(ref _closed) != 0) return RemoteCommandResult.Fail("monitor_spike_closed", "The Remote Monitor test session is already closed.");
            try
            {
                _peer.addIceCandidate(new RTCIceCandidateInit
                {
                    candidate = candidate,
                    sdpMid = sdpMid ?? string.Empty,
                    sdpMLineIndex = sdpMLineIndex,
                    usernameFragment = usernameFragment ?? string.Empty
                });
                return RemoteCommandResult.Ok();
            }
            catch (Exception exception)
            {
                return RemoteCommandResult.Fail("monitor_spike_ice_failed", $"Could not add the browser ICE candidate: {exception.Message}");
            }
        }

        private void OnAudioFormatsNegotiated(List<AudioFormat> formats)
        {
            if (formats.Count == 0) return;
            _negotiatedFormat = formats[0];
            _toneSource?.SetAudioSourceFormat(formats[0]);
        }

        private void OnIceCandidate(RTCIceCandidate candidate)
        {
            if (candidate is null || string.IsNullOrWhiteSpace(candidate.candidate)) return;
            _ = SendSafelyAsync("monitor.spike.ice", new
            {
                candidate = candidate.candidate,
                sdpMid = candidate.sdpMid,
                sdpMLineIndex = candidate.sdpMLineIndex,
                usernameFragment = candidate.usernameFragment
            });
        }

        private void OnConnectionStateChanged(RTCPeerConnectionState state)
        {
            _ = HandleConnectionStateAsync(state);
        }

        private async Task HandleConnectionStateAsync(RTCPeerConnectionState state)
        {
            string? connectedDetail = _source switch
            {
                "monitor-mix" => "Remote Monitor Mix is streaming Windows output + ABI-9 Soundboard + direct Media. My Voice is available as an explicit processed-mic opt-in.",
                "windows-loopback" => "Windows output capture is streaming. Play audio on the selected Windows output device.",
                "soundboard-tap" => "ABI-9 Soundboard tap is streaming. Trigger a Pad while the GrassiBoard engine is running.",
                _ => "Synthetic Opus tone is streaming"
            };

            await SendStateAsync(state.ToString(), state == RTCPeerConnectionState.connected ? connectedDetail : null);
            if (state == RTCPeerConnectionState.connected)
            {
                if (Interlocked.Exchange(ref _audioStarted, 1) == 0)
                {
                    try
                    {
                        if (_source == "monitor-mix") StartMonitorMix();
                        else if (_source == "windows-loopback") StartLoopbackCapture();
                        else if (_source == "soundboard-tap") StartNativeSoundboardTap();
                        else if (_toneSource is not null) await _toneSource.StartAudio();
                    }
                    catch (Exception exception)
                    {
                        await SendStateAsync("failed", $"Remote Monitor source failed: {exception.Message}");
                    }
                }
            }
            else if (state is RTCPeerConnectionState.failed or RTCPeerConnectionState.closed)
            {
                await CloseAsync($"Peer state: {state}");
            }
        }

        public RemoteCommandResult ApplyMixSettings(JsonElement payload)
        {
            if (_source != "monitor-mix")
                return RemoteCommandResult.Fail("monitor_spike_not_mix", "Monitor mix levels are only available while the Monitor Mix source is active.");

            float windowsGain = Volatile.Read(ref _mixWindowsGain);
            float soundboardGain = Volatile.Read(ref _mixSoundboardGain);
            float mediaGain = Volatile.Read(ref _mixMediaGain);
            float voiceGain = Volatile.Read(ref _mixVoiceGain);
            float masterGain = Volatile.Read(ref _mixMasterGain);
            bool voiceEnabled = Volatile.Read(ref _mixVoiceEnabled) != 0;

            if (!TryApplyGain(payload, "windowsGain", ref windowsGain, out string? error) ||
                !TryApplyGain(payload, "soundboardGain", ref soundboardGain, out error) ||
                !TryApplyGain(payload, "mediaGain", ref mediaGain, out error) ||
                !TryApplyGain(payload, "voiceGain", ref voiceGain, out error) ||
                !TryApplyGain(payload, "masterGain", ref masterGain, out error) ||
                !TryApplyBoolean(payload, "voiceEnabled", ref voiceEnabled, out error))
            {
                return RemoteCommandResult.Fail("monitor_spike_invalid_mix_gain", error ?? "Remote Monitor gains must be 0.0..1.0 and voiceEnabled must be boolean.");
            }

            Volatile.Write(ref _mixWindowsGain, windowsGain);
            Volatile.Write(ref _mixSoundboardGain, soundboardGain);
            Volatile.Write(ref _mixMediaGain, mediaGain);
            Volatile.Write(ref _mixVoiceGain, voiceGain);
            Volatile.Write(ref _mixMasterGain, masterGain);
            Volatile.Write(ref _mixVoiceEnabled, voiceEnabled ? 1 : 0);
            return RemoteCommandResult.Ok();
        }

        private static bool TryApplyGain(JsonElement payload, string propertyName, ref float target, out string? error)
        {
            error = null;
            if (payload.ValueKind != JsonValueKind.Object ||
                !payload.TryGetProperty(propertyName, out JsonElement element))
                return true;

            if (element.ValueKind != JsonValueKind.Number ||
                !element.TryGetDouble(out double value) ||
                !double.IsFinite(value) ||
                value < 0.0 ||
                value > 1.0)
            {
                error = $"{propertyName} must be a finite number from 0.0 to 1.0.";
                return false;
            }

            target = (float)value;
            return true;
        }

        private static bool TryApplyBoolean(JsonElement payload, string propertyName, ref bool target, out string? error)
        {
            error = null;
            if (payload.ValueKind != JsonValueKind.Object ||
                !payload.TryGetProperty(propertyName, out JsonElement element))
                return true;

            if (element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                error = $"{propertyName} must be a boolean.";
                return false;
            }

            target = element.GetBoolean();
            return true;
        }

        private void StartMonitorMix()
        {
            if (_negotiatedFormat is null)
                throw new InvalidOperationException("Opus was not negotiated before Remote Monitor Mix started.");
            if (!_nativeEngine.IsAvailable || _nativeEngine.ApiVersion != NativeAudioEngine.ExpectedApiVersion)
                throw new InvalidOperationException("The ABI-9 native audio engine is not available for Remote Monitor Mix.");

            AudioFormat negotiatedFormat = _negotiatedFormat.Value;
            int channels = negotiatedFormat.ChannelCount is 1 or 2 ? negotiatedFormat.ChannelCount : NativeTapChannels;
            if (negotiatedFormat.ClockRate != NativeTapSampleRate)
                throw new InvalidOperationException($"Remote Monitor Mix requires 48 kHz Opus, but {negotiatedFormat.ClockRate} Hz was negotiated.");

            IOpusEncoder encoder = CreateTunedOpusEncoder(NativeTapSampleRate, channels, MonitorMixOpusBitrate);
            NativeResult enableResult = _nativeEngine.SetMonitorTapEnabled(true);
            if (enableResult != NativeResult.Ok)
            {
                encoder.Dispose();
                throw new InvalidOperationException($"Could not enable the native Soundboard source for Remote Monitor Mix: {enableResult}.");
            }
            NativeResult enableVoiceResult = _nativeEngine.SetVoiceMonitorTapEnabled(true);
            if (enableVoiceResult != NativeResult.Ok)
            {
                try { _nativeEngine.SetMonitorTapEnabled(false); } catch (Exception) { }
                encoder.Dispose();
                throw new InvalidOperationException($"Could not enable the processed My Voice source for Remote Monitor Mix: {enableVoiceResult}.");
            }

            try
            {
                _nativeEngine.ClearMonitorTap();
                _nativeEngine.ClearVoiceMonitorTap();
                _mediaDeck.SetRemoteMonitorTapEnabled(true);
                _mediaDeck.ClearRemoteMonitorTap();
                _nativeTapOpusEncoder = encoder;

                // Loopback callback only buffers PCM in monitor-mix mode. A single
                // paced worker below combines it with the native Soundboard ring
                // and performs the only Opus encode/send for this source.
                StartLoopbackCapture();

                var cts = new CancellationTokenSource();
                _nativeTapCts = cts;
                _nativeTapTask = Task.Run(() => PumpMonitorMixAsync(negotiatedFormat, channels, encoder, cts.Token));
                FireAndForgetStateAsync("connected", "Remote Monitor Mix is live. Windows / Space, Soundboard, and Media are active; processed My Voice remains OFF until you explicitly enable it.");
            }
            catch
            {
                try { _nativeEngine.SetMonitorTapEnabled(false); } catch (Exception) { }
                try { _nativeEngine.ClearMonitorTap(); } catch (Exception) { }
                try { _nativeEngine.SetVoiceMonitorTapEnabled(false); } catch (Exception) { }
                try { _nativeEngine.ClearVoiceMonitorTap(); } catch (Exception) { }
                try { _mediaDeck.SetRemoteMonitorTapEnabled(false); } catch (Exception) { }
                _nativeTapOpusEncoder = null;
                encoder.Dispose();
                throw;
            }
        }

        private async Task PumpMonitorMixAsync(
            AudioFormat negotiatedFormat,
            int outputChannels,
            IOpusEncoder encoder,
            CancellationToken cancellationToken)
        {
            int frameSamplesPerChannel = NativeTapSampleRate * FrameMilliseconds / 1000;
            int frameBytes = frameSamplesPerChannel * outputChannels * sizeof(short);
            float[] soundboard = new float[frameSamplesPerChannel * NativeTapChannels];
            float[] voice = new float[frameSamplesPerChannel * NativeTapChannels];
            float[] media = new float[frameSamplesPerChannel * NativeTapChannels];
            byte[] windowsPcm = new byte[frameBytes];
            byte[] discardPcm = new byte[frameBytes];
            float[] mixedFloat = new float[frameSamplesPerChannel * outputChannels];
            short[] mixedPcm = new short[frameSamplesPerChannel * outputChannels];
            byte[] packet = new byte[MaxOpusPacketBytes];
            uint rtpUnits = (uint)(negotiatedFormat.RtpClockRate / 1000 * FrameMilliseconds);

            uint pendingSoundboardFrames = 0U;
            uint pendingVoiceFrames = 0U;
            bool windowsPrimed = false;
            bool mediaPrimed = false;
            bool? previousMediaDuplicateSuppressed = null;
            float limiterGain = 1.0F;

            try
            {
                // The accepted isolated Soundboard tap is tied to the native audio
                // callback clock and already proved artifact-free on the real device.
                // Use complete native frames as the mix cadence instead of a managed
                // PeriodicTimer. This avoids cutting either source at arbitrary timer
                // boundaries and prevents partial source frames from being padded with
                // zeros every time WASAPI/native delivery arrives in smaller bursts.
                while (!cancellationToken.IsCancellationRequested &&
                       Volatile.Read(ref _closed) == 0)
                {
                    if (pendingSoundboardFrames < frameSamplesPerChannel)
                    {
                        NativeResult readResult = _nativeEngine.ReadMonitorTap(
                            soundboard,
                            pendingSoundboardFrames,
                            (uint)frameSamplesPerChannel - pendingSoundboardFrames,
                            out uint readFrames);
                        if (readResult != NativeResult.Ok)
                            throw new InvalidOperationException($"Remote Monitor Mix Soundboard read failed: {readResult}.");
                        pendingSoundboardFrames += readFrames;
                    }

                    if (pendingVoiceFrames < frameSamplesPerChannel)
                    {
                        NativeResult voiceReadResult = _nativeEngine.ReadVoiceMonitorTap(
                            voice,
                            pendingVoiceFrames,
                            (uint)frameSamplesPerChannel - pendingVoiceFrames,
                            out uint voiceReadFrames);
                        if (voiceReadResult != NativeResult.Ok)
                            throw new InvalidOperationException($"Remote Monitor Mix My Voice read failed: {voiceReadResult}.");
                        pendingVoiceFrames += voiceReadFrames;
                    }

                    if (pendingSoundboardFrames < frameSamplesPerChannel ||
                        pendingVoiceFrames < frameSamplesPerChannel)
                    {
                        await Task.Delay(1, cancellationToken);
                        continue;
                    }

                    Array.Clear(windowsPcm);
                    bool windowsFrameReady = false;

                    lock (_loopbackGate)
                    {
                        BufferedWaveProvider? buffer = _loopbackBuffer;
                        if (buffer is not null)
                        {
                            // WASAPI callback sizes are bursty and are not guaranteed
                            // to line up with our 20 ms Opus frame. Never consume a
                            // partial Windows frame. Prime a small 40 ms reservoir,
                            // then consume exactly one complete frame per native mix
                            // frame. This trades a tiny fixed monitor-only latency for
                            // continuity instead of repeated micro-gaps/crackle.
                            while (buffer.BufferedBytes > frameBytes * MixLoopbackHighWaterFrames)
                            {
                                int discard = Math.Min(frameBytes, buffer.BufferedBytes - frameBytes * MixLoopbackHighWaterFrames);
                                if (discard < frameBytes ||
                                    buffer.Read(discardPcm, 0, frameBytes) != frameBytes)
                                {
                                    break;
                                }
                            }

                            if (!windowsPrimed &&
                                buffer.BufferedBytes >= frameBytes * MixLoopbackPrebufferFrames)
                            {
                                windowsPrimed = true;
                            }

                            if (windowsPrimed)
                            {
                                if (buffer.BufferedBytes >= frameBytes)
                                {
                                    windowsFrameReady = buffer.Read(windowsPcm, 0, frameBytes) == frameBytes;
                                    if (!windowsFrameReady) windowsPrimed = false;
                                }
                                else
                                {
                                    // Re-prime after a real underflow rather than
                                    // draining whatever partial fragment is present.
                                    // The fragment stays queued and is joined with the
                                    // next WASAPI callback.
                                    windowsPrimed = false;
                                }
                            }
                        }
                    }

                    Array.Clear(media);
                    bool mediaFrameReady = false;
                    bool mediaDuplicateSuppressed = IsMediaDuplicateSuppressedByWindowsLoopback();
                    Volatile.Write(ref _mediaDuplicateSuppressed, mediaDuplicateSuppressed ? 1 : 0);

                    if (mediaDuplicateSuppressed)
                    {
                        // Local Media monitoring already reaches the exact Windows
                        // endpoint captured by WASAPI loopback. Suppress the direct
                        // tap to avoid doubled/comb-filtered Media, and clear its
                        // backlog so routing changes cannot replay stale audio.
                        _mediaDeck.ClearRemoteMonitorTap();
                        mediaPrimed = false;
                    }
                    else
                    {
                        if (!mediaPrimed && _mediaDeck.RemoteMonitorTapBufferedFrames >= frameSamplesPerChannel * MixMediaPrebufferFrames)
                            mediaPrimed = true;
                        if (mediaPrimed)
                        {
                            mediaFrameReady = _mediaDeck.TryReadRemoteMonitorTap(media, frameSamplesPerChannel);
                            if (!mediaFrameReady) mediaPrimed = false;
                        }
                    }

                    if (previousMediaDuplicateSuppressed != mediaDuplicateSuppressed)
                    {
                        previousMediaDuplicateSuppressed = mediaDuplicateSuppressed;
                        FireAndForgetStateAsync(
                            "connected",
                            mediaDuplicateSuppressed
                                ? "Media duplicate prevention is active: Media is already present through Windows output, so the direct Media tap is suppressed."
                                : "Direct Media contribution is active when Media plays; its monitor-only level is independent from Windows / Space and Program/VB-CABLE.");
                    }

                    float windowsGain = Volatile.Read(ref _mixWindowsGain);
                    float soundboardGain = Volatile.Read(ref _mixSoundboardGain);
                    float mediaGain = Volatile.Read(ref _mixMediaGain);
                    float voiceGain = Volatile.Read(ref _mixVoiceGain);
                    bool voiceEnabled = Volatile.Read(ref _mixVoiceEnabled) != 0;
                    float masterGain = Volatile.Read(ref _mixMasterGain);
                    float peak = 0.0F;

                    for (int frame = 0; frame < frameSamplesPerChannel; ++frame)
                    {
                        float boardLeft = soundboard[frame * 2];
                        float boardRight = soundboard[frame * 2 + 1];

                        if (outputChannels == 1)
                        {
                            int byteOffset = frame * sizeof(short);
                            float windowsSample = windowsFrameReady
                                ? ReadPcm16(windowsPcm, byteOffset) / 32768.0F
                                : 0.0F;
                            float boardSample = (boardLeft + boardRight) * 0.5F;
                            float mediaSample = mediaFrameReady ? (media[frame * 2] + media[frame * 2 + 1]) * 0.5F : 0.0F;
                            float voiceSample = voiceEnabled ? (voice[frame * 2] + voice[frame * 2 + 1]) * 0.5F : 0.0F;
                            float mixed = (windowsSample * windowsGain + boardSample * soundboardGain + mediaSample * mediaGain + voiceSample * voiceGain) * masterGain;
                            mixedFloat[frame] = mixed;
                            peak = Math.Max(peak, Math.Abs(mixed));
                        }
                        else
                        {
                            int byteOffset = frame * 2 * sizeof(short);
                            float windowsLeft = windowsFrameReady
                                ? ReadPcm16(windowsPcm, byteOffset) / 32768.0F
                                : 0.0F;
                            float windowsRight = windowsFrameReady
                                ? ReadPcm16(windowsPcm, byteOffset + sizeof(short)) / 32768.0F
                                : 0.0F;
                            float mediaLeft = mediaFrameReady ? media[frame * 2] : 0.0F;
                            float mediaRight = mediaFrameReady ? media[frame * 2 + 1] : 0.0F;
                            float voiceLeft = voiceEnabled ? voice[frame * 2] : 0.0F;
                            float voiceRight = voiceEnabled ? voice[frame * 2 + 1] : 0.0F;
                            float mixedLeft = (windowsLeft * windowsGain + boardLeft * soundboardGain + mediaLeft * mediaGain + voiceLeft * voiceGain) * masterGain;
                            float mixedRight = (windowsRight * windowsGain + boardRight * soundboardGain + mediaRight * mediaGain + voiceRight * voiceGain) * masterGain;
                            mixedFloat[frame * 2] = mixedLeft;
                            mixedFloat[frame * 2 + 1] = mixedRight;
                            peak = Math.Max(peak, Math.Max(Math.Abs(mixedLeft), Math.Abs(mixedRight)));
                        }
                    }

                    // Windows + Soundboard + Media + optional My Voice can legitimately sum above 0 dBFS even
                    // when each source is individually clean. Hard-clipping the sum
                    // creates the exact "scratchy/crackly" texture this gate is meant
                    // to detect. Apply a monitor-only transparent peak limiter:
                    // immediate attack, slow release, no Program/VB-CABLE effect.
                    float desiredLimiterGain = peak > MixLimiterCeiling
                        ? MixLimiterCeiling / peak
                        : 1.0F;
                    limiterGain = desiredLimiterGain < limiterGain
                        ? desiredLimiterGain
                        : Math.Min(1.0F, limiterGain + (1.0F - limiterGain) * MixLimiterReleasePerFrame);

                    for (int sample = 0; sample < mixedFloat.Length; ++sample)
                    {
                        mixedPcm[sample] = FloatToPcm16(mixedFloat[sample] * limiterGain);
                    }

                    int encodedLength = encoder.Encode(mixedPcm, frameSamplesPerChannel, packet, packet.Length);
                    if (encodedLength > 0)
                    {
                        byte[] encoded = packet.AsSpan(0, encodedLength).ToArray();
                        _peer.SendAudio(rtpUnits, encoded);
                    }

                    pendingSoundboardFrames = 0U;
                    pendingVoiceFrames = 0U;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                await SendStateAsync("failed", $"Remote Monitor Mix failed: {exception.Message}");
            }
        }

        private bool IsMediaDuplicateSuppressedByWindowsLoopback()
        {
            if (!_mediaDeck.MonitorEnabled) return false;
            string mediaMonitorDeviceId = _mediaDeck.MonitorDeviceId;
            if (string.IsNullOrWhiteSpace(mediaMonitorDeviceId)) return false;
            string? loopbackDeviceId;
            lock (_loopbackGate) loopbackDeviceId = _deviceId;
            return !string.IsNullOrWhiteSpace(loopbackDeviceId) &&
                string.Equals(loopbackDeviceId, mediaMonitorDeviceId, StringComparison.OrdinalIgnoreCase);
        }

        private static short ReadPcm16(byte[] buffer, int offset)
        {
            if (offset < 0 || offset + 1 >= buffer.Length) return 0;
            return (short)(buffer[offset] | (buffer[offset + 1] << 8));
        }

        private void StartNativeSoundboardTap()
        {
            if (_negotiatedFormat is null)
                throw new InvalidOperationException("Opus was not negotiated before the native Soundboard tap started.");
            if (!_nativeEngine.IsAvailable || _nativeEngine.ApiVersion != NativeAudioEngine.ExpectedApiVersion)
                throw new InvalidOperationException("The ABI-9 native audio engine is not available in this spike build.");

            AudioFormat negotiatedFormat = _negotiatedFormat.Value;
            int channels = negotiatedFormat.ChannelCount is 1 or 2 ? negotiatedFormat.ChannelCount : NativeTapChannels;
            if (negotiatedFormat.ClockRate != NativeTapSampleRate)
                throw new InvalidOperationException($"The Soundboard tap requires 48 kHz Opus, but {negotiatedFormat.ClockRate} Hz was negotiated.");

            IOpusEncoder encoder = CreateTunedOpusEncoder(NativeTapSampleRate, channels, NativeTapOpusBitrate);
            NativeResult enableResult = _nativeEngine.SetMonitorTapEnabled(true);
            if (enableResult != NativeResult.Ok)
            {
                encoder.Dispose();
                throw new InvalidOperationException($"Could not enable the native Soundboard monitor tap: {enableResult}.");
            }
            _nativeEngine.ClearMonitorTap();

            _nativeTapOpusEncoder = encoder;
            _captureSampleRate = NativeTapSampleRate;
            _captureChannels = NativeTapChannels;
            _deviceName = "GrassiBoard ABI-9 Soundboard tap";

            var cts = new CancellationTokenSource();
            _nativeTapCts = cts;
            _nativeTapTask = Task.Run(() => PumpNativeSoundboardTapAsync(negotiatedFormat, channels, encoder, cts.Token));
        }

        private async Task PumpNativeSoundboardTapAsync(
            AudioFormat negotiatedFormat,
            int outputChannels,
            IOpusEncoder encoder,
            CancellationToken cancellationToken)
        {
            int frameSamplesPerChannel = NativeTapSampleRate * FrameMilliseconds / 1000;
            float[] source = new float[frameSamplesPerChannel * NativeTapChannels];
            short[] pcm = new short[frameSamplesPerChannel * outputChannels];
            byte[] packet = new byte[MaxOpusPacketBytes];
            uint rtpUnits = (uint)(negotiatedFormat.RtpClockRate / 1000 * FrameMilliseconds);
            uint pendingFrames = 0U;

            try
            {
                while (!cancellationToken.IsCancellationRequested && Volatile.Read(ref _closed) == 0)
                {
                    NativeResult readResult = _nativeEngine.ReadMonitorTap(
                        source,
                        pendingFrames,
                        (uint)frameSamplesPerChannel - pendingFrames,
                        out uint readFrames);
                    if (readResult != NativeResult.Ok)
                        throw new InvalidOperationException($"Native Soundboard tap read failed: {readResult}.");

                    pendingFrames += readFrames;
                    if (pendingFrames < frameSamplesPerChannel)
                    {
                        await Task.Delay(2, cancellationToken);
                        continue;
                    }

                    for (int frame = 0; frame < frameSamplesPerChannel; ++frame)
                    {
                        float left = source[frame * 2];
                        float right = source[frame * 2 + 1];
                        if (outputChannels == 1)
                        {
                            pcm[frame] = FloatToPcm16((left + right) * 0.5F);
                        }
                        else
                        {
                            pcm[frame * 2] = FloatToPcm16(left);
                            pcm[frame * 2 + 1] = FloatToPcm16(right);
                        }
                    }

                    int encodedLength = encoder.Encode(pcm, frameSamplesPerChannel, packet, packet.Length);
                    if (encodedLength > 0)
                    {
                        byte[] encoded = packet.AsSpan(0, encodedLength).ToArray();
                        _peer.SendAudio(rtpUnits, encoded);
                    }
                    pendingFrames = 0U;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                await SendStateAsync("failed", $"Native Soundboard tap failed: {exception.Message}");
            }
        }

        private static short FloatToPcm16(float sample)
        {
            float safe = Math.Clamp(float.IsFinite(sample) ? sample : 0.0F, -1.0F, 1.0F);
            return safe >= 0.0F
                ? (short)Math.Min(short.MaxValue, Math.Round(safe * short.MaxValue))
                : (short)Math.Max(short.MinValue, Math.Round(safe * -short.MinValue));
        }

        private void StartLoopbackCapture()
        {
            if (_negotiatedFormat is null)
                throw new InvalidOperationException("Opus was not negotiated before Windows loopback capture started.");

            RestartLoopbackCaptureForCurrentDefault();
            StartDefaultDeviceWatcher();
        }

        private void RestartLoopbackCaptureForCurrentDefault()
        {
            if (_negotiatedFormat is null)
                throw new InvalidOperationException("Opus was not negotiated before Windows loopback capture started.");

            AudioFormat negotiatedFormat = _negotiatedFormat.Value;
            int sampleRate = negotiatedFormat.ClockRate > 0 ? negotiatedFormat.ClockRate : 48000;
            int channels = negotiatedFormat.ChannelCount is 1 or 2 ? negotiatedFormat.ChannelCount : 2;

            var newEnumerator = new MMDeviceEnumerator();
            MMDevice newDevice;
            try
            {
                newDevice = newEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            catch
            {
                newEnumerator.Dispose();
                throw;
            }

            var newCapture = new WasapiLoopbackCapture(newDevice)
            {
                WaveFormat = new WaveFormat(sampleRate, 16, channels)
            };
            var newBuffer = new BufferedWaveProvider(newCapture.WaveFormat)
            {
                BufferDuration = TimeSpan.FromMilliseconds(500),
                DiscardOnBufferOverflow = true,
                ReadFully = false
            };
            IOpusEncoder? newOpusEncoder = _source == "monitor-mix"
                ? null
                : CreateTunedOpusEncoder(sampleRate, channels, channels == 2 ? LoopbackOpusBitrate : 96000);

            newCapture.DataAvailable += OnLoopbackDataAvailable;
            newCapture.RecordingStopped += OnLoopbackRecordingStopped;

            try
            {
                // Start before swapping the active references. If WASAPI rejects the
                // new endpoint/format, the existing monitor source keeps running.
                newCapture.StartRecording();
            }
            catch
            {
                newCapture.DataAvailable -= OnLoopbackDataAvailable;
                newCapture.RecordingStopped -= OnLoopbackRecordingStopped;
                newCapture.Dispose();
                newOpusEncoder?.Dispose();
                newDevice.Dispose();
                newEnumerator.Dispose();
                throw;
            }

            MMDeviceEnumerator? oldEnumerator;
            MMDevice? oldDevice;
            WasapiLoopbackCapture? oldCapture;
            IOpusEncoder? oldOpusEncoder;

            lock (_loopbackGate)
            {
                if (Volatile.Read(ref _closed) != 0)
                {
                    newCapture.DataAvailable -= OnLoopbackDataAvailable;
                    newCapture.RecordingStopped -= OnLoopbackRecordingStopped;
                    newCapture.StopRecording();
                    newCapture.Dispose();
                    newOpusEncoder?.Dispose();
                    newDevice.Dispose();
                    newEnumerator.Dispose();
                    return;
                }

                oldEnumerator = _deviceEnumerator;
                oldDevice = _loopbackDevice;
                oldCapture = _loopbackCapture;
                oldOpusEncoder = _loopbackOpusEncoder;

                _deviceEnumerator = newEnumerator;
                _loopbackDevice = newDevice;
                _loopbackCapture = newCapture;
                _loopbackBuffer = newBuffer;
                _loopbackOpusEncoder = newOpusEncoder;
                _deviceName = newDevice.FriendlyName;
                _deviceId = newDevice.ID;
                _captureSampleRate = newCapture.WaveFormat.SampleRate;
                _captureChannels = newCapture.WaveFormat.Channels;
            }

            DisposeLoopbackCapture(oldCapture, oldDevice, oldEnumerator);
            try { oldOpusEncoder?.Dispose(); } catch (Exception) { }
        }

        private static IOpusEncoder CreateTunedOpusEncoder(int sampleRate, int channels, int bitrate)
        {
            if (sampleRate != 48000)
                throw new InvalidOperationException($"The tuned Remote Monitor encoder currently requires 48 kHz PCM, but the negotiated rate was {sampleRate} Hz.");
            if (channels is not (1 or 2))
                throw new InvalidOperationException($"The tuned Remote Monitor encoder supports mono/stereo only, but {channels} channels were negotiated.");

            IOpusEncoder encoder = OpusCodecFactory.CreateEncoder(sampleRate, channels);
            encoder.Bitrate = bitrate;
            encoder.UseVBR = true;
            encoder.UseConstrainedVBR = false;
            encoder.UseDTX = false;
            encoder.Complexity = 10;
            encoder.ForceChannels = channels;
            encoder.LSBDepth = 16;
            return encoder;
        }

        private void StartDefaultDeviceWatcher()
        {
            if (_deviceWatchCts is not null) return;

            var cts = new CancellationTokenSource();
            _deviceWatchCts = cts;
            _deviceWatchTask = Task.Run(() => WatchDefaultDeviceAsync(cts.Token));
        }

        private async Task WatchDefaultDeviceAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && Volatile.Read(ref _closed) == 0)
            {
                try
                {
                    await Task.Delay(750, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                string? currentDeviceId;
                try
                {
                    using var probeEnumerator = new MMDeviceEnumerator();
                    using MMDevice defaultDevice = probeEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    currentDeviceId = defaultDevice.ID;
                }
                catch
                {
                    continue;
                }

                string? activeDeviceId;
                lock (_loopbackGate) activeDeviceId = _deviceId;
                if (string.Equals(activeDeviceId, currentDeviceId, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    RestartLoopbackCaptureForCurrentDefault();
                    await SendStateAsync("connected", $"Windows output changed; capture switched automatically to {_deviceName}.");
                }
                catch (Exception exception)
                {
                    await SendStateAsync("connected", $"Windows output changed, but automatic capture restart is retrying: {exception.Message}");
                }
            }
        }

        private void OnLoopbackDataAvailable(object? sender, WaveInEventArgs args)
        {
            if (Volatile.Read(ref _closed) != 0 || args.BytesRecorded <= 0 || _negotiatedFormat is null) return;
            if (sender is not WasapiLoopbackCapture capture) return;

            BufferedWaveProvider? buffer;
            lock (_loopbackGate)
            {
                if (!ReferenceEquals(capture, _loopbackCapture)) return;
                buffer = _loopbackBuffer;
            }
            if (buffer is null) return;

            try
            {
                buffer.AddSamples(args.Buffer, 0, args.BytesRecorded);
                if (_source == "monitor-mix") return;

                int channels = capture.WaveFormat.Channels;
                int sampleRate = capture.WaveFormat.SampleRate;
                int frameBytes = sampleRate * channels * sizeof(short) * FrameMilliseconds / 1000;
                if (frameBytes <= 0) return;

                byte[] frame = new byte[frameBytes];
                while (buffer.BufferedBytes >= frameBytes)
                {
                    int read = buffer.Read(frame, 0, frameBytes);
                    if (read != frameBytes) break;

                    short[] pcm = new short[frameBytes / sizeof(short)];
                    Buffer.BlockCopy(frame, 0, pcm, 0, frameBytes);
                    AudioFormat negotiatedFormat = _negotiatedFormat.Value;
                    byte[] encoded;

                    lock (_loopbackGate)
                    {
                        if (!ReferenceEquals(capture, _loopbackCapture)) return;
                        IOpusEncoder? opusEncoder = _loopbackOpusEncoder;
                        if (opusEncoder is null)
                            encoded = _encoder.EncodeAudio(pcm, negotiatedFormat);
                        else
                        {
                            int frameSamplesPerChannel = sampleRate * FrameMilliseconds / 1000;
                            byte[] packet = new byte[MaxOpusPacketBytes];
                            int encodedLength = opusEncoder.Encode(pcm, frameSamplesPerChannel, packet, packet.Length);
                            if (encodedLength <= 0) continue;
                            encoded = packet.AsSpan(0, encodedLength).ToArray();
                        }
                    }

                    uint rtpUnits = (uint)(negotiatedFormat.RtpClockRate / 1000 * FrameMilliseconds);
                    _peer.SendAudio(rtpUnits, encoded);
                }
            }
            catch (Exception exception)
            {
                _ = SendStateAsync("failed", $"Windows loopback encode failed: {exception.Message}");
            }
        }

        private void OnLoopbackRecordingStopped(object? sender, StoppedEventArgs args)
        {
            if (Volatile.Read(ref _closed) != 0 || sender is not WasapiLoopbackCapture capture) return;

            lock (_loopbackGate)
            {
                if (!ReferenceEquals(capture, _loopbackCapture)) return;
            }

            if (args.Exception is not null)
                _ = SendStateAsync("connected", $"Windows loopback capture paused unexpectedly; automatic device watcher will retry: {args.Exception.Message}");
        }

        private void DisposeLoopbackCapture(
            WasapiLoopbackCapture? capture,
            MMDevice? device,
            MMDeviceEnumerator? enumerator)
        {
            if (capture is not null)
            {
                try { capture.DataAvailable -= OnLoopbackDataAvailable; } catch (Exception) { }
                try { capture.RecordingStopped -= OnLoopbackRecordingStopped; } catch (Exception) { }
                try { capture.StopRecording(); } catch (Exception) { }
                try { capture.Dispose(); } catch (Exception) { }
            }
            try { device?.Dispose(); } catch (Exception) { }
            try { enumerator?.Dispose(); } catch (Exception) { }
        }

        public void RebindEventSink(Func<string, object, Task> sendEventAsync)
        {
            lock (_eventSinkGate) _sendEventAsync = sendEventAsync;
        }

        public Task PublishCurrentStateAsync(string? detail)
        {
            string state = _peer.connectionState.ToString();
            return SendStateAsync(state, detail);
        }

        private void FireAndForgetStateAsync(string phase, string detail) => _ = SendStateAsync(phase, detail);

        private Task SendStateAsync(string state, string? detail) =>
            SendSafelyAsync("monitor.spike.state", new
            {
                state,
                detail,
                source = _source,
                codec = "opus",
                ice = "host-only",
                device = _deviceName,
                sampleRate = _captureSampleRate,
                channels = _captureChannels,
                frameMilliseconds = FrameMilliseconds,
                encoderBitrateKbps = _source switch
                {
                    "monitor-mix" => MonitorMixOpusBitrate / 1000,
                    "windows-loopback" => LoopbackOpusBitrate / 1000,
                    "soundboard-tap" => NativeTapOpusBitrate / 1000,
                    _ => 0
                },
                encoderProfile = _source is "monitor-mix" or "windows-loopback" or "soundboard-tap" ? "hq-audio-vbr" : "library-default",
                mix = _source == "monitor-mix"
                    ? new
                    {
                        windowsGain = Volatile.Read(ref _mixWindowsGain),
                        soundboardGain = Volatile.Read(ref _mixSoundboardGain),
                        mediaGain = Volatile.Read(ref _mixMediaGain),
                        voiceGain = Volatile.Read(ref _mixVoiceGain),
                        voiceEnabled = Volatile.Read(ref _mixVoiceEnabled) != 0,
                        voiceMode = "post-voice-fx-pre-program-mixer",
                        mediaDuplicateSuppressed = Volatile.Read(ref _mediaDuplicateSuppressed) != 0,
                        mediaMode = Volatile.Read(ref _mediaDuplicateSuppressed) != 0 ? "windows-output" : "direct",
                        masterGain = Volatile.Read(ref _mixMasterGain)
                    }
                    : null
            });

        private async Task SendSafelyAsync(string type, object payload)
        {
            if (Volatile.Read(ref _closed) != 0) return;
            Func<string, object, Task> sink;
            lock (_eventSinkGate) sink = _sendEventAsync;
            try { await sink(type, payload); }
            catch (Exception) { }
        }

        public async Task CloseAsync(string reason)
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0) return;

            CancellationTokenSource? deviceWatchCts = _deviceWatchCts;
            Task? deviceWatchTask = _deviceWatchTask;
            _deviceWatchCts = null;
            _deviceWatchTask = null;
            if (deviceWatchCts is not null)
            {
                try { deviceWatchCts.Cancel(); } catch (Exception) { }
            }
            if (deviceWatchTask is not null)
            {
                try { await deviceWatchTask; } catch (OperationCanceledException) { } catch (Exception) { }
            }
            try { deviceWatchCts?.Dispose(); } catch (Exception) { }

            CancellationTokenSource? nativeTapCts = _nativeTapCts;
            Task? nativeTapTask = _nativeTapTask;
            _nativeTapCts = null;
            _nativeTapTask = null;
            if (nativeTapCts is not null)
            {
                try { nativeTapCts.Cancel(); } catch (Exception) { }
            }
            if (nativeTapTask is not null)
            {
                try { await nativeTapTask; } catch (OperationCanceledException) { } catch (Exception) { }
            }
            try { nativeTapCts?.Dispose(); } catch (Exception) { }
            try { _nativeEngine.SetMonitorTapEnabled(false); } catch (Exception) { }
            try { _nativeEngine.ClearMonitorTap(); } catch (Exception) { }
            try { _nativeEngine.SetVoiceMonitorTapEnabled(false); } catch (Exception) { }
            try { _nativeEngine.ClearVoiceMonitorTap(); } catch (Exception) { }
            if (_source == "monitor-mix")
            {
                try { _mediaDeck.SetRemoteMonitorTapEnabled(false); } catch (Exception) { }
            }
            try { _nativeTapOpusEncoder?.Dispose(); } catch (Exception) { }
            _nativeTapOpusEncoder = null;

            WasapiLoopbackCapture? capture;
            MMDevice? device;
            MMDeviceEnumerator? enumerator;
            IOpusEncoder? opusEncoder;
            lock (_loopbackGate)
            {
                capture = _loopbackCapture;
                device = _loopbackDevice;
                enumerator = _deviceEnumerator;
                opusEncoder = _loopbackOpusEncoder;
                _loopbackCapture = null;
                _loopbackBuffer = null;
                _loopbackOpusEncoder = null;
                _loopbackDevice = null;
                _deviceEnumerator = null;
                _deviceName = null;
                _deviceId = null;
                _captureSampleRate = 0;
                _captureChannels = 0;
            }
            DisposeLoopbackCapture(capture, device, enumerator);
            try { opusEncoder?.Dispose(); } catch (Exception) { }

            if (_toneSource is not null)
            {
                try { _toneSource.OnAudioSourceEncodedSample -= _peer.SendAudio; } catch (Exception) { }
                try { await _toneSource.CloseAudio(); } catch (Exception) { }
            }
            try { _peer.Close(reason); } catch (Exception) { }
            try { _peer.Dispose(); } catch (Exception) { }
        }
    }
}
#endif
