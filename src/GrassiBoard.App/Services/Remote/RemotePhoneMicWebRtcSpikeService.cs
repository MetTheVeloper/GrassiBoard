#if REMOTE_MONITOR_SPIKE
using System.Net;
using System.Text.Json;
using GrassiBoard.Services;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace GrassiBoard.Services.Remote;

/// <summary>
/// v1.3 Gate 2: authenticated Phone Mic WebRTC/Opus receive, bounded managed
/// jitter/drift adaptation, and explicit ABI-10 routing into the native Audio Engine.
/// Windows Mic remains the default source until the paired client explicitly routes Phone Mic.
/// </summary>
internal sealed class RemotePhoneMicWebRtcSpikeService : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, PhoneMicSession> _sessions = [];
    private readonly NativeAudioEngine _engine;

    public RemotePhoneMicWebRtcSpikeService(NativeAudioEngine engine)
    {
        _engine = engine;
    }

    public async Task<RemoteCommandResult> HandleOfferAsync(
        Guid clientId,
        JsonElement payload,
        Func<string, object, Task> sendEventAsync)
    {
        if (!TryGetString(payload, "sdp", out string sdp))
            return RemoteCommandResult.Fail("mic_spike_invalid_offer", "The phone microphone WebRTC offer is missing SDP.");

        string captureMode = TryGetOptionalString(payload, "captureMode") ?? "communication";
        if (captureMode is not ("communication" or "clean"))
            return RemoteCommandResult.Fail("mic_spike_invalid_capture_mode", "captureMode must be communication or clean.");

        await CloseAsync(clientId, "Replacing previous Phone Mic Gate 2 session");

        PhoneMicSession session;
        lock (_gate)
        {
            if (_sessions.Count != 0)
                return RemoteCommandResult.Fail("mic_spike_in_use", "Another paired device already owns Remote Phone Mic.");

            try { session = new PhoneMicSession(_engine, sendEventAsync, captureMode); }
            catch (Exception exception)
            {
                return RemoteCommandResult.Fail("mic_spike_create_failed", $"Could not create Phone Mic receiver: {exception.Message}");
            }
            _sessions[clientId] = session;
        }

        try
        {
            RemoteCommandResult result = await session.AcceptOfferAsync(sdp);
            if (!result.Success)
            {
                await CloseAsync(clientId, "Phone Mic offer negotiation failed");
                return result;
            }
            return RemoteCommandResult.Ok();
        }
        catch (Exception exception)
        {
            await CloseAsync(clientId, "Phone Mic offer exception");
            return RemoteCommandResult.Fail("mic_spike_offer_failed", $"Phone Mic negotiation failed: {exception.Message}");
        }
    }

    public Task<RemoteCommandResult> HandleIceCandidateAsync(Guid clientId, JsonElement payload)
    {
        PhoneMicSession? session;
        lock (_gate) _sessions.TryGetValue(clientId, out session);
        if (session is null) return Task.FromResult(RemoteCommandResult.Ok());

        if (!TryGetString(payload, "candidate", out string candidate))
            return Task.FromResult(RemoteCommandResult.Ok());

        string? sdpMid = TryGetOptionalString(payload, "sdpMid");
        ushort sdpMLineIndex = 0;
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("sdpMLineIndex", out JsonElement indexElement) &&
            indexElement.ValueKind == JsonValueKind.Number &&
            indexElement.TryGetInt32(out int parsedIndex) &&
            parsedIndex is >= 0 and <= ushort.MaxValue)
        {
            sdpMLineIndex = (ushort)parsedIndex;
        }

        return Task.FromResult(session.AddIceCandidate(
            candidate,
            sdpMid,
            sdpMLineIndex,
            TryGetOptionalString(payload, "usernameFragment")));
    }

    public async Task<RemoteCommandResult> HandleRouteAsync(Guid clientId, JsonElement payload)
    {
        PhoneMicSession? session;
        lock (_gate) _sessions.TryGetValue(clientId, out session);
        if (session is null)
            return RemoteCommandResult.Fail("mic_spike_no_session", "Start Phone Mic before changing the Audio Engine route.");

        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("enabled", out JsonElement enabledElement) ||
            enabledElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return RemoteCommandResult.Fail("mic_spike_invalid_route", "enabled must be true or false.");

        return await session.SetRouteEnabledAsync(enabledElement.GetBoolean());
    }

    public async Task RebindClientAsync(Guid clientId, Func<string, object, Task> sendEventAsync)
    {
        PhoneMicSession? session;
        lock (_gate) _sessions.TryGetValue(clientId, out session);
        if (session is null) return;

        session.RebindEventSink(sendEventAsync);
        await session.PublishCurrentStateAsync("WSS reconnected; existing Phone Mic media session preserved.");
    }

    public async Task<RemoteCommandResult> StopAsync(Guid clientId)
    {
        await CloseAsync(clientId, "Stopped by GrassiMote");
        return RemoteCommandResult.Ok();
    }

    public async Task CloseAsync(Guid clientId, string reason)
    {
        PhoneMicSession? session = null;
        lock (_gate)
        {
            if (_sessions.Remove(clientId, out PhoneMicSession? removed))
                session = removed;
        }
        if (session is not null) await session.CloseAsync(reason);
    }

    public async Task CloseAllAsync(string reason)
    {
        PhoneMicSession[] sessions;
        lock (_gate)
        {
            sessions = _sessions.Values.ToArray();
            _sessions.Clear();
        }
        foreach (PhoneMicSession session in sessions)
            await session.CloseAsync(reason);
    }

    public async ValueTask DisposeAsync() => await CloseAllAsync("Phone Mic Gate 2 disposed");

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

    private sealed class PhoneMicSession
    {
        private const long PublishEveryMs = 250;
        // RFC 7587/WebRTC advertises Opus as opus/48000/2 in SDP, but the
        // accepted SIPSorcery 10.0.13 AudioEncoder decodes to its mono PCM
        // convention. Do NOT use AudioFormat.ChannelCount for decoded PCM.
        private const int OpusDecodedPcmChannels = 1;

        private readonly RTCPeerConnection _peer;
        private readonly AudioEncoder _decoder;
        private readonly RemotePhoneMicPcmBridge _bridge;
        private readonly object _eventGate = new();
        private readonly object _statsGate = new();
        private Func<string, object, Task> _sendEventAsync;
        private readonly string _captureMode;

        private AudioFormat? _negotiatedFormat;
        private long _rtpPackets;
        private long _decodedFrames;
        private long _decodedSamples;
        private long _decodeErrors;
        private long _lastPublishTick;
        private double _rmsDbfs = -96.0;
        private double _peakDbfs = -96.0;
        private int _frameMs;
        private int _closed;

        public PhoneMicSession(
            NativeAudioEngine engine,
            Func<string, object, Task> sendEventAsync,
            string captureMode)
        {
            _sendEventAsync = sendEventAsync;
            _captureMode = captureMode;
            _decoder = new AudioEncoder(includeOpus: true);
            _bridge = new RemotePhoneMicPcmBridge(engine);

            List<AudioFormat> formats = _decoder.SupportedFormats
                .Where(format => format.Codec == AudioCodecsEnum.OPUS)
                .ToList();
            if (formats.Count == 0)
                throw new InvalidOperationException("The accepted SIPSorcery build exposes no Opus format.");

            _peer = new RTCPeerConnection();
            _peer.addTrack(new MediaStreamTrack(formats, MediaStreamStatusEnum.RecvOnly));
            _peer.OnAudioFormatsNegotiated += OnAudioFormatsNegotiated;
            _peer.OnRtpPacketReceived += OnRtpPacketReceived;
            _peer.onicecandidate += OnIceCandidate;
            _peer.onconnectionstatechange += state => _ = HandleConnectionStateAsync(state);
            _peer.oniceconnectionstatechange += state => _ = SendStateAsync("negotiating", $"ICE: {state}");
        }

        public async Task<RemoteCommandResult> AcceptOfferAsync(string sdp)
        {
            await SendStateAsync("negotiating", "Applying Android microphone offer.");
            var offer = new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = sdp };
            SetDescriptionResultEnum setResult = _peer.setRemoteDescription(offer);
            if (setResult != SetDescriptionResultEnum.OK)
                return RemoteCommandResult.Fail("mic_spike_sdp_rejected", $"Android SDP rejected: {setResult}.");

            RTCSessionDescriptionInit answer = _peer.createAnswer();
            await _peer.setLocalDescription(answer);
            await SendSafelyAsync("mic.spike.answer", new { type = "answer", sdp = answer.sdp });
            await SendStateAsync("negotiating", "Answer sent; waiting for same-LAN ICE/DTLS and microphone RTP.");
            return RemoteCommandResult.Ok();
        }

        public RemoteCommandResult AddIceCandidate(
            string candidate,
            string? sdpMid,
            ushort sdpMLineIndex,
            string? usernameFragment)
        {
            if (Volatile.Read(ref _closed) != 0)
                return RemoteCommandResult.Fail("mic_spike_closed", "Phone Mic Gate 2 session is closed.");

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
                return RemoteCommandResult.Fail("mic_spike_ice_failed", $"Could not add Android ICE candidate: {exception.Message}");
            }
        }

        private void OnAudioFormatsNegotiated(List<AudioFormat> formats)
        {
            foreach (AudioFormat format in formats)
            {
                if (format.Codec != AudioCodecsEnum.OPUS) continue;
                _negotiatedFormat = format;
                _ = SendStateAsync(
                    "negotiating",
                    $"Opus SDP negotiated at {format.ClockRate} Hz / {Math.Max(1, format.ChannelCount)}; decoded PCM is mono.");
                return;
            }
            _ = SendStateAsync("failed", "WebRTC connected without negotiated Opus.");
        }

        private void OnRtpPacketReceived(IPEndPoint remoteEndPoint, SDPMediaTypesEnum mediaType, RTPPacket rtpPacket)
        {
            if (Volatile.Read(ref _closed) != 0 ||
                mediaType != SDPMediaTypesEnum.audio ||
                rtpPacket.Payload is null ||
                rtpPacket.Payload.Length == 0)
                return;

            Interlocked.Increment(ref _rtpPackets);

            AudioFormat? negotiated = _negotiatedFormat;
            if (negotiated is null)
            {
                MaybePublishStats();
                return;
            }

            try
            {
                AudioFormat format = negotiated.Value;
                short[] pcm = _decoder.DecodeAudio(rtpPacket.Payload, format);
                if (pcm.Length != 0)
                {
                    // SIPSorcery 10.0.13's AudioEncoder returns mono PCM for
                    // Opus even though the SDP clock format is opus/48000/2.
                    // Treating format.ChannelCount (2) as decoded PCM stereo
                    // halves the timebase: 20 ms becomes 10 ms, producing an
                    // approximately +12 semitone pitch error and continuous
                    // bridge starvation/underruns.
                    int decodedPcmChannels = OpusDecodedPcmChannels;
                    int sampleRate = format.ClockRate > 0 ? format.ClockRate : 48000;
                    int perChannel = pcm.Length / decodedPcmChannels;

                    double sum = 0.0;
                    int peak = 0;
                    foreach (short sample in pcm)
                    {
                        int abs = sample == short.MinValue ? 32768 : Math.Abs((int)sample);
                        if (abs > peak) peak = abs;
                        double normalized = sample / 32768.0;
                        sum += normalized * normalized;
                    }

                    double rms = Math.Sqrt(sum / pcm.Length);
                    double peakNormalized = peak / 32768.0;
                    double rmsDb = rms > 0.000001 ? 20.0 * Math.Log10(rms) : -96.0;
                    double peakDb = peakNormalized > 0.000001 ? 20.0 * Math.Log10(peakNormalized) : -96.0;

                    Interlocked.Increment(ref _decodedFrames);
                    Interlocked.Add(ref _decodedSamples, pcm.Length);
                    lock (_statsGate)
                    {
                        _rmsDbfs = Math.Clamp(rmsDb, -96.0, 0.0);
                        _peakDbfs = Math.Clamp(peakDb, -96.0, 0.0);
                        _frameMs = perChannel > 0
                            ? Math.Max(1, (int)Math.Round(perChannel * 1000.0 / sampleRate))
                            : 0;
                    }
                    _bridge.PushDecoded(pcm, decodedPcmChannels, sampleRate);
                }
            }
            catch
            {
                Interlocked.Increment(ref _decodeErrors);
            }

            MaybePublishStats();
        }

        private void MaybePublishStats()
        {
            long now = Environment.TickCount64;
            long previous = Volatile.Read(ref _lastPublishTick);
            if (now - previous < PublishEveryMs) return;
            if (Interlocked.CompareExchange(ref _lastPublishTick, now, previous) != previous) return;
            _ = SendStateAsync("connected", null);
        }

        private void OnIceCandidate(RTCIceCandidate candidate)
        {
            if (candidate is null || string.IsNullOrWhiteSpace(candidate.candidate)) return;
            _ = SendSafelyAsync("mic.spike.ice", new
            {
                candidate = candidate.candidate,
                sdpMid = candidate.sdpMid,
                sdpMLineIndex = candidate.sdpMLineIndex,
                usernameFragment = candidate.usernameFragment
            });
        }

        private async Task HandleConnectionStateAsync(RTCPeerConnectionState state)
        {
            await SendStateAsync(
                state.ToString(),
                state == RTCPeerConnectionState.connected
                    ? "Phone Mic WebRTC connected. Gate 2 transport is ready; routing to Audio Engine remains explicit."
                    : null);

            if (state is RTCPeerConnectionState.failed or RTCPeerConnectionState.closed)
                await CloseAsync($"Peer state: {state}");
        }

        public async Task<RemoteCommandResult> SetRouteEnabledAsync(bool enabled)
        {
            if (enabled && _peer.connectionState != RTCPeerConnectionState.connected)
            {
                return RemoteCommandResult.Fail(
                    "mic_spike_route_not_ready",
                    "Wait for Phone Mic WebRTC to reach connected before routing it to the Audio Engine.");
            }

            NativeResult result = _bridge.SetRouteEnabled(enabled);
            if (result != NativeResult.Ok)
            {
                return RemoteCommandResult.Fail(
                    "mic_spike_native_route_failed",
                    $"Could not switch Audio Engine input source: {result}.");
            }

            await SendStateAsync(
                _peer.connectionState.ToString(),
                enabled
                    ? "Phone Mic route armed. Building the bounded managed/native prebuffer before the atomic input switch."
                    : "Audio Engine input returned to the Windows microphone.");
            return RemoteCommandResult.Ok();
        }

        public void RebindEventSink(Func<string, object, Task> sendEventAsync)
        {
            lock (_eventGate) _sendEventAsync = sendEventAsync;
        }

        public Task PublishCurrentStateAsync(string? detail) =>
            SendStateAsync(_peer.connectionState.ToString(), detail);

        private Task SendStateAsync(string state, string? detail)
        {
            double rms, peak;
            int frameMs;
            lock (_statsGate)
            {
                rms = _rmsDbfs;
                peak = _peakDbfs;
                frameMs = _frameMs;
            }

            AudioFormat? format = _negotiatedFormat;
            RemotePhoneMicBridgeStatistics bridge = _bridge.GetStatistics();
            return SendSafelyAsync("mic.spike.state", new
            {
                state,
                detail,
                captureMode = _captureMode,
                codec = format is null ? null : "opus",
                ice = "host-only",
                sampleRate = format?.ClockRate ?? 0,
                channels = format is null ? 0 : OpusDecodedPcmChannels,
                sdpChannels = format is null ? 0 : Math.Max(1, format.Value.ChannelCount),
                frameMilliseconds = frameMs,
                rtpPackets = Interlocked.Read(ref _rtpPackets),
                decodedFrames = Interlocked.Read(ref _decodedFrames),
                decodedSamples = Interlocked.Read(ref _decodedSamples),
                decodeErrors = Interlocked.Read(ref _decodeErrors),
                rmsDbfs = rms,
                peakDbfs = peak,
                transportOnly = false,
                nativeAbi = 10,
                routeRequested = bridge.RouteRequested,
                routedToAudioEngine = bridge.Routed,
                nativeRequestedSourceMode = bridge.NativeRequestedSourceMode,
                nativeSourceMode = bridge.NativeActiveSourceMode,
                jitterFillFrames = bridge.JitterFillFrames,
                jitterTargetFrames = bridge.JitterTargetFrames,
                jitterDroppedFrames = bridge.DroppedFrames,
                bridgeUnderruns = bridge.BridgeUnderruns,
                nativeShortWrites = bridge.NativeShortWrites,
                driftCorrection = bridge.DriftCorrection,
                nativeRemoteFillFrames = bridge.NativeFillFrames,
                nativeRemoteCapacityFrames = bridge.NativeCapacityFrames,
                nativeRemotePushedFrames = bridge.NativePushedFrames,
                nativeRemoteConsumedFrames = bridge.NativeConsumedFrames,
                nativeRemoteUnderrunFrames = bridge.NativeUnderrunFrames,
                nativeRemoteOverrunFrames = bridge.NativeOverrunFrames
            });
        }

        private async Task SendSafelyAsync(string type, object payload)
        {
            if (Volatile.Read(ref _closed) != 0) return;
            Func<string, object, Task> sink;
            lock (_eventGate) sink = _sendEventAsync;
            try { await sink(type, payload); } catch (Exception) { }
        }

        public async Task CloseAsync(string reason)
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0) return;
            try { _peer.OnAudioFormatsNegotiated -= OnAudioFormatsNegotiated; } catch (Exception) { }
            try { _peer.OnRtpPacketReceived -= OnRtpPacketReceived; } catch (Exception) { }
            try { _peer.Close(reason); } catch (Exception) { }
            try { _peer.Dispose(); } catch (Exception) { }
            await _bridge.DisposeAsync();
        }
    }
}
#endif
