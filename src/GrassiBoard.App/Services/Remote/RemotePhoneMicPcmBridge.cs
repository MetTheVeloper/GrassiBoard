#if REMOTE_MONITOR_SPIKE
using GrassiBoard.Services;

namespace GrassiBoard.Services.Remote;

/// <summary>
/// Non-realtime bridge between decoded WebRTC PCM and the ABI-10 native Remote
/// Input SPSC ring. It owns all downmixing, jitter buffering and tiny drift
/// corrections; the native render thread only consumes bounded 48 kHz mono PCM.
/// </summary>
internal sealed class RemotePhoneMicPcmBridge : IAsyncDisposable
{
    internal const int OutputSampleRate = 48_000;
    internal const int OutputBlockFrames = 480; // nominal 10 ms
    internal const int TargetJitterFrames = 1_440; // 30 ms managed clock target
    internal const int NativePrebufferFrames = 1_440; // 30 ms before source switch
    internal const int NativeTargetFillFrames = 1_440; // 30 ms realtime safety reservoir

    private const int JitterCapacityFrames = 24_000; // 500 ms hard bound
    private const int ManagedReserveFrames = 960; // keep 20 ms above the native handoff
    private const int StartupJitterFrames = TargetJitterFrames + NativePrebufferFrames; // 60 ms cold-start reserve
    private const int SourceDriftThresholdFrames = 480;
    private const int MinimumAdaptiveFrames = OutputBlockFrames - 1;
    private const int MaximumAdaptiveFrames = OutputBlockFrames + 1;
    private const int MaxNativeRefillBlocksPerTick = 6; // bounded catch-up; max 60 ms per scheduler wake

    private readonly NativeAudioEngine _engine;
    private readonly object _bufferGate = new();
    private readonly float[] _jitter = new float[JitterCapacityFrames];
    private readonly float[] _scratch = new float[MaximumAdaptiveFrames];
    private readonly float[] _output = new float[MaximumAdaptiveFrames];
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pumpTask;

    private int _readIndex;
    private int _writeIndex;
    private int _fillFrames;
    private int _routeRequested;
    private int _routeEnabled;
    private int _switchPending;
    private long _switchPendingSinceTick;
    private int _started;
    private long _droppedFrames;
    private long _bridgeUnderruns;
    private long _nativeShortWrites;
    private int _lastConsumeFrames = OutputBlockFrames;
    private int _lastPushFrames = OutputBlockFrames;

    public RemotePhoneMicPcmBridge(NativeAudioEngine engine)
    {
        _engine = engine;
        _pumpTask = Task.Run(PumpAsync);
    }

    public void PushDecoded(short[] pcm, int channelCount, int sampleRate)
    {
        if (pcm.Length == 0 || channelCount <= 0 || sampleRate <= 0) return;
        int sourceFrames = pcm.Length / channelCount;
        if (sourceFrames <= 0) return;

        lock (_bufferGate)
        {
            if (sampleRate == OutputSampleRate)
            {
                for (int frame = 0; frame < sourceFrames; ++frame)
                    WriteSampleLocked(DownmixFrame(pcm, frame, channelCount));
                return;
            }

            int outputFrames = Math.Max(1, (int)Math.Round(sourceFrames * (double)OutputSampleRate / sampleRate));
            if (outputFrames == 1 || sourceFrames == 1)
            {
                WriteSampleLocked(DownmixFrame(pcm, 0, channelCount));
                return;
            }

            double sourceScale = (sourceFrames - 1.0) / (outputFrames - 1.0);
            for (int frame = 0; frame < outputFrames; ++frame)
            {
                double position = frame * sourceScale;
                int leftIndex = Math.Min(sourceFrames - 1, (int)position);
                int rightIndex = Math.Min(sourceFrames - 1, leftIndex + 1);
                float fraction = (float)(position - leftIndex);
                float left = DownmixFrame(pcm, leftIndex, channelCount);
                float right = DownmixFrame(pcm, rightIndex, channelCount);
                WriteSampleLocked(left + (right - left) * fraction);
            }
        }
    }

    public NativeResult SetRouteEnabled(bool enabled)
    {
        if (enabled)
        {
            if (Volatile.Read(ref _routeRequested) != 0) return NativeResult.Ok;

            NativeResult statisticsResult = _engine.GetStatistics(out AudioStatistics statistics);
            if (statisticsResult != NativeResult.Ok) return statisticsResult;
            if (statistics.Running == 0U) return NativeResult.NotRunning;

            NativeResult nativeStatisticsResult = _engine.GetRemoteInputStatistics(out RemoteInputStatistics native);
            if (nativeStatisticsResult != NativeResult.Ok) return nativeStatisticsResult;
            if (native.RequestedSourceMode != (uint)RemoteInputSourceMode.Windows ||
                native.ActiveSourceMode != (uint)RemoteInputSourceMode.Windows)
                return NativeResult.Internal;

            // Reset only while the realtime consumer is still on Windows Mic.
            // The pump first prebuffers both managed and native rings, then flips
            // the atomic native source selector to Remote in one bounded step.
            ResetManagedBuffer();
            ResetManagedDiagnostics();
            NativeResult reset = _engine.ResetRemoteInput();
            if (reset != NativeResult.Ok) return reset;

            Volatile.Write(ref _routeEnabled, 0);
            Volatile.Write(ref _switchPending, 0);
            Volatile.Write(ref _routeRequested, 1);
            return NativeResult.Ok;
        }

        Volatile.Write(ref _routeRequested, 0);
        Volatile.Write(ref _routeEnabled, 0);
        Volatile.Write(ref _switchPending, 0);
        Volatile.Write(ref _started, 0);

        // Move the realtime consumer away first. Do not reset the native ring
        // here: a render block may still be finishing with the old source. The
        // next enable performs the reset while Windows is confirmed active.
        NativeResult routeResult = _engine.SetInputSourceMode(RemoteInputSourceMode.Windows);
        ResetManagedBuffer();
        return routeResult;
    }

    public RemotePhoneMicBridgeStatistics GetStatistics()
    {
        int fill;
        lock (_bufferGate) fill = _fillFrames;

        RemoteInputStatistics native = default;
        NativeResult nativeResult = _engine.GetRemoteInputStatistics(out native);
        AudioStatistics audio = default;
        NativeResult audioResult = _engine.GetStatistics(out audio);
        bool routed = Volatile.Read(ref _routeEnabled) != 0 &&
            nativeResult == NativeResult.Ok && native.ActiveSourceMode == (uint)RemoteInputSourceMode.Remote &&
            audioResult == NativeResult.Ok && audio.Running != 0U;

        return new RemotePhoneMicBridgeStatistics(
            RouteRequested: Volatile.Read(ref _routeRequested) != 0,
            Routed: routed,
            JitterFillFrames: fill,
            JitterTargetFrames: TargetJitterFrames,
            DroppedFrames: Interlocked.Read(ref _droppedFrames),
            BridgeUnderruns: Interlocked.Read(ref _bridgeUnderruns),
            NativeShortWrites: Interlocked.Read(ref _nativeShortWrites),
            DriftCorrection: DescribeDrift(),
            NativeAvailable: nativeResult == NativeResult.Ok,
            NativeRequestedSourceMode: native.RequestedSourceMode,
            NativeActiveSourceMode: native.ActiveSourceMode,
            NativeFillFrames: native.FillFrames,
            NativeCapacityFrames: native.CapacityFrames,
            NativePushedFrames: native.PushedFrames,
            NativeConsumedFrames: native.ConsumedFrames,
            NativeUnderrunFrames: native.UnderrunFrames,
            NativeOverrunFrames: native.OverrunFrames);
    }

    private async Task PumpAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));
        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                if (Volatile.Read(ref _routeRequested) == 0) continue;

                NativeResult audioResult = _engine.GetStatistics(out AudioStatistics audio);
                NativeResult nativeResult = _engine.GetRemoteInputStatistics(out RemoteInputStatistics native);
                if (audioResult != NativeResult.Ok || audio.Running == 0U || nativeResult != NativeResult.Ok)
                {
                    FailSafeRouteToWindows();
                    continue;
                }

                bool routed = Volatile.Read(ref _routeEnabled) != 0;
                bool switchPending = Volatile.Read(ref _switchPending) != 0;

                if (routed)
                {
                    if (native.ActiveSourceMode != (uint)RemoteInputSourceMode.Remote)
                    {
                        // An Audio Engine restart or external source change must
                        // never silently re-arm Phone Mic.
                        FailSafeRouteToWindows();
                        continue;
                    }
                }
                else if (switchPending)
                {
                    if (native.ActiveSourceMode == (uint)RemoteInputSourceMode.Remote)
                    {
                        Volatile.Write(ref _switchPending, 0);
                        Volatile.Write(ref _routeEnabled, 1);
                        routed = true;
                    }
                    else if (native.ActiveSourceMode == (uint)RemoteInputSourceMode.Windows &&
                        Environment.TickCount64 - Volatile.Read(ref _switchPendingSinceTick) < 1000)
                    {
                        // The requested mode is atomic, but the realtime worker
                        // acknowledges it only at its next render boundary.
                        continue;
                    }
                    else
                    {
                        FailSafeRouteToWindows();
                        continue;
                    }
                }
                else if (native.RequestedSourceMode != (uint)RemoteInputSourceMode.Windows ||
                    native.ActiveSourceMode != (uint)RemoteInputSourceMode.Windows)
                {
                    FailSafeRouteToWindows();
                    continue;
                }

                int fill;
                lock (_bufferGate) fill = _fillFrames;
                if (Volatile.Read(ref _started) == 0)
                {
                    // Cold start must contain both the managed jitter reserve and
                    // the native prebuffer. Otherwise moving the first 30 ms into
                    // native would immediately empty the managed jitter stage.
                    if (fill < StartupJitterFrames) continue;
                    Volatile.Write(ref _started, 1);
                }

                // The realtime render clock is the output clock. Do not assume
                // the managed 10 ms timer is itself an audio clock: a late wake
                // may need several bounded 10 ms pushes to restore native fill.
                RemoteInputStatistics refill = native;
                int refillBlocks = 0;
                while (refill.FillFrames < NativeTargetFillFrames &&
                    refillBlocks < MaxNativeRefillBlocksPerTick)
                {
                    lock (_bufferGate) fill = _fillFrames;
                    int consumeFrames = ChooseSourceConsumeFrames(fill);

                    // Preserve 20 ms on the managed side so scheduler catch-up
                    // cannot simply move all jitter protection into native.
                    if (fill < ManagedReserveFrames + consumeFrames)
                        break;

                    Volatile.Write(ref _lastConsumeFrames, consumeFrames);
                    Volatile.Write(ref _lastPushFrames, OutputBlockFrames);

                    if (!TryReadResampledBlock(consumeFrames, OutputBlockFrames))
                    {
                        Interlocked.Increment(ref _bridgeUnderruns);
                        Volatile.Write(ref _started, 0);
                        break;
                    }

                    NativeResult pushResult = _engine.PushRemoteInput(
                        _output, OutputBlockFrames, out uint acceptedFrames);
                    if (pushResult != NativeResult.Ok || acceptedFrames != OutputBlockFrames)
                    {
                        Interlocked.Increment(ref _nativeShortWrites);
                        break;
                    }

                    ++refillBlocks;
                    NativeResult refreshedResult = _engine.GetRemoteInputStatistics(out refill);
                    if (refreshedResult != NativeResult.Ok)
                    {
                        FailSafeRouteToWindows();
                        break;
                    }
                }

                if (Volatile.Read(ref _routeRequested) == 0) continue;

                if (!routed && refill.FillFrames >= NativePrebufferFrames)
                {
                    NativeResult routeResult = _engine.SetInputSourceMode(RemoteInputSourceMode.Remote);
                    if (routeResult == NativeResult.Ok && Volatile.Read(ref _routeRequested) != 0)
                    {
                        Volatile.Write(ref _switchPendingSinceTick, Environment.TickCount64);
                        Volatile.Write(ref _switchPending, 1);
                    }
                    else
                    {
                        // A concurrent Return/Stop wins over the pending switch.
                        _engine.SetInputSourceMode(RemoteInputSourceMode.Windows);
                        Volatile.Write(ref _switchPending, 0);
                        Volatile.Write(ref _routeEnabled, 0);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
    }

    private static int ChooseSourceConsumeFrames(int fillFrames)
    {
        if (fillFrames > TargetJitterFrames + SourceDriftThresholdFrames)
            return MaximumAdaptiveFrames;
        if (fillFrames < TargetJitterFrames - SourceDriftThresholdFrames)
            return MinimumAdaptiveFrames;
        return OutputBlockFrames;
    }

    private bool TryReadResampledBlock(int consumeFrames, int outputFrames)
    {
        lock (_bufferGate)
        {
            if (_fillFrames < consumeFrames)
            {
                _readIndex = _writeIndex;
                _fillFrames = 0;
                return false;
            }

            for (int frame = 0; frame < consumeFrames; ++frame)
            {
                _scratch[frame] = _jitter[_readIndex];
                _readIndex = (_readIndex + 1) % _jitter.Length;
            }
            _fillFrames -= consumeFrames;
        }

        if (consumeFrames == outputFrames)
        {
            Array.Copy(_scratch, _output, outputFrames);
            return true;
        }

        double scale = (consumeFrames - 1.0) / (outputFrames - 1.0);
        for (int frame = 0; frame < outputFrames; ++frame)
        {
            double position = frame * scale;
            int leftIndex = Math.Min(consumeFrames - 1, (int)position);
            int rightIndex = Math.Min(consumeFrames - 1, leftIndex + 1);
            float fraction = (float)(position - leftIndex);
            float left = _scratch[leftIndex];
            float right = _scratch[rightIndex];
            _output[frame] = left + (right - left) * fraction;
        }
        return true;
    }

    private string DescribeDrift()
    {
        string source = Volatile.Read(ref _lastConsumeFrames) switch
        {
            < OutputBlockFrames => "src-slow",
            > OutputBlockFrames => "src-fast",
            _ => "src-neutral"
        };
        string sink = Volatile.Read(ref _lastPushFrames) switch
        {
            < OutputBlockFrames => "sink-slow",
            > OutputBlockFrames => "sink-fast",
            _ => "sink-neutral"
        };
        return $"{source} / {sink}";
    }

    private void FailSafeRouteToWindows()
    {
        Volatile.Write(ref _routeRequested, 0);
        Volatile.Write(ref _routeEnabled, 0);
        Volatile.Write(ref _switchPending, 0);
        Volatile.Write(ref _started, 0);
        try { _engine.SetInputSourceMode(RemoteInputSourceMode.Windows); } catch { }
        ResetManagedBuffer();
    }

    private void WriteSampleLocked(float sample)
    {
        if (_fillFrames == _jitter.Length)
        {
            _readIndex = (_readIndex + 1) % _jitter.Length;
            --_fillFrames;
            Interlocked.Increment(ref _droppedFrames);
        }

        _jitter[_writeIndex] = Math.Clamp(float.IsFinite(sample) ? sample : 0.0F, -1.0F, 1.0F);
        _writeIndex = (_writeIndex + 1) % _jitter.Length;
        ++_fillFrames;
    }

    private static float DownmixFrame(short[] pcm, int frame, int channelCount)
    {
        int offset = frame * channelCount;
        double sum = 0.0;
        for (int channel = 0; channel < channelCount; ++channel)
            sum += pcm[offset + channel] / 32768.0;
        return (float)(sum / channelCount);
    }

    private void ResetManagedBuffer()
    {
        lock (_bufferGate)
        {
            _readIndex = 0;
            _writeIndex = 0;
            _fillFrames = 0;
        }
        Volatile.Write(ref _started, 0);
        Volatile.Write(ref _lastConsumeFrames, OutputBlockFrames);
        Volatile.Write(ref _lastPushFrames, OutputBlockFrames);
    }

    private void ResetManagedDiagnostics()
    {
        Interlocked.Exchange(ref _droppedFrames, 0L);
        Interlocked.Exchange(ref _bridgeUnderruns, 0L);
        Interlocked.Exchange(ref _nativeShortWrites, 0L);
    }

    public async ValueTask DisposeAsync()
    {
        try { SetRouteEnabled(false); } catch { }
        _cts.Cancel();
        try { await _pumpTask; } catch (OperationCanceledException) { }
        _cts.Dispose();
    }
}

internal readonly record struct RemotePhoneMicBridgeStatistics(
    bool RouteRequested,
    bool Routed,
    int JitterFillFrames,
    int JitterTargetFrames,
    long DroppedFrames,
    long BridgeUnderruns,
    long NativeShortWrites,
    string DriftCorrection,
    bool NativeAvailable,
    uint NativeRequestedSourceMode,
    uint NativeActiveSourceMode,
    uint NativeFillFrames,
    uint NativeCapacityFrames,
    ulong NativePushedFrames,
    ulong NativeConsumedFrames,
    ulong NativeUnderrunFrames,
    ulong NativeOverrunFrames);
#endif
