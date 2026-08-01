using GrassiBoard.Shared;

if (BuildInfo.CurrentVersion != "0.5.0")
{
    Console.Error.WriteLine("Managed version contract is inconsistent.");
    return 1;
}

string fixture = Path.Combine(AppContext.BaseDirectory, "BuildInfo.fixture.json");
File.WriteAllText(fixture, """
    {
      "Version": "0.5.0",
      "CommitSha": "0123456789abcdef",
      "TargetArchitecture": "x64"
    }
    """);

BuildInfo info = BuildInfo.Load(fixture);
File.Delete(fixture);

if (info.Version != "0.5.0" || info.ShortCommit != "01234567" || info.TargetArchitecture != "x64")
{
    Console.Error.WriteLine("BuildInfo contract smoke test failed.");
    return 2;
}

Console.WriteLine("Managed BuildInfo smoke test passed.");
return 0;
