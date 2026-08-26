using System.Runtime.CompilerServices;
using System.Xml.Linq;
using GrassiBoard.Models.Looper;
using GrassiBoard.Services.Looper;

internal static class LooperGate1Smoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyWebRtcDependencyBaseline();

        var analysis = new WaveformAnalysisService();
        float[] synthetic =
        [
            -1.0F, -0.5F,
             0.25F, 0.5F,
            -0.25F, 0.75F,
             0.0F, 1.0F
        ];
        WaveformEnvelope envelope = analysis.BuildEnvelope(synthetic, 2, 2);
        if (envelope.Count != 2 || envelope.Minimum[0] > -0.99F || envelope.Maximum[1] < 0.99F)
        {
            throw new InvalidOperationException("Gate 1 waveform envelope contract failed.");
        }

        string wavePath = Path.Combine(Path.GetTempPath(), $"grassilooper-gate1-{Guid.NewGuid():N}.wav");
        try
        {
            WriteMonoPcmWave(wavePath, 480);
            WaveformAnalysisResult imported = analysis.AnalyzeFileAsync(wavePath, 64).GetAwaiter().GetResult();
            if (imported.FrameCount != 480 || imported.Samples.Length != 960 || imported.Envelope.Count == 0)
            {
                throw new InvalidOperationException("Gate 1 imported Master analysis contract failed.");
            }
        }
        finally
        {
            if (File.Exists(wavePath)) File.Delete(wavePath);
        }

        var store = new LooperProjectStore();
        var master = new LooperMasterModel
        {
            FrameCount = 4,
            SourceEndFrame = 4,
            Samples = synthetic,
            Waveform = envelope
        };
        store.SetMaster(master);
        if (store.Current.MasterFrameCount != 4 || !store.Current.CanRedefineMaster)
        {
            throw new InvalidOperationException("Gate 1 project Master contract failed.");
        }
        store.Current.Tracks.Add(new LooperTrackModel { Name = "Child" });
        bool rejected = false;
        try
        {
            store.SetMaster(master);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        if (!rejected)
        {
            throw new InvalidOperationException("Gate 1 must lock Master redefinition after child tracks exist.");
        }
    }

    private static void VerifyWebRtcDependencyBaseline()
    {
        string projectPath = Path.Combine(
            Environment.CurrentDirectory, "src", "GrassiBoard.App", "GrassiBoard.App.csproj");
        XDocument project = XDocument.Load(projectPath);
        XElement[] sipsorceryReferences = project
            .Descendants("PackageReference")
            .Where(element => string.Equals((string?)element.Attribute("Include"), "SIPSorcery", StringComparison.Ordinal))
            .ToArray();
        if (sipsorceryReferences.Length != 1 ||
            !string.Equals((string?)sipsorceryReferences[0].Attribute("Version"), "10.0.15", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Gate 1 requires the real SIPSorcery PackageReference to remain exactly 10.0.15; legacy migration comments do not count.");
        }
    }

    private static void WriteMonoPcmWave(string path, int frames)
    {
        const int sampleRate = 48_000;
        const short channels = 1;
        const short bitsPerSample = 16;
        short blockAlign = (short)(channels * bitsPerSample / 8);
        int byteRate = sampleRate * blockAlign;
        int dataSize = frames * blockAlign;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        for (int frame = 0; frame < frames; frame++)
        {
            double phase = 2.0 * Math.PI * 440.0 * frame / sampleRate;
            writer.Write((short)(Math.Sin(phase) * 12_000.0));
        }
    }
}
