using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrassiBoard.Shared;

public sealed record BuildInfo
{
    public static readonly string CurrentVersion = "0.1.0";

    [JsonPropertyName("Version")]
    public string Version { get; init; } = CurrentVersion;

    [JsonPropertyName("CommitSha")]
    public string CommitSha { get; init; } = "local";

    [JsonPropertyName("BuildDate")]
    public string BuildDate { get; init; } = "local";

    [JsonPropertyName("WorkflowRunNumber")]
    public string WorkflowRunNumber { get; init; } = "local";

    [JsonPropertyName("Configuration")]
    public string Configuration { get; init; } = "Debug";

    [JsonPropertyName("TargetArchitecture")]
    public string TargetArchitecture { get; init; } = "x64";

    [JsonPropertyName("WdkVersion")]
    public string WdkVersion { get; init; } = "10.0.28000.1839";

    [JsonPropertyName("SdkVersion")]
    public string SdkVersion { get; init; } = "10.0.26100.0";

    [JsonPropertyName("DotNetVersion")]
    public string DotNetVersion { get; init; } = "8.0.423";

    [JsonPropertyName("PitchBackendVersion")]
    public string PitchBackendVersion { get; init; } = "not-implemented";

    public string ShortCommit => CommitSha.Length > 8 ? CommitSha[..8] : CommitSha;

    public static BuildInfo Load(string path)
    {
        if (!File.Exists(path))
        {
            return new BuildInfo();
        }

        using FileStream stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<BuildInfo>(stream) ?? new BuildInfo();
    }
}
