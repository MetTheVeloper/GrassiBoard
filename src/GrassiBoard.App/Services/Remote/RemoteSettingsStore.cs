using System.IO;
using System.Text.Json;

namespace GrassiBoard.Services.Remote;

internal sealed class RemoteSettingsDocument
{
    public int SchemaVersion { get; set; } = 1;
    public bool Enabled { get; set; }
    public int Port { get; set; } = 47918;
    public List<RemotePairedClientRecord> Clients { get; set; } = [];
}

internal sealed class RemotePairedClientRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Remote device";
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSeenAt { get; set; }
}

internal sealed class RemoteSettingsStore
{
    private readonly string _path;
    private readonly object _gate = new();

    public RemoteSettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GrassiBoard",
            "remote-settings.json");
    }

    public RemoteSettingsDocument Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_path)) return new RemoteSettingsDocument();
            try
            {
                RemoteSettingsDocument? value = JsonSerializer.Deserialize<RemoteSettingsDocument>(
                    File.ReadAllText(_path), RemoteProtocol.JsonOptions);
                if (value is null) return new RemoteSettingsDocument();
                value.Port = value.Port is >= 1024 and <= 65535 ? value.Port : 47918;
                value.Clients ??= [];
                value.Clients.RemoveAll(client => client.Id == Guid.Empty || string.IsNullOrWhiteSpace(client.TokenHash));
                foreach (RemotePairedClientRecord client in value.Clients)
                {
                    client.Name = string.IsNullOrWhiteSpace(client.Name) ? "Remote device" : client.Name.Trim();
                }
                return value;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                return new RemoteSettingsDocument();
            }
        }
    }

    public void Save(RemoteSettingsDocument document)
    {
        lock (_gate)
        {
            string? directory = Path.GetDirectoryName(_path);
            if (directory is not null) Directory.CreateDirectory(directory);
            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(document, RemoteProtocol.JsonOptions));
            File.Move(temporary, _path, true);
        }
    }
}
