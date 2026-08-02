using GrassiBoard.Shared;

if (BuildInfo.CurrentVersion != "0.7.0")
{
    Console.Error.WriteLine("Managed version contract is inconsistent.");
    return 1;
}

string fixture = Path.Combine(AppContext.BaseDirectory, "BuildInfo.fixture.json");
File.WriteAllText(fixture, """
    {
      "Version": "0.7.0",
      "CommitSha": "0123456789abcdef",
      "TargetArchitecture": "x64"
    }
    """);

BuildInfo info = BuildInfo.Load(fixture);
File.Delete(fixture);

if (info.Version != "0.7.0" || info.ShortCommit != "01234567" || info.TargetArchitecture != "x64")
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

Console.WriteLine("Managed BuildInfo smoke test passed.");
return 0;
