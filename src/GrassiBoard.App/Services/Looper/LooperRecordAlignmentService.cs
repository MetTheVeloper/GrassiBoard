using System.Windows;
using GrassiBoard.Services;
using GrassiBoard.ViewModels;

namespace GrassiBoard.Services.Looper;

internal readonly record struct LooperRecordAlignmentSnapshot(
    uint CompensationFrames,
    uint SourceBufferFrames,
    uint PitchLatencyFrames,
    uint MonitorLatencyFrames,
    double CalibrationMilliseconds,
    LooperRecordSourceMode SourceMode)
{
    public double CompensationMilliseconds => CompensationFrames * 1000.0 / LooperRecordService.SampleRate;

    public static LooperRecordAlignmentSnapshot None(LooperRecordSourceMode sourceMode) =>
        new(0U, 0U, 0U, 0U, 0.0, sourceMode);
}

internal static class LooperRecordAlignmentService
{
    public static bool TryCapture(
        NativeAudioEngine engine,
        out LooperRecordAlignmentSnapshot snapshot,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(engine);
        snapshot = default;
        error = string.Empty;

        if (engine.GetLooperRecordState(out LooperRecordNativeState recordState) != NativeResult.Ok)
        {
            error = "Looper Record Tap source state is unavailable for timing compensation.";
            return false;
        }

        LooperRecordSourceMode sourceMode = (LooperRecordSourceMode)recordState.SourceMode;
        if (engine.GetLooperState(out LooperNativeState looperState) != NativeResult.Ok || looperState.LoopFrames == 0U)
        {
            // A free first-Master recording has no project clock to align against.
            snapshot = LooperRecordAlignmentSnapshot.None(sourceMode);
            return true;
        }

        if (engine.GetStatistics(out AudioStatistics statistics) != NativeResult.Ok || statistics.Running == 0U)
        {
            error = "Audio timing statistics are unavailable for Looper recording alignment.";
            return false;
        }

        ulong sourceBufferFrames = (ulong)statistics.CaptureBufferFrames + statistics.RingBufferFillFrames;
#if REMOTE_MONITOR_SPIKE
        if (sourceMode == LooperRecordSourceMode.Remote)
        {
            if (engine.GetRemoteInputStatistics(out RemoteInputStatistics remoteState) != NativeResult.Ok)
            {
                error = "Phone Mic timing state is unavailable for Looper recording alignment.";
                return false;
            }
            sourceBufferFrames = remoteState.FillFrames;
        }
#endif

        double calibrationMilliseconds = ResolveSharedCalibrationMilliseconds();
        uint monitorLatencyFrames = LooperMonitorService.CalculateMonitorPathLatencyFrames(calibrationMilliseconds);
        uint compensationFrames = CalculateCompensationFrames(
            sourceBufferFrames,
            statistics.PitchLatencySamples,
            monitorLatencyFrames);

        snapshot = new LooperRecordAlignmentSnapshot(
            compensationFrames,
            checked((uint)Math.Min(sourceBufferFrames, uint.MaxValue)),
            statistics.PitchLatencySamples,
            monitorLatencyFrames,
            calibrationMilliseconds,
            sourceMode);
        return true;
    }

    internal static uint CalculateCompensationFrames(
        ulong sourceBufferFrames,
        uint pitchLatencyFrames,
        uint monitorLatencyFrames)
    {
        ulong total = sourceBufferFrames + pitchLatencyFrames + monitorLatencyFrames;
        return checked((uint)Math.Min(total, uint.MaxValue));
    }

    internal static float[] RemoveCapturedPreroll(float[] interleavedStereo, uint compensationFrames)
    {
        ArgumentNullException.ThrowIfNull(interleavedStereo);
        int channels = LooperRecordService.Channels;
        int completeSamples = interleavedStereo.Length - interleavedStereo.Length % channels;
        long frameCount = completeSamples / channels;
        if (frameCount == 0L || compensationFrames == 0U)
        {
            if (completeSamples == interleavedStereo.Length) return interleavedStereo;
            return interleavedStereo[..completeSamples];
        }
        if (compensationFrames >= (ulong)frameCount) return [];

        int sourceSample = checked((int)compensationFrames * channels);
        int resultSamples = completeSamples - sourceSample;
        var result = new float[resultSamples];
        Array.Copy(interleavedStereo, sourceSample, result, 0, resultSamples);
        return result;
    }

    private static double ResolveSharedCalibrationMilliseconds()
    {
        // This intentionally reuses the existing persisted Media Sync Calibration.
        // No Looper-only calibration preference is introduced. StartAsync is called
        // from the desktop UI thread, so the active profile value is read directly
        // and snapshotted for the full Take.
        if (Application.Current?.MainWindow?.DataContext is MainViewModel mainViewModel)
        {
            double value = mainViewModel.MediaSyncOffsetMilliseconds;
            return Math.Round(Math.Clamp(double.IsFinite(value) ? value : 0.0, -100.0, 100.0));
        }
        return 0.0;
    }
}
