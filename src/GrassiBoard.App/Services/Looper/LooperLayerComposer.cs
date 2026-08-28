using GrassiBoard.Models.Looper;

namespace GrassiBoard.Services.Looper;

internal static class LooperLayerComposer
{
    public static float[] Compose(
        LooperLayerRecordMode mode,
        float[] existingMono,
        float[] recordedStereo,
        long loopFrames)
    {
        ArgumentNullException.ThrowIfNull(existingMono);
        ArgumentNullException.ThrowIfNull(recordedStereo);
        if (loopFrames <= 0L || loopFrames > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(loopFrames));
        }

        int loopLength = checked((int)loopFrames);
        float[] result = mode == LooperLayerRecordMode.OneCycle
            ? new float[loopLength]
            : NormalizeExisting(existingMono, loopLength);

        int recordedFrames = recordedStereo.Length / LooperRecordService.Channels;
        int framesToApply = mode == LooperLayerRecordMode.OneCycle
            ? Math.Min(loopLength, recordedFrames)
            : recordedFrames;

        for (int frame = 0; frame < framesToApply; frame++)
        {
            int stereoOffset = frame * LooperRecordService.Channels;
            float left = Finite(recordedStereo[stereoOffset]);
            float right = Finite(recordedStereo[stereoOffset + 1]);
            float mono = Finite((left + right) * 0.5F);
            int destination = frame % loopLength;

            if (mode == LooperLayerRecordMode.Overdub)
            {
                result[destination] = Finite(result[destination] + mono);
            }
            else
            {
                result[destination] = mono;
            }
        }

        return result;
    }

    private static float[] NormalizeExisting(float[] source, int loopLength)
    {
        var result = new float[loopLength];
        int copy = Math.Min(source.Length, loopLength);
        for (int index = 0; index < copy; index++) result[index] = Finite(source[index]);
        return result;
    }

    private static float Finite(float sample) => float.IsFinite(sample) ? sample : 0.0F;
}
