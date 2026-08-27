using System.Runtime.InteropServices;
using GrassiBoard.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace GrassiBoard.Services.Looper;

internal sealed class LooperMonitorService : IDisposable
{
    public const int SampleRate = 48_000;
    public const int Channels = 2;
    public const int MaxSupportedLoopMinutes = 10;
    public const long MaxSupportedLoopFrames = (long)SampleRate * 60L * MaxSupportedLoopMinutes;
    public const long MaxStereoFloatBytes = MaxSupportedLoopFrames * Channels * sizeof(float);

    private const uint PrebufferFrames = 1_920U;
    private const uint DriftBandFrames = 720U;

    private readonly Func<string?> _monitorDeviceIdProvider;
    private readonly NativeLooperSampleProvider _sampleProvider;
    private NativeAudioEngine? _engine;
    private MMDeviceEnumerator? _deviceEnumerator;
    private MMDevice? _device;
    private WasapiOut? _output;
    private string _openedDeviceId = string.Empty;
    private bool _primed;
    private float _lastLeft;
    private float _lastRight;
    private float[] _nativeReadBuffer = new float[8_192];
    private bool _disposed;

    public LooperMonitorService(Func<string?>? monitorDeviceIdProvider = null)
    {
        _monitorDeviceIdProvider = monitorDeviceIdProvider ?? (() => null);
        _sampleProvider = new NativeLooperSampleProvider(this);
    }

    public string MonitorDeviceName { get; private set; } = "Windows default output";
    public string LastError { get; private set; } = string.Empty;
    public ulong LocalUnderrunCount { get; private set; }
    public ulong LocalDriftCorrectionCount { get; private set; }

    public bool IsProgramEngineRunning
    {
        get
        {
            NativeAudioEngine? engine = ResolveEngine();
            return engine is not null &&
                engine.GetStatistics(out AudioStatistics statistics) == NativeResult.Ok &&
                statistics.Running != 0U;
        }
    }

    public NativeResult ConfigureLoop(float[] interleavedStereoSamples, long startFrame, long frameCount)
    {
        ThrowIfDisposed();
        if (frameCount <= 0L || frameCount > MaxSupportedLoopFrames) return NativeResult.InvalidArgument;
        NativeAudioEngine? engine = ResolveEngine();
        if (engine is null)
        {
            LastError = "Start the GrassiBoard audio engine before using Looper playback.";
            return NativeResult.NotRunning;
        }

        NativeResult result = engine.LoadLooperMaster(interleavedStereoSamples, startFrame, frameCount);
        if (result == NativeResult.Ok)
        {
            ResetMonitorReadState();
            LastError = string.Empty;
        }
        else
        {
            LastError = $"Native Looper could not load the Master selection ({result}).";
        }
        return result;
    }

    public bool Play()
    {
        ThrowIfDisposed();
        NativeAudioEngine? engine = ResolveEngine();
        if (engine is null)
        {
            LastError = "Start the GrassiBoard audio engine before using Looper playback.";
            return false;
        }

        try
        {
            EnsureOutput();
            NativeResult result = engine.SetLooperTransport(LooperTransportState.Playing);
            if (result != NativeResult.Ok)
            {
                LastError = $"Native Looper Play failed ({result}).";
                return false;
            }
            if (_output!.PlaybackState != PlaybackState.Playing) _output.Play();
            LastError = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or ArgumentException)
        {
            engine.SetLooperTransport(LooperTransportState.Paused);
            LastError = $"Looper local monitor could not start: {exception.Message}";
            return false;
        }
    }

    public void Pause()
    {
        if (_disposed) return;
        _engine?.SetLooperTransport(LooperTransportState.Paused);
        ResetMonitorReadState();
        try { _output?.Pause(); } catch (InvalidOperationException) { }
    }

    public void Stop()
    {
        if (_disposed) return;
        _engine?.SetLooperTransport(LooperTransportState.Stopped);
        ResetMonitorReadState();
        try { _output?.Pause(); } catch (InvalidOperationException) { }
    }

    public void Clear()
    {
        if (_disposed) return;
        Stop();
        _engine?.ClearLooper();
        LastError = string.Empty;
    }

    public bool TryGetState(out LooperNativeState state)
    {
        state = default;
        NativeAudioEngine? engine = ResolveEngine();
        return engine is not null && engine.GetLooperState(out state) == NativeResult.Ok;
    }

    private NativeAudioEngine? ResolveEngine()
    {
        if (_engine is not null && _engine.IsAvailable) return _engine;
        _engine = NativeAudioEngine.FindRunningProcessEngine();
        return _engine;
    }

    private void EnsureOutput()
    {
        string requestedDeviceId = _monitorDeviceIdProvider()?.Trim() ?? string.Empty;
        if (_output is not null && string.Equals(requestedDeviceId, _openedDeviceId, StringComparison.Ordinal)) return;

        DisposeOutput();
        _deviceEnumerator = new MMDeviceEnumerator();
        _device = string.IsNullOrWhiteSpace(requestedDeviceId)
            ? _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
            : _deviceEnumerator.GetDevice(requestedDeviceId);
        _openedDeviceId = requestedDeviceId;
        MonitorDeviceName = _device.FriendlyName;
        _output = new WasapiOut(_device, AudioClientShareMode.Shared, true, 30);
        _output.Init(_sampleProvider);
        _output.PlaybackStopped += OnPlaybackStopped;
    }

    private int ReadForMonitor(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        if (_disposed || count <= 0 || count % Channels != 0) return count;
        NativeAudioEngine? engine = _engine;
        if (engine is null || engine.GetLooperState(out LooperNativeState state) != NativeResult.Ok ||
            state.Transport != (uint)LooperTransportState.Playing || state.LoopFrames == 0U)
        {
            _primed = false;
            return count;
        }

        uint requestedFrames = checked((uint)(count / Channels));
        if (!_primed)
        {
            if (state.MonitorFillFrames < PrebufferFrames) return count;
            _primed = true;
        }

        int correction = 0;
        if (state.MonitorFillFrames > PrebufferFrames + DriftBandFrames) correction = 1;
        else if (state.MonitorFillFrames + DriftBandFrames < PrebufferFrames && requestedFrames > 1U) correction = -1;

        uint nativeFramesWanted = checked((uint)Math.Max(1, (long)requestedFrames + correction));
        EnsureNativeBuffer(nativeFramesWanted);
        NativeResult result = engine.ReadLooperMonitor(_nativeReadBuffer, nativeFramesWanted, out uint readFrames);
        if (result != NativeResult.Ok)
        {
            LastError = $"Looper monitor tap read failed ({result}).";
            _primed = false;
            return count;
        }

        uint sourceStart = 0U;
        uint sourceFrames = readFrames;
        if (correction > 0 && sourceFrames > requestedFrames)
        {
            sourceStart = sourceFrames - requestedFrames;
            sourceFrames = requestedFrames;
            LocalDriftCorrectionCount++;
        }

        uint framesToCopy = Math.Min(sourceFrames, requestedFrames);
        for (uint frame = 0U; frame < framesToCopy; frame++)
        {
            uint sourceFrame = sourceStart + frame;
            float left = _nativeReadBuffer[sourceFrame * Channels];
            float right = _nativeReadBuffer[sourceFrame * Channels + 1U];
            int destination = offset + checked((int)frame * Channels);
            buffer[destination] = float.IsFinite(left) ? left : 0.0F;
            buffer[destination + 1] = float.IsFinite(right) ? right : 0.0F;
            _lastLeft = buffer[destination];
            _lastRight = buffer[destination + 1];
        }

        if (correction < 0 && framesToCopy + 1U == requestedFrames && framesToCopy > 0U)
        {
            int destination = offset + checked((int)framesToCopy * Channels);
            buffer[destination] = _lastLeft;
            buffer[destination + 1] = _lastRight;
            framesToCopy++;
            LocalDriftCorrectionCount++;
        }

        if (framesToCopy < requestedFrames)
        {
            LocalUnderrunCount++;
            _primed = false;
        }
        return count;
    }

    private void EnsureNativeBuffer(uint frameCount)
    {
        int requiredSamples = checked((int)frameCount * Channels);
        if (_nativeReadBuffer.Length < requiredSamples)
        {
            _nativeReadBuffer = new float[Math.Max(requiredSamples, _nativeReadBuffer.Length * 2)];
        }
    }

    private void ResetMonitorReadState()
    {
        _primed = false;
        _lastLeft = 0.0F;
        _lastRight = 0.0F;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_disposed || e.Exception is null) return;
        _engine?.SetLooperTransport(LooperTransportState.Paused);
        ResetMonitorReadState();
        LastError = $"Looper local monitor stopped: {e.Exception.Message}";
    }

    private void DisposeOutput()
    {
        if (_output is not null)
        {
            _output.PlaybackStopped -= OnPlaybackStopped;
            try { _output.Stop(); } catch { }
            _output.Dispose();
            _output = null;
        }
        _device?.Dispose();
        _device = null;
        _deviceEnumerator?.Dispose();
        _deviceEnumerator = null;
        _openedDeviceId = string.Empty;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LooperMonitorService));
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _engine?.ClearLooper();
        _disposed = true;
        DisposeOutput();
        _engine = null;
    }

    private sealed class NativeLooperSampleProvider(LooperMonitorService owner) : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);
        public int Read(float[] buffer, int offset, int count) => owner.ReadForMonitor(buffer, offset, count);
    }
}
