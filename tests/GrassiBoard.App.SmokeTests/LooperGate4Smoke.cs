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
    }
}
