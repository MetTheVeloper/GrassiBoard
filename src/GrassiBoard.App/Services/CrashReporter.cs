using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace GrassiBoard.Services;

internal static class CrashReporter
{
    private static int _handlingFatalError;

    public static string Report(Exception exception, string context, bool fatal, string? directoryOverride = null)
    {
        try
        {
            string directory = directoryOverride ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GrassiBoard",
                "CrashReports");
            Directory.CreateDirectory(directory);

            string timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff");
            string kind = fatal ? "crash" : "error";
            string path = Path.Combine(directory, $"GrassiBoard-{kind}-{timestamp}-{Environment.ProcessId}.txt");
            string report = BuildReport(exception, context, fatal);
            File.WriteAllText(path, report, Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(directory, "latest.txt"),
                report,
                Encoding.UTF8);
            return path;
        }
        catch
        {
            return "The crash report could not be written.";
        }
    }

    public static bool BeginFatalReport() => Interlocked.Exchange(ref _handlingFatalError, 1) == 0;

    private static string BuildReport(Exception exception, string context, bool fatal)
    {
        using Process process = Process.GetCurrentProcess();
        var report = new StringBuilder();
        report.AppendLine($"GrassiBoard {Shared.BuildInfo.CurrentVersion} {(fatal ? "fatal crash" : "error")}");
        report.AppendLine($"Local time: {DateTimeOffset.Now:O}");
        report.AppendLine($"UTC: {DateTimeOffset.UtcNow:O}");
        report.AppendLine($"Context: {context}");
        report.AppendLine($"Process: {process.ProcessName} ({Environment.ProcessId})");
        report.AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
        report.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        report.AppendLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");
        report.AppendLine($"Thread: {Environment.CurrentManagedThreadId}");
        report.AppendLine($"Base directory: {AppContext.BaseDirectory}");
        report.AppendLine();
        report.AppendLine(exception.ToString());
        return report.ToString();
    }
}
