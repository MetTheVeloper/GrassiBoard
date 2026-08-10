using GrassiBoard.Infrastructure;

namespace GrassiBoard.Models;

internal sealed class ProfileModel : ObservableObject
{
    private string _name = "Default";

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, string.IsNullOrWhiteSpace(value) ? "Untitled profile" : value.Trim());
    }

    public string InputDeviceId { get; set; } = string.Empty;
    public string OutputDeviceId { get; set; } = string.Empty;
    public string MonitorDeviceId { get; set; } = string.Empty;
    public AudioStateSnapshot AudioState { get; set; } = new();
    public List<SoundPadModel> Pads { get; set; } = [];
    public List<UserPresetModel> UserPresets { get; set; } = [];
    public AppPreferences Preferences { get; set; } = new();

    public ProfileModel Clone(string? name = null) => new()
    {
        Name = name ?? $"{Name} copy",
        InputDeviceId = InputDeviceId,
        OutputDeviceId = OutputDeviceId,
        MonitorDeviceId = MonitorDeviceId,
        AudioState = AudioState.Clone(),
        Pads = Pads.Select(pad => new SoundPadModel
        {
            Id = Guid.NewGuid(),
            Title = pad.Title,
            FilePath = pad.FilePath,
            Volume = pad.Volume,
            Loop = pad.Loop,
            RestartOnPress = pad.RestartOnPress,
            Hotkey = pad.Hotkey
        }).ToList(),
        UserPresets = UserPresets.Select(preset => new UserPresetModel
        {
            Id = Guid.NewGuid(),
            Name = preset.Name,
            Hotkey = preset.Hotkey,
            State = preset.State.Clone()
        }).ToList(),
        Preferences = new AppPreferences
        {
            MinimizeToTray = Preferences.MinimizeToTray,
            StartMinimized = Preferences.StartMinimized,
            StartWithWindows = Preferences.StartWithWindows,
            MuteHotkey = Preferences.MuteHotkey,
            StopAllHotkey = Preferences.StopAllHotkey,
            VoiceFxHotkey = Preferences.VoiceFxHotkey,
            PushToTalkHotkey = Preferences.PushToTalkHotkey,
            ShowHideHotkey = Preferences.ShowHideHotkey,
            MediaPlayPauseHotkey = Preferences.MediaPlayPauseHotkey,
            MediaStopHotkey = Preferences.MediaStopHotkey,
            MediaBackHotkey = Preferences.MediaBackHotkey,
            MediaForwardHotkey = Preferences.MediaForwardHotkey,
            MediaVolume = Preferences.MediaVolume,
            MediaMonitorEnabled = Preferences.MediaMonitorEnabled,
            MediaSendEnabled = Preferences.MediaSendEnabled,
            MediaSyncOffsetMilliseconds = Preferences.MediaSyncOffsetMilliseconds,
            LastMediaPath = Preferences.LastMediaPath
        }
    };

    public override string ToString() => Name;
}
