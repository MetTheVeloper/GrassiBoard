using GrassiBoard.Services;

namespace GrassiBoard.Services.Looper;

internal sealed record LooperRecordedTake(
    float[] StereoSamples,
    long FrameCount,
    LooperRecordSourceMode SourceMode,
    ulong OverrunFrames,
    uint AlignmentFrames,
    double AlignmentMilliseconds,
    double SyncCalibrationMilliseconds);

internal sealed class LooperRecordService : IDisposable
{
    public const int SampleRate = 48_000;
    public const int Channels = 2;
    public const int MaxCaptureMinutes = 10;
    public const long MaxCaptureFrames = (long)SampleRate * 60L * MaxCaptureMinutes;

    private readonly object _sync = new();
    private NativeAudioEngine? _engine;
    private CancellationTokenSource? _workerCancellation;
    private Task? _worker;
    private List<float>? _samples;
    private long _drainedFrames;
    private LooperRecordAlignmentSnapshot _alignmentSnapshot;
    private bool _recording;
    private bool _disposed;
    private string _lastError = string.Empty;

    public bool IsRecording
    {
        get { lock (_sync) return _recording; }
    }

    public string LastError
    {
        get { lock (_sync) return _lastError; }
    }

    public uint AlignmentFrames
    {
        get { lock (_sync) return _alignmentSnapshot.CompensationFrames; }
    }

    public double AlignmentMilliseconds
    {
        get { lock (_sync) return _alignmentSnapshot.CompensationMilliseconds; }
    }

    public bool TryGetState(out LooperRecordNativeState state)
    {
        state = default;
        NativeAudioEngine? engine;
        long drainedFrames;
        uint compensationFrames;
        lock (_sync)
        {
            engine = _engine ?? NativeAudioEngine.FindRunningProcessEngine();
            drainedFrames = _drainedFrames;
            compensationFrames = _alignmentSnapshot.CompensationFrames;
        }
        if (engine is null || engine.GetLooperRecordState(out state) != NativeResult.Ok)
        {
            return false;
        }

        // Native reports the raw Record Tap position. For an aligned child Take,
        // the first compensationFrames are deliberate capture pre-roll: the user
        // is hearing the Master through the local output path while the microphone
        // also traverses source buffering + Voice/Pitch processing. Expose the
        // post-compensation project duration so One Cycle records the required tail
        // instead of stopping before the last aligned samples arrive.
        ulong rawCapturedFrames = checked((ulong)Math.Max(0L, drainedFrames) + state.FillFrames);
        state.CapturedFrames = rawCapturedFrames > compensationFrames
            ? rawCapturedFrames - compensationFrames
            : 0U;
        return true;
    }

    public Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        lock (_sync)
        {
            if (_recording) return Task.FromResult(true);
            _engine = NativeAudioEngine.FindRunningProcessEngine();
            if (_engine is null)
            {
                _lastError = "Start the GrassiBoard audio engine before recording a Looper Master.";
                return Task.FromResult(false);
            }

            NativeResult result = _engine.StartLooperRecord();
            if (result != NativeResult.Ok)
            {
                _lastError = $"Looper Record Tap could not start ({result}).";
                return Task.FromResult(false);
            }

            if (!LooperRecordAlignmentService.TryCapture(_engine, out _alignmentSnapshot, out string alignmentError))
            {
                _engine.StopLooperRecord();
                _alignmentSnapshot = default;
                _lastError = alignmentError;
                return Task.FromResult(false);
            }

            _samples = new List<float>(SampleRate * Channels * 8);
            _drainedFrames = 0L;
            _workerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _recording = true;
            _lastError = string.Empty;
            _worker = Task.Run(() => DrainLoopAsync(_workerCancellation.Token), CancellationToken.None);
            return Task.FromResult(true);
        }
    }

    public async Task<LooperRecordedTake?> StopAsync()
    {
        ThrowIfDisposed();
        Task? worker;
        NativeAudioEngine? engine;
        lock (_sync)
        {
            if (!_recording || _engine is null) return null;
            engine = _engine;
            engine.StopLooperRecord();
            worker = _worker;
        }

        if (worker is not null)
        {
            try { await worker.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        lock (_sync)
        {
            _recording = false;
            _worker = null;
            _workerCancellation?.Dispose();
            _workerCancellation = null;

            if (engine.GetStatistics(out AudioStatistics audioState) != NativeResult.Ok || audioState.Running == 0U)
            {
                _lastError = "The GrassiBoard audio engine stopped during the Take. The partial Take was discarded safely.";
                _samples = null;
                _alignmentSnapshot = default;
                return null;
            }
            if (engine.GetLooperRecordState(out LooperRecordNativeState state) != NativeResult.Ok)
            {
                _lastError = "Looper Record Tap state could not be read after Stop.";
                _samples = null;
                _alignmentSnapshot = default;
                return null;
            }
            if (state.SourceChanged != 0U)
            {
                _lastError = "Microphone source changed during the Take. The Take was discarded instead of combining two inputs.";
                _samples = null;
                _alignmentSnapshot = default;
                return null;
            }
            if (state.OverrunFrames != 0U)
            {
                _lastError = $"Looper Record Tap overran by {state.OverrunFrames:N0} frames. The Take was discarded.";
                _samples = null;
                _alignmentSnapshot = default;
                return null;
            }
            if (_samples is null || _samples.Count < Channels)
            {
                _lastError = "The microphone Take contained no audio frames.";
                _samples = null;
                _alignmentSnapshot = default;
                return null;
            }

            int completeSamples = _samples.Count - _samples.Count % Channels;
            if (completeSamples != _samples.Count)
            {
                _samples.RemoveRange(completeSamples, _samples.Count - completeSamples);
            }

            LooperRecordAlignmentSnapshot alignment = _alignmentSnapshot;
            float[] rawSamples = _samples.ToArray();
            _samples = null;
            float[] samples = LooperRecordAlignmentService.RemoveCapturedPreroll(
                rawSamples,
                alignment.CompensationFrames);
            if (samples.Length < Channels)
            {
                _lastError = alignment.CompensationFrames == 0U
                    ? "The microphone Take contained no aligned audio frames."
                    : $"The Take ended before its {alignment.CompensationMilliseconds:0.0} ms sync compensation completed; it was discarded.";
                _alignmentSnapshot = default;
                return null;
            }

            long frames = samples.LongLength / Channels;
            _alignmentSnapshot = default;
            _lastError = string.Empty;
            return new LooperRecordedTake(
                samples,
                frames,
                (LooperRecordSourceMode)state.SourceMode,
                state.OverrunFrames,
                alignment.CompensationFrames,
                alignment.CompensationMilliseconds,
                alignment.CalibrationMilliseconds);
        }
    }

    public void Cancel()
    {
        if (_disposed) return;
        Task? worker;
        lock (_sync)
        {
            _engine?.StopLooperRecord();
            _workerCancellation?.Cancel();
            worker = _worker;
            _recording = false;
            _samples = null;
            _worker = null;
            _drainedFrames = 0L;
            _alignmentSnapshot = default;
        }
        try { worker?.GetAwaiter().GetResult(); } catch { }
        lock (_sync)
        {
            _workerCancellation?.Dispose();
            _workerCancellation = null;
        }
    }

    private async Task DrainLoopAsync(CancellationToken cancellationToken)
    {
        float[] buffer = new float[8_192];
        while (!cancellationToken.IsCancellationRequested)
        {
            NativeAudioEngine? engine;
            lock (_sync) engine = _engine;
            if (engine is null) return;

            NativeResult stateResult = engine.GetLooperRecordState(out LooperRecordNativeState state);
            if (stateResult != NativeResult.Ok) return;

            bool readAnything = false;
            do
            {
                NativeResult readResult = engine.ReadLooperRecord(buffer, (uint)(buffer.Length / Channels), out uint readFrames);
                if (readResult != NativeResult.Ok) return;
                if (readFrames == 0U) break;
                readAnything = true;
                lock (_sync)
                {
                    if (_samples is null) return;
                    long currentFrames = _samples.Count / Channels;
                    long acceptedFrames = Math.Min(readFrames, Math.Max(0L, MaxCaptureFrames - currentFrames));
                    int acceptedSamples = checked((int)acceptedFrames * Channels);
                    for (int index = 0; index < acceptedSamples; index++)
                    {
                        float sample = buffer[index];
                        _samples.Add(float.IsFinite(sample) ? sample : 0.0F);
                    }
                    _drainedFrames += acceptedFrames;
                    if (currentFrames + acceptedFrames >= MaxCaptureFrames)
                    {
                        engine.StopLooperRecord();
                    }
                }
            }
            while (true);

            if (state.SourceChanged != 0U) return;
            if (state.Active == 0U && state.FillFrames == 0U) return;
            if (!readAnything) await Task.Delay(4, cancellationToken).ConfigureAwait(false);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LooperRecordService));
    }

    public void Dispose()
    {
        if (_disposed) return;
        Cancel();
        _disposed = true;
        _engine = null;
    }
}
