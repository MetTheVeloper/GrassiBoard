using System.Diagnostics;
using System.IO;
using System.Windows;

namespace GrassiBoard.Installer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length > 0 && string.Equals(e.Args[0], "--uninstall", StringComparison.OrdinalIgnoreCase))
        {
            StartDetachedUninstaller();
            Shutdown();
            return;
        }

        string? uninstallTarget = e.Args.Length > 1 &&
            string.Equals(e.Args[0], "--uninstall-worker", StringComparison.OrdinalIgnoreCase)
                ? e.Args[1]
                : null;
        new MainWindow(uninstallTarget).Show();
    }

    private static void StartDetachedUninstaller()
    {
        string processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Installer path is unavailable.");
        string installDirectory = Path.GetDirectoryName(processPath) ?? throw new InvalidOperationException("Install directory is unavailable.");
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "GrassiBoard", "Uninstall");
        Directory.CreateDirectory(temporaryDirectory);
        string temporaryPath = Path.Combine(temporaryDirectory, $"GrassiBoard-Uninstall-{Guid.NewGuid():N}.exe");
        File.Copy(processPath, temporaryPath, true);
        Process.Start(new ProcessStartInfo
        {
            FileName = temporaryPath,
            UseShellExecute = true,
            ArgumentList = { "--uninstall-worker", installDirectory }
        });
    }
}
