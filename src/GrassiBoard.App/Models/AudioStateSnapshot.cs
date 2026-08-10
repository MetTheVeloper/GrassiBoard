namespace GrassiBoard.Models;

internal sealed class AudioStateSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public bool VoiceFxEnabled { get; set; }
    public double Pitch { get; set; }
    public double FinePitch { get; set; }
    public double Formant { get; set; }
    public bool PreserveVocalCharacter { get; set; } = true;
    public int QualityIndex { get; set; } = 1;
    public double MicGain { get; set; }
    public double SoundboardGain { get; set; }
    public double MasterGain { get; set; }
    public bool NoiseGateEnabled { get; set; }
    public double GateThreshold { get; set; } = -55.0;
    public bool CompressorEnabled { get; set; }
    public double CompressorThreshold { get; set; } = -18.0;
    public double CompressorRatio { get; set; } = 3.0;
    public bool LimiterEnabled { get; set; } = true;
    public double LimiterCeiling { get; set; } = -1.0;
    public bool DuckingEnabled { get; set; }
    public double DuckingAmount { get; set; } = 9.0;
    public bool ClippingProtectionEnabled { get; set; } = true;
    public double PitchWetMix { get; set; } = 100.0;

    public AudioStateSnapshot Clone() => (AudioStateSnapshot)MemberwiseClone();
}
