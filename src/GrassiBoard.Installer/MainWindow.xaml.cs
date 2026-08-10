using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using Microsoft.Win32;

namespace GrassiBoard.Installer;

public partial class MainWindow : Window
{
    private const string CableDownloadUrl =
        "https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip";
    private readonly InstallationService _installation = new();
    private readonly string? _uninstallTarget;
    private bool _busy;
    private bool _cablePresent;
    private string _installedPath = string.Empty;

    public MainWindow(string? uninstallTarget)
    {
        InitializeComponent();
        _uninstallTarget = uninstallTarget;
        InstallPathTextBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "GrassiBoard");

        if (_uninstallTarget is not null)
        {
            Title = "Uninstall GrassiBoard";
            SetupTitle.Text = "Remove GrassiBoard from this computer?";
            CableStatusText.Text = "Your profiles, Sound Pads and preferences in AppData will be preserved.";
            InstallPathPanel.Visibility = Visibility.Collapsed;
            DesktopShortcutCheckBox.Visibility = Visibility.Collapsed;
            InstallButton.Content = "Uninstall";
        }
        else
        {
            Loaded += OnLoaded;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _cablePresent = await Task.Run(InstallationService.IsCompatibleVirtualCableInstalled);
        CableStatusText.Text = _cablePresent
            ? "Compatible virtual audio cable detected · ready for GrassiBoard routing"
            : "Virtual cable not detected · installation can continue and a publisher download link will be shown";
        CableStatusText.Foreground = _cablePresent
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(118, 231, 183))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(242, 207, 114));
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }
        if (_uninstallTarget is not null)
        {
            ScheduleTemporarySelfDelete();
        }
        Close();
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose the GrassiBoard installation folder",
            InitialDirectory = Directory.Exists(InstallPathTextBox.Text)
                ? InstallPathTextBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };
        if (dialog.ShowDialog(this) == true)
        {
            InstallPathTextBox.Text = dialog.FolderName;
        }
    }

    private async void OnInstallClick(object sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }
        if (_uninstallTarget is not null)
        {
            await RunUninstallAsync();
            return;
        }

        _busy = true;
        SetupPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        try
        {
            _cablePresent = await Task.Run(InstallationService.IsCompatibleVirtualCableInstalled);
            var progress = new Progress<InstallProgress>(UpdateProgress);
            await _installation.InstallAsync(
                InstallPathTextBox.Text,
                DesktopShortcutCheckBox.IsChecked == true,
                progress,
                CancellationToken.None);
            _installedPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(InstallPathTextBox.Text.Trim()));
            ProgressPanel.Visibility = Visibility.Collapsed;
            CompletePanel.Visibility = Visibility.Visible;
            CableDownloadMessage.Visibility = _cablePresent ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception exception)
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            SetupPanel.Visibility = Visibility.Visible;
            MessageBox.Show(this, exception.Message, "GrassiBoard installation failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task RunUninstallAsync()
    {
        _busy = true;
        SetupPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        try
        {
            var progress = new Progress<InstallProgress>(UpdateProgress);
            await _installation.UninstallAsync(_uninstallTarget!, progress);
            ProgressPanel.Visibility = Visibility.Collapsed;
            CompletePanel.Visibility = Visibility.Visible;
            CompleteTitle.Text = "GrassiBoard was uninstalled";
            CompleteMessage.Text = "Application files, shortcuts and the Windows uninstall entry were removed. Your AppData profiles were preserved.";
            OpenButton.Visibility = Visibility.Collapsed;
            FinishButton.Content = "Finish";
        }
        catch (Exception exception)
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            SetupPanel.Visibility = Visibility.Visible;
            MessageBox.Show(this, exception.Message, "GrassiBoard uninstall failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _busy = false;
        }
    }

    private void UpdateProgress(InstallProgress progress)
    {
        InstallProgressBar.Value = Math.Clamp(progress.Percent, 0, 100);
        ProgressStatusText.Text = progress.Status;
    }

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        string executable = Path.Combine(_installedPath, "GrassiBoard.exe");
        if (File.Exists(executable))
        {
            Process.Start(new ProcessStartInfo { FileName = executable, WorkingDirectory = _installedPath, UseShellExecute = true });
        }
        Close();
    }

    private void OnCableLinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        string target = e.Uri?.AbsoluteUri ?? CableDownloadUrl;
        Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
        e.Handled = true;
    }

    private static void ScheduleTemporarySelfDelete()
    {
        string? path = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(path) && path.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
        {
            NativeMethods.MoveFileEx(path, null, 0x4U);
        }
    }

    private static partial class NativeMethods
    {
        [LibraryImport("kernel32.dll", EntryPoint = "MoveFileExW", StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool MoveFileEx(string existingFileName, string? newFileName, uint flags);
    }
}
