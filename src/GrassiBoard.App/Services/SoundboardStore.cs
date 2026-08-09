using System.IO;
using System.Text.Json;
using GrassiBoard.Models;

namespace GrassiBoard.Services;

internal sealed class SoundboardStore
{
    private readonly string _path;

    public SoundboardStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GrassiBoard",
            "soundboard.json");
    }

    public IReadOnlyList<SoundPadModel> Load()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            using FileStream stream = File.OpenRead(_path);
            return JsonSerializer.Deserialize<List<SoundPadModel>>(stream, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    public void Save(IEnumerable<SoundPadModel> pads)
    {
        string? directory = Path.GetDirectoryName(_path);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        string temporary = _path + ".tmp";
        using (FileStream stream = File.Create(temporary))
        {
            JsonSerializer.Serialize(stream, pads, JsonOptions);
        }
        File.Move(temporary, _path, true);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
