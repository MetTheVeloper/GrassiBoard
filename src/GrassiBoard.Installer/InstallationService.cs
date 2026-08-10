using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace GrassiBoard.Installer;

internal sealed record InstallProgress(double Percent, string Status);

internal sealed class InstallationService
{
    internal const string ProductVersion = "1.0.1";
    private const string ManifestFileName = ".grassiboard-install-manifest.json";

    public async Task InstallAsync(
        string targetDirectory,
        bool createDesktopShortcut,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        string target = ValidateTarget(targetDirectory);
        string staging = Path.Combine(Path.GetTempPath(), "GrassiBoard", "Install", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            progress.Report(new InstallProgress(2, "Reading the embedded application package"));
            using Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("GrassiBoardPayload.zip") ??
                throw new InvalidOperationException("The installer payload is missing. Download the complete setup executable again.");
            using var archive = new ZipArchive(payload, ZipArchiveMode.Read, false);
            ZipArchiveEntry[] files = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToArray();
            for (int index = 0; index < files.Length; ++index)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ZipArchiveEntry entry = files[index];
                string destination = SafeCombine(staging, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using Stream source = entry.Open();
                await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                await source.CopyToAsync(output, cancellationToken);
                progress.Report(new InstallProgress(5 + 45.0 * (index + 1) / Math.Max(files.Length, 1), $"Extracting {entry.Name}"));
            }

            if (!File.Exists(Path.Combine(staging, "GrassiBoard.exe")) ||
                !File.Exists(Path.Combine(staging, "GrassiBoard.AudioEngine.dll")))
            {
                throw new InvalidDataException("The embedded package does not contain the required GrassiBoard files.");
            }

            Directory.CreateDirectory(target);
            RemovePriorManagedFiles(target);
            string[] stagedFiles = Directory.GetFiles(staging, "*", SearchOption.AllDirectories);
            var installedRelativePaths = new List<string>(stagedFiles.Length + 1);
            for (int index = 0; index < stagedFiles.Length; ++index)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relative = Path.GetRelativePath(staging, stagedFiles[index]);
                string destination = SafeCombine(target, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(stagedFiles[index], destination, true);
                installedRelativePaths.Add(relative);
                progress.Report(new InstallProgress(50 + 35.0 * (index + 1) / Math.Max(stagedFiles.Length, 1), $"Installing {Path.GetFileName(relative)}"));
            }

            string currentExecutable = Environment.ProcessPath ?? throw new InvalidOperationException("The setup executable path is unavailable.");
            string uninstallerPath = Path.Combine(target, "GrassiBoard.Uninstall.exe");
            File.Copy(currentExecutable, uninstallerPath, true);
            installedRelativePaths.Add(Path.GetFileName(uninstallerPath));

            var manifest = new InstallManifest(ProductVersion, installedRelativePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            await File.WriteAllTextAsync(
                Path.Combine(target, ManifestFileName),
                JsonSerializer.Serialize(manifest, JsonOptions),
                cancellationToken);

            progress.Report(new InstallProgress(90, "Creating shortcuts and Windows uninstall entry"));
            CreateShortcuts(target, createDesktopShortcut);
            RegisterUninstaller(target, uninstallerPath, installedRelativePaths.Sum(path =>
                new FileInfo(SafeCombine(target, path)).Exists ? new FileInfo(SafeCombine(target, path)).Length : 0L));
            progress.Report(new InstallProgress(100, "Installation complete"));
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    public Task UninstallAsync(string targetDirectory, IProgress<InstallProgress> progress) => Task.Run(() =>
    {
        string target = ValidateInstalledTarget(targetDirectory);
        progress.Report(new InstallProgress(10, "Reading the installation manifest"));
        string manifestPath = Path.Combine(target, ManifestFileName);
        InstallManifest? manifest = JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(manifestPath), JsonOptions);
        string[] files = manifest?.Files ?? [];
        for (int index = 0; index < files.Length; ++index)
        {
            string path = SafeCombine(target, files[index]);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            progress.Report(new InstallProgress(10 + 70.0 * (index + 1) / Math.Max(files.Length, 1), $"Removing {Path.GetFileName(files[index])}"));
        }
        if (File.Exists(manifestPath))
        {
            File.Delete(manifestPath);
        }
        DeleteShortcuts();
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\GrassiBoard", false);
        DeleteEmptyDirectories(target);
        progress.Report(new InstallProgress(100, "GrassiBoard was removed"));
    });

    public static bool IsCompatibleVirtualCableInstalled()
    {
        try
        {
            using RegistryKey? audio = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio");
            if (audio is null)
            {
                return false;
            }
            foreach (string flow in new[] { "Render", "Capture" })
            {
                using RegistryKey? flowKey = audio.OpenSubKey(flow);
                if (flowKey is null)
                {
                    continue;
                }
                foreach (string endpointName in flowKey.GetSubKeyNames())
                {
                    using RegistryKey? endpoint = flowKey.OpenSubKey(endpointName);
                    if (endpoint?.GetValue("DeviceState") is not int state || state != 1)
                    {
                        continue;
                    }
                    if (ContainsCableMarker(endpoint))
                    {
                        return true;
                    }
                }
            }
        }
        catch (SecurityException)
        {
            return false;
        }
        return false;
    }

    private static bool ContainsCableMarker(RegistryKey endpoint)
    {
        using RegistryKey? properties = endpoint.OpenSubKey("Properties");
        if (properties is null)
        {
            return false;
        }
        foreach (string valueName in properties.GetValueNames())
        {
            object? value = properties.GetValue(valueName);
            string text = value switch
            {
                string stringValue => stringValue,
                string[] strings => string.Join(' ', strings),
                byte[] bytes => Encoding.Unicode.GetString(bytes),
                _ => string.Empty
            };
            if (text.Contains("GrassiBoard", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (text.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Virtual Audio Cable", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("AMM Virtual Audio", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string ValidateTarget(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Choose an installation folder.");
        }
        string full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(directory.Trim()));
        string? root = Path.GetPathRoot(full);
        if (string.Equals(full.TrimEnd(Path.DirectorySeparatorChar), root?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A drive root cannot be used as the installation folder.");
        }
        return full.TrimEnd(Path.DirectorySeparatorChar);
    }

    private static string ValidateInstalledTarget(string directory)
    {
        string target = ValidateTarget(directory);
        if (!File.Exists(Path.Combine(target, ManifestFileName)) || !File.Exists(Path.Combine(target, "GrassiBoard.exe")))
        {
            throw new InvalidOperationException("This folder does not contain a managed GrassiBoard installation.");
        }
        return target;
    }

    private static string SafeCombine(string root, string relative)
    {
        string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string result = Path.GetFullPath(Path.Combine(rootFull, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!result.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Unsafe package path: {relative}");
        }
        return result;
    }

    private static void RemovePriorManagedFiles(string target)
    {
        string manifestPath = Path.Combine(target, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return;
        }
        try
        {
            InstallManifest? manifest = JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(manifestPath), JsonOptions);
            foreach (string relative in manifest?.Files ?? [])
            {
                string path = SafeCombine(target, relative);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
        catch (JsonException)
        {
            throw new InvalidDataException("The previous installation manifest is damaged. Uninstall the old copy before installing again.");
        }
    }

    private static void CreateShortcuts(string target, bool desktop)
    {
        string executable = Path.Combine(target, "GrassiBoard.exe");
        string startMenuFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "GrassiBoard");
        Directory.CreateDirectory(startMenuFolder);
        ShortcutService.Create(Path.Combine(startMenuFolder, "GrassiBoard.lnk"), executable, target);
        if (desktop)
        {
            ShortcutService.Create(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "GrassiBoard.lnk"),
                executable,
                target);
        }
    }

    private static void DeleteShortcuts()
    {
        string startMenuFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "GrassiBoard");
        TryDeleteFile(Path.Combine(startMenuFolder, "GrassiBoard.lnk"));
        if (Directory.Exists(startMenuFolder) && !Directory.EnumerateFileSystemEntries(startMenuFolder).Any())
        {
            Directory.Delete(startMenuFolder);
        }
        TryDeleteFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "GrassiBoard.lnk"));
    }

    private static void RegisterUninstaller(string target, string uninstallerPath, long installedBytes)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\GrassiBoard", true);
        key.SetValue("DisplayName", "GrassiBoard");
        key.SetValue("DisplayVersion", ProductVersion);
        key.SetValue("Publisher", "GrassiBoard");
        key.SetValue("InstallLocation", target);
        key.SetValue("DisplayIcon", Path.Combine(target, "GrassiBoard.exe"));
        key.SetValue("UninstallString", $"\"{uninstallerPath}\" --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", checked((int)Math.Min(int.MaxValue, (installedBytes + 1023L) / 1024L)), RegistryValueKind.DWord);
    }

    private static void DeleteEmptyDirectories(string target)
    {
        if (!Directory.Exists(target))
        {
            return;
        }
        foreach (string directory in Directory.GetDirectories(target, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
        if (!Directory.EnumerateFileSystemEntries(target).Any())
        {
            Directory.Delete(target);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch (IOException) { }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
    }

    private sealed record InstallManifest(string Version, string[] Files);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}

internal static class ShortcutService
{
    public static void Create(string shortcutPath, string targetPath, string workingDirectory)
    {
        var link = (IShellLinkW)(object)new ShellLink();
        link.SetPath(targetPath);
        link.SetWorkingDirectory(workingDirectory);
        link.SetDescription("GrassiBoard Virtual Microphone, Soundboard and Voice FX");
        link.SetIconLocation(targetPath, 0);
        ((IPersistFile)link).Save(shortcutPath, false);
        Marshal.FinalReleaseComObject(link);
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, nint findData, uint flags);
        void GetIDList(out nint itemIdList);
        void SetIDList(nint itemIdList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int maxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int iconPathLength, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(nint window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}
