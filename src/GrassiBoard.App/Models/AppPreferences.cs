namespace GrassiBoard.Models;

internal sealed class AppPreferences
{
    public bool MinimizeToTray { get; set; } = true;
    public bool StartMinimized { get; set; }
    public bool StartWithWindows { get; set; }
    public string MuteHotkey { get; set; } = "Ctrl+Alt+M";
    public string StopAllHotkey { get; set; } = "Ctrl+Alt+S";
    public string VoiceFxHotkey { get; set; } = "Ctrl+Alt+V";
    public string PushToTalkHotkey { get; set; } = string.Empty;
    public string ShowHideHotkey { get; set; } = "Ctrl+Alt+G";
    public string MediaPlayPauseHotkey { get; set; } = "Ctrl+Alt+Space";
    public string MediaStopHotkey { get; set; } = "Ctrl+Alt+Down";
    public string MediaBackHotkey { get; set; } = "Ctrl+Alt+Left";
    public string MediaForwardHotkey { get; set; } = "Ctrl+Alt+Right";
    public double MediaVolume { get; set; } = 0.8;
    public bool MediaMonitorEnabled { get; set; } = true;
    public bool MediaSendEnabled { get; set; } = true;
    public string LastMediaPath { get; set; } = string.Empty;
}
