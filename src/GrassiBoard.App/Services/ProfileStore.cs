using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using GrassiBoard.Models;

namespace GrassiBoard.Services;

internal sealed class ProfileStore
{
    private readonly string _path;

    public ProfileStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GrassiBoard",
            "profiles.json");
    }

    public ProfileDocument Load()
    {
        if (!File.Exists(_path))
        {
            return new ProfileDocument();
        }

        var result = new ProfileDocument();
        try
        {
            using JsonDocument json = JsonDocument.Parse(File.ReadAllText(_path));
            JsonElement root = json.RootElement;
            if (root.TryGetProperty("SchemaVersion", out JsonElement schema) && schema.TryGetInt32(out int version))
            {
                result.SchemaVersion = version;
            }
            if (root.TryGetProperty("ActiveProfileId", out JsonElement active) &&
                Guid.TryParse(active.GetString(), out Guid activeId))
            {
                result.ActiveProfileId = activeId;
            }
            if (root.TryGetProperty("Profiles", out JsonElement profiles) && profiles.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in profiles.EnumerateArray())
                {
                    try
                    {
                        JsonObject? profileNode = JsonNode.Parse(item.GetRawText()) as JsonObject;
                        if (profileNode is null)
                        {
                            continue;
                        }

                        List<JsonNode?> presetNodes = profileNode["UserPresets"] is JsonArray presets
                            ? presets.Select(preset => preset?.DeepClone()).ToList()
                            : [];
                        List<JsonNode?> padNodes = profileNode["Pads"] is JsonArray pads
                            ? pads.Select(pad => pad?.DeepClone()).ToList()
                            : [];
                        profileNode["UserPresets"] = new JsonArray();
                        profileNode["Pads"] = new JsonArray();
                        ProfileModel? profile = profileNode.Deserialize<ProfileModel>(JsonOptions);
                        if (profile is not null && profile.Id != Guid.Empty)
                        {
                            profile.Pads = DeserializeItems<SoundPadModel>(padNodes);
                            profile.UserPresets = DeserializeItems<UserPresetModel>(presetNodes);
                            profile.Preferences ??= new AppPreferences();
                            profile.AudioState ??= new AudioStateSnapshot();
                            Normalize(profile);
                            result.Profiles.Add(profile);
                        }
                    }
                    catch (JsonException)
                    {
                        // One malformed profile must not discard the remaining valid profiles.
                    }
                }
            }
        }
        catch (JsonException)
        {
            return new ProfileDocument();
        }
        catch (IOException)
        {
            return new ProfileDocument();
        }
        return result;
    }

    private static List<T> DeserializeItems<T>(IEnumerable<JsonNode?> nodes) where T : class
    {
        var result = new List<T>();
        foreach (JsonNode? node in nodes)
        {
            try
            {
                T? item = node is null ? null : node.Deserialize<T>(JsonOptions);
                if (item is not null)
                {
                    result.Add(item);
                }
            }
            catch (JsonException)
            {
                // A malformed pad or preset is isolated from its siblings.
            }
        }
        return result;
    }

    private static void Normalize(ProfileModel profile)
    {
        AppPreferences preferences = profile.Preferences;
        preferences.MuteHotkey ??= string.Empty;
        preferences.StopAllHotkey ??= string.Empty;
        preferences.VoiceFxHotkey ??= string.Empty;
        preferences.PushToTalkHotkey ??= string.Empty;
        preferences.ShowHideHotkey ??= string.Empty;
        preferences.MediaPlayPauseHotkey ??= string.Empty;
        preferences.MediaStopHotkey ??= string.Empty;
        preferences.MediaBackHotkey ??= string.Empty;
        preferences.MediaForwardHotkey ??= string.Empty;
        preferences.LastMediaPath ??= string.Empty;
        foreach (UserPresetModel preset in profile.UserPresets)
        {
            preset.State ??= new AudioStateSnapshot();
        }
    }

    public void Save(ProfileDocument document)
    {
        string? directory = Path.GetDirectoryName(_path);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }
        string temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(document, JsonOptions));
        File.Move(temporary, _path, true);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
