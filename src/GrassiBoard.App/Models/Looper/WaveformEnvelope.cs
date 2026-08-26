namespace GrassiBoard.Models.Looper;

internal sealed record WaveformEnvelope(float[] Minimum, float[] Maximum)
{
    public static WaveformEnvelope Empty { get; } = new([], []);

    public int Count => Math.Min(Minimum.Length, Maximum.Length);
}
