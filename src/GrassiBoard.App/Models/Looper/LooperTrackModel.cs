using System.Windows.Media;

namespace GrassiBoard.Models.Looper;

internal sealed class LooperTrackModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "Track";
    public Color DisplayColor { get; set; } = Color.FromRgb(113, 172, 255);
    public WaveformEnvelope Waveform { get; set; } = WaveformEnvelope.Empty;
    public float[] Samples { get; set; } = [];
    public long ActiveStartFrame { get; set; }
    public long ActiveEndFrame { get; set; }
    public double Gain { get; set; } = 1.0;
    public double Pan { get; set; }
    public bool Muted { get; set; }
    public bool Solo { get; set; }
}
