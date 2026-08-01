using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using GrassiBoard.Shared;

namespace GrassiBoard;

public partial class MainWindow : Window
{
    private const uint ExpectedNativeApiVersion = 1;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildInfo build = BuildInfo.Load(Path.Combine(AppContext.BaseDirectory, "BuildInfo.json"));
        VersionText.Text = $"v{build.Version}";
        CommitText.Text = build.ShortCommit;

        try
        {
            uint apiVersion = NativeMethods.GetApiVersion();
            string nativeVersion = Marshal.PtrToStringUTF8(NativeMethods.GetVersion()) ?? "unknown";
            if (apiVersion != ExpectedNativeApiVersion)
            {
                SetNativeStatus($"ABI mismatch (got {apiVersion})", false);
                return;
            }

            SetNativeStatus($"Loaded · API {apiVersion} · v{nativeVersion}", true);
        }
        catch (Exception exception) when (exception is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            SetNativeStatus($"Load failed · {exception.GetType().Name}", false);
        }
    }

    private void SetNativeStatus(string text, bool healthy)
    {
        NativeStatusText.Text = text;
        NativeStatusDot.Fill = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(healthy ? "#75E6B5" : "#FF7A90"));
    }

    private static partial class NativeMethods
    {
        private const string LibraryName = "GrassiBoard.AudioEngine.dll";

        [LibraryImport(LibraryName, EntryPoint = "gb_get_api_version")]
        internal static partial uint GetApiVersion();

        [LibraryImport(LibraryName, EntryPoint = "gb_get_version")]
        internal static partial nint GetVersion();
    }
}
