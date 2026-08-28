using System.Windows.Media;
using GrassiBoard.Infrastructure;

namespace GrassiBoard.Models.Looper;

internal sealed class LooperTrackModel : ObservableObject
{
    private string _name = "Track";
    private Color _displayColor = Color.FromRgb(113, 172, 255);
    private WaveformEnvelope _waveform = WaveformEnvelope.Empty;
    private float[] _samples = [];
    private long _activeStartFrame;
    private long _activeEndFrame;
    private double _gain = 1.0;
    private double _pan;
    private bool _muted;
    private bool _solo;
    private bool _hasRecordedAudio;
    private LooperLayerRecordMode _recordMode = LooperLayerRecordMode.OneCycle;

    public Guid Id { get; init; } = Guid.NewGuid();
    public uint NativeTrackId { get; init; }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public Color DisplayColor { get => _displayColor; set => SetProperty(ref _displayColor, value); }
    public WaveformEnvelope Waveform { get => _waveform; set => SetProperty(ref _waveform, value); }
    public float[] Samples { get => _samples; set => SetProperty(ref _samples, value); }
    public long ActiveStartFrame { get => _activeStartFrame; set => SetProperty(ref _activeStartFrame, value); }
    public long ActiveEndFrame { get => _activeEndFrame; set => SetProperty(ref _activeEndFrame, value); }
    public double Gain { get => _gain; set => SetProperty(ref _gain, Math.Clamp(double.IsFinite(value) ? value : 1.0, 0.0, 4.0)); }
    public double Pan { get => _pan; set => SetProperty(ref _pan, Math.Clamp(double.IsFinite(value) ? value : 0.0, -1.0, 1.0)); }
    public bool Muted { get => _muted; set => SetProperty(ref _muted, value); }
    public bool Solo { get => _solo; set => SetProperty(ref _solo, value); }
    public bool HasRecordedAudio { get => _hasRecordedAudio; set => SetProperty(ref _hasRecordedAudio, value); }
    public LooperLayerRecordMode RecordMode { get => _recordMode; set => SetProperty(ref _recordMode, value); }

    internal float[]? UndoSamples { get; set; }
}
