using GrassiBoard.Shared;
using GrassiBoard.Models;
using GrassiBoard.Services;
using System.Xml.Linq;

if (BuildInfo.CurrentVersion != "0.8.1")
{
    Console.Error.WriteLine("Managed version contract is inconsistent.");
    return 1;
}

string fixture = Path.Combine(AppContext.BaseDirectory, "BuildInfo.fixture.json");
File.WriteAllText(fixture, """
    {
      "Version": "0.8.1",
      "CommitSha": "0123456789abcdef",
      "TargetArchitecture": "x64"
    }
    """);

BuildInfo info = BuildInfo.Load(fixture);
File.Delete(fixture);

if (info.Version != "0.8.1" || info.ShortCommit != "01234567" || info.TargetArchitecture != "x64")
{
    Console.Error.WriteLine("BuildInfo contract smoke test failed.");
    return 2;
}

var captures = new[]
{
    new AudioEndpointDescriptor("vb-capture", "CABLE Output (VB-Audio Virtual Cable)", "{VB}", false),
    new AudioEndpointDescriptor("amm-capture", "Microphone (AMM Virtual Audio Device)", string.Empty, false),
    new AudioEndpointDescriptor("physical-capture", "Headset Microphone (Microsoft LifeChat LX-3000)", "{LIFECHAT}", true)
};

var vbRender = new AudioEndpointDescriptor(
    "vb-render", "CABLE Input (VB-Audio Virtual Cable)", "{VB}", false);
var ammRender = new AudioEndpointDescriptor(
    "amm-render", "Speakers (AMM Virtual Audio Device)", string.Empty, false);
var physicalRender = new AudioEndpointDescriptor(
    "physical-render", "Headset Earphone (Microsoft LifeChat LX-3000)", "{LIFECHAT}", true);
var retiredRender = new AudioEndpointDescriptor(
    "retired-render", "Speakers (GrassiBoard Virtual Audio)", "{GRASSIBOARD}", false);

if (VirtualCableMatcher.FindPairedCaptureEndpoint(vbRender, captures)?.Id != "vb-capture" ||
    VirtualCableMatcher.FindPairedCaptureEndpoint(ammRender, captures)?.Id != "amm-capture" ||
    VirtualCableMatcher.FindPairedCaptureEndpoint(physicalRender, captures) is not null ||
    VirtualCableMatcher.FindPairedCaptureEndpoint(retiredRender, captures) is not null)
{
    Console.Error.WriteLine("External virtual-cable matching contract failed.");
    return 3;
}

string temporaryRoot = Path.Combine(Path.GetTempPath(), $"GrassiBoard-tests-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryRoot);
try
{
    string wavePath = Path.Combine(temporaryRoot, "pad.wav");
    WriteMonoPcmWave(wavePath);
    DecodedAudio decoded = AudioFileDecoder.Decode(wavePath);
    if (decoded.FrameCount != 480U || decoded.Samples.Length != 960 ||
        Math.Abs(decoded.Samples[0] - decoded.Samples[1]) > 0.0001F)
    {
        Console.Error.WriteLine("Offline Sound Pad decode contract failed.");
        return 4;
    }

    string storePath = Path.Combine(temporaryRoot, "soundboard.json");
    var store = new SoundboardStore(storePath);
    var pad = new SoundPadModel
    {
        Title = "Test pad",
        FilePath = wavePath,
        Volume = 0.75,
        Loop = true
    };
    store.Save([pad]);
    SoundPadModel restored = store.Load().Single();
    if (restored.Id != pad.Id || restored.Title != pad.Title ||
        Math.Abs(restored.Volume - 0.75) > 0.0001 || !restored.Loop)
    {
        Console.Error.WriteLine("Sound Pad persistence contract failed.");
        return 5;
    }
}
finally
{
    Directory.Delete(temporaryRoot, true);
}

string[] meterViews =
[
    Path.Combine(Environment.CurrentDirectory, "src", "GrassiBoard.App", "MainWindow.xaml"),
    Path.Combine(Environment.CurrentDirectory, "src", "GrassiBoard.App", "Views", "BoardView.xaml")
];
foreach (string meterView in meterViews)
{
    XDocument document = XDocument.Load(meterView);
    XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    foreach (XElement progressBar in document.Descendants(presentation + "ProgressBar"))
    {
        string binding = progressBar.Attribute("Value")?.Value ?? string.Empty;
        if (binding.Contains("Meter", StringComparison.Ordinal) &&
            !binding.Contains("Mode=OneWay", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Meter binding must be OneWay: {meterView} · {binding}");
            return 6;
        }
    }
}

Console.WriteLine("Managed app, decode, and persistence smoke tests passed.");
return 0;

static void WriteMonoPcmWave(string path)
{
    const int sampleRate = 48_000;
    const short channels = 1;
    const short bitsPerSample = 16;
    const int frameCount = 480;
    int dataBytes = frameCount * channels * bitsPerSample / 8;
    using FileStream stream = File.Create(path);
    using var writer = new BinaryWriter(stream);
    writer.Write(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
    writer.Write(36 + dataBytes);
    writer.Write(new byte[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
    writer.Write(new byte[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
    writer.Write(16);
    writer.Write((short)1);
    writer.Write(channels);
    writer.Write(sampleRate);
    writer.Write(sampleRate * channels * bitsPerSample / 8);
    writer.Write((short)(channels * bitsPerSample / 8));
    writer.Write(bitsPerSample);
    writer.Write(new byte[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
    writer.Write(dataBytes);
    for (int frame = 0; frame < frameCount; ++frame)
    {
        writer.Write((short)(Math.Sin(frame * 2.0 * Math.PI * 440.0 / sampleRate) * 4000.0));
    }
}
