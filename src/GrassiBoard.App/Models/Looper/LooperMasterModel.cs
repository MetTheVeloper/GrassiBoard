using System.Windows.Media;

namespace GrassiBoard.Models.Looper;

internal enum LooperMasterSourceType
{
    Imported,
    Microphone
}

internal sealed class LooperMasterModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "Master Loop";
    public LooperMasterSourceType SourceType { get; init; } = LooperMasterSourceType.Imported;
    public string SourceFileName { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public int SampleRate { get; init; } = 48_000;
    public int Channels { get; init; } = 2;
    public long SourceStartFrame { get; init; }
    public long SourceEndFrame { get; init; }
    public long FrameCount { get; init; }
    public float[] SourceSamples { get; init; } = [];
    public float[] Samples { get; init; } = [];
    public WaveformEnvelope Waveform { get; init; } = WaveformEnvelope.Empty;
    public Color DisplayColor { get; init; } = Color.FromRgb(86, 211, 163);
    public TimeSpan Duration => TimeSpan.FromSeconds(FrameCount / (double)SampleRate);
    public string SourceTypeLabel => SourceType == LooperMasterSourceType.Microphone ? "Microphone" : "Imported audio";
}
