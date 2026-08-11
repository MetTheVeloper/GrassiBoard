using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrassiBoard.Services.Remote;

internal static class RemoteProtocol
{
    public const int Version = 1;
    public const int MaxMessageBytes = 64 * 1024;

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}

internal sealed class RemoteIncomingEnvelope
{
    public int ProtocolVersion { get; set; }
    public string Type { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public JsonElement Payload { get; set; }
}

internal sealed record RemotePairRequest(string? Secret, string? Code, string? DeviceName);
internal sealed record RemotePairResponse(string ClientId, string ClientToken, string DeviceName);
internal sealed record RemotePairingInfo(string Url, string Code, DateTimeOffset ExpiresAt);

internal sealed record RemoteClientDisplay(Guid Id, string Name, bool Connected, DateTimeOffset CreatedAt, DateTimeOffset? LastSeenAt)
{
    public string StatusLabel => Connected ? "Connected" : "Paired";
}

internal sealed record RemotePadSnapshot(
    Guid Id,
    string Title,
    string State,
    bool Ready,
    bool Playing,
    bool Loop,
    bool HasError);

internal sealed record RemotePresetSnapshot(Guid Id, string Name);

internal sealed record RemoteEngineSnapshot(
    bool NativeReady,
    bool Running,
    bool Busy,
    string State,
    string Status);

internal sealed record RemoteVoiceSnapshot(
    bool Enabled,
    double Pitch,
    double FinePitch,
    double Formant,
    bool PreserveVocalCharacter);

internal sealed record RemoteMixerSnapshot(
    double MicGain,
    double SoundboardGain,
    double MasterGain);

internal sealed record RemoteMediaSnapshot(
    bool HasMedia,
    string DisplayName,
    bool Playing,
    double Position,
    double Duration,
    double Volume,
    bool MonitorEnabled,
    bool SendEnabled,
    bool HasError);

internal sealed record RemoteMeterSnapshot(
    double Microphone,
    double Soundboard,
    double Master,
    string MicrophoneDb,
    string SoundboardDb,
    string MasterDb);

internal sealed record RemoteStateSnapshot(
    long Revision,
    string ProfileName,
    RemoteEngineSnapshot Engine,
    bool MicrophoneMuted,
    RemoteVoiceSnapshot Voice,
    RemoteMixerSnapshot Mixer,
    RemoteMediaSnapshot Media,
    RemoteMeterSnapshot Meters,
    IReadOnlyList<RemotePadSnapshot> Pads,
    IReadOnlyList<RemotePresetSnapshot> Presets);

internal sealed record RemoteCommandResult(bool Success, string? ErrorCode = null, string? ErrorMessage = null)
{
    public static RemoteCommandResult Ok() => new(true);
    public static RemoteCommandResult Fail(string code, string message) => new(false, code, message);
}
