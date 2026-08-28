using System.Runtime.CompilerServices;
using GrassiBoard.Models.Looper;
using GrassiBoard.Services.Looper;

internal static class LooperGate4Smoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        // Deterministic reference contract from the roadmap: loop=8, input=12.
        // To keep this test tiny, one "frame" is represented by a duplicated stereo value.
        float[] input = new float[24];
        for (int frame = 0; frame < 12; frame++)
        {
            input[frame * 2] = frame;
            input[frame * 2 + 1] = frame;
        }

        float[] replaced = LooperLayerComposer.Compose(
            LooperLayerRecordMode.LoopReplace,
            new float[8],
            input,
            8);
        float[] expectedReplace = [8, 9, 10, 11, 4, 5, 6, 7];
        if (!replaced.SequenceEqual(expectedReplace))
        {
            throw new InvalidOperationException("Gate 4 circular Replace 12→8 reference behavior failed.");
        }

        float[] oneCycle = LooperLayerComposer.Compose(
            LooperLayerRecordMode.OneCycle,
            new float[8],
            input,
            8);
        if (!oneCycle.SequenceEqual(new float[] { 0, 1, 2, 3, 4, 5, 6, 7 }))
        {
            throw new InvalidOperationException("Gate 4 One Cycle must keep exactly one Master-length pass.");
        }

        float[] overdub = LooperLayerComposer.Compose(
            LooperLayerRecordMode.Overdub,
            Enumerable.Repeat(1.0F, 8).ToArray(),
            input.Take(16).ToArray(),
            8);
        float[] expectedOverdub = [1, 2, 3, 4, 5, 6, 7, 8];
        if (!overdub.SequenceEqual(expectedOverdub))
        {
            throw new InvalidOperationException("Gate 4 Overdub accumulation contract failed.");
        }

        float[] nonFinite = [float.NaN, float.PositiveInfinity, 0, 0];
        float[] safe = LooperLayerComposer.Compose(
            LooperLayerRecordMode.LoopReplace,
            new float[2],
            nonFinite,
            2);
        if (safe.Any(sample => !float.IsFinite(sample)))
        {
            throw new InvalidOperationException("Gate 4 child layer composition must sanitize non-finite PCM.");
        }

        // Looper local monitoring is deliberately lower-latency than Media Deck:
        // 30 ms WASAPI target + 40 ms native monitor prebuffer = 70 ms before the
        // user's existing shared calibration value is added.
        if (LooperMonitorService.CalculateMonitorPathLatencyFrames(0.0) != 3_360U ||
            LooperMonitorService.CalculateMonitorPathLatencyFrames(25.0) != 4_560U ||
            LooperMonitorService.CalculateMonitorPathLatencyFrames(-100.0) != 0U)
        {
            throw new InvalidOperationException("Gate 4 Looper local-monitor latency/calibration frame calculation failed.");
        }

        if (LooperRecordAlignmentService.CalculateCompensationFrames(120U, 240U, 3_360U) != 3_720U)
        {
            throw new InvalidOperationException("Gate 4 microphone + Pitch + local-monitor compensation sum failed.");
        }

        // A child Take carries capture pre-roll equal to the timing compensation.
        // Removing two frames must make the intended performance frame 2 become
        // project frame 0 while preserving stereo sample order.
        float[] alignmentInput =
        [
            0, 0,
            1, 1,
            2, 2,
            3, 3,
            4, 4,
            5, 5
        ];
        float[] alignedStereo = LooperRecordAlignmentService.RemoveCapturedPreroll(alignmentInput, 2U);
        float[] alignedOneCycle = LooperLayerComposer.Compose(
            LooperLayerRecordMode.OneCycle,
            new float[4],
            alignedStereo,
            4);
        if (!alignedOneCycle.SequenceEqual(new float[] { 2, 3, 4, 5 }))
        {
            throw new InvalidOperationException("Gate 4 compensated child Take must advance captured microphone audio onto the Master clock.");
        }
    }
}
