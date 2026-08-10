using Microsoft.Win32;

namespace GrassiBoard.Services;

internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GrassiBoard";

    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null)
            {
                return false;
            }
            if (enabled)
            {
                string executable = Environment.ProcessPath ?? string.Empty;
                if (string.IsNullOrWhiteSpace(executable))
                {
                    return false;
                }
                key.SetValue(ValueName, $"\"{executable}\" --minimized", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
