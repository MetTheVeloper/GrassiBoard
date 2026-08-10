using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace GrassiBoard.Services;

internal static class ThemeManager
{
    private static readonly IReadOnlyDictionary<string, string> DarkPalette =
        new Dictionary<string, string>
        {
            ["PageBrush"] = "#090E13",
            ["SidebarBrush"] = "#0C1219",
            ["PanelBrush"] = "#131C26",
            ["PanelRaisedBrush"] = "#182431",
            ["PanelQuietBrush"] = "#0E161F",
            ["TopBarBrush"] = "#0B1118",
            ["ElevatedCardBrush"] = "#17222E",
            ["ControlSurfaceBrush"] = "#101923",
            ["HoverSurfaceBrush"] = "#1D2B38",
            ["PressedSurfaceBrush"] = "#243746",
            ["SelectedSurfaceBrush"] = "#163D32",
            ["BorderBrush"] = "#263746",
            ["TextBrush"] = "#F4F7F9",
            ["MutedTextBrush"] = "#93A7BA",
            ["AccentBrush"] = "#76E7B7",
            ["AccentDarkBrush"] = "#163D32",
            ["SuccessBrush"] = "#76E7B7",
            ["WarningBrush"] = "#F2CF72",
            ["DangerBrush"] = "#FF7C92",
            ["DangerDarkBrush"] = "#47232C",
            ["MeterTrackBrush"] = "#243442",
            ["DisabledBrush"] = "#607080"
        };

    private static readonly IReadOnlyDictionary<string, string> LightPalette =
        new Dictionary<string, string>
        {
            ["PageBrush"] = "#F3F6F8",
            ["SidebarBrush"] = "#FFFFFF",
            ["PanelBrush"] = "#FFFFFF",
            ["PanelRaisedBrush"] = "#E7EDF2",
            ["PanelQuietBrush"] = "#EDF2F5",
            ["TopBarBrush"] = "#FFFFFF",
            ["ElevatedCardBrush"] = "#FFFFFF",
            ["ControlSurfaceBrush"] = "#F7F9FA",
            ["HoverSurfaceBrush"] = "#E8F0F4",
            ["PressedSurfaceBrush"] = "#DCE8ED",
            ["SelectedSurfaceBrush"] = "#D7F1E6",
            ["BorderBrush"] = "#C8D4DD",
            ["TextBrush"] = "#15212B",
            ["MutedTextBrush"] = "#53697B",
            ["AccentBrush"] = "#17815F",
            ["AccentDarkBrush"] = "#D7F1E6",
            ["SuccessBrush"] = "#17815F",
            ["WarningBrush"] = "#8D6500",
            ["DangerBrush"] = "#C33C55",
            ["DangerDarkBrush"] = "#F8E3E8",
            ["MeterTrackBrush"] = "#D7E1E8",
            ["DisabledBrush"] = "#91A0AC"
        };

    private static readonly string PreferencesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GrassiBoard",
        "preferences.json");

    public static bool IsDark { get; private set; } = true;

    public static void Initialize()
    {
        bool isDark = true;
        try
        {
            if (File.Exists(PreferencesPath))
            {
                using FileStream stream = File.OpenRead(PreferencesPath);
                ThemePreferences? preferences = JsonSerializer.Deserialize<ThemePreferences>(stream);
                isDark = !string.Equals(preferences?.Theme, "Light", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            CrashReporter.Report(exception, "Loading theme preferences", false);
        }

        Apply(isDark, persist: false);
    }

    public static void Toggle() => Apply(!IsDark, persist: true);

    internal static void Apply(bool isDark, bool persist)
    {
        IsDark = isDark;
        IReadOnlyDictionary<string, string> palette = isDark ? DarkPalette : LightPalette;
        ResourceDictionary tokens = Application.Current.Resources.MergedDictionaries
            .First(dictionary => dictionary.Contains("PageColor"));
        foreach ((string key, string value) in palette)
        {
            tokens[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        }

        if (!persist)
        {
            return;
        }

        try
        {
            string? directory = Path.GetDirectoryName(PreferencesPath);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }
            using FileStream stream = File.Create(PreferencesPath);
            JsonSerializer.Serialize(stream, new ThemePreferences(isDark ? "Dark" : "Light"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            CrashReporter.Report(exception, "Saving theme preferences", false);
        }
    }

    private sealed record ThemePreferences(string Theme);
}
