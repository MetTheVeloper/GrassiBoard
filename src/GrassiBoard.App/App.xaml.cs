using System.IO;
using System.Text;
using System.Windows;

namespace GrassiBoard;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GrassiBoard");
            string logPath = Path.Combine(directory, "startup-error.txt");
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(logPath, BuildExceptionReport(exception), Encoding.UTF8);
            }
            catch (IOException)
            {
                logPath = "The startup log could not be written.";
            }

            MessageBox.Show(
                $"GrassiBoard could not start.\n\n{exception.GetType().Name}: {exception.Message}\n\nLog: {logPath}",
                "GrassiBoard startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static string BuildExceptionReport(Exception exception)
    {
        var report = new StringBuilder();
        report.AppendLine($"GrassiBoard {Shared.BuildInfo.CurrentVersion} startup error");
        report.AppendLine($"UTC: {DateTimeOffset.UtcNow:O}");
        report.AppendLine($"OS: {Environment.OSVersion}");
        report.AppendLine();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            report.AppendLine(current.ToString());
            report.AppendLine();
        }
        return report.ToString();
    }
}
