using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using GrassiBoard.Models;
using GrassiBoard.Services;
using GrassiBoard.ViewModels;
using GrassiBoard.Views;
using GrassiBoard.Views.Looper;

namespace GrassiBoard;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private HwndSource? _windowSource;
    private TrayService? _tray;
    private bool _exiting;
    private LooperView? _looperView;
    private Button? _looperButton;
    private TextBlock? _pageTitleText;
    private TextBlock? _pageSubtitleText;

    public MainWindow() : this(new MainViewModel())
    {
    }

    internal MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
        InstallLooperWorkspace();
        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
        StateChanged += OnWindowStateChanged;
        _viewModel.EditPadRequested += OnEditPadRequested;
        UpdateChromeButtons();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        InstallLooperWorkspace();
        await _viewModel.InitializeAsync();
        _tray?.SetMuted(_viewModel.MicrophoneMuted);
        if (_viewModel.StartMinimized || Environment.GetCommandLineArgs().Contains("--minimized", StringComparer.OrdinalIgnoreCase))
        {
            HideToTray();
        }
    }

    private void InstallLooperWorkspace()
    {
        if (_looperView is null)
        {
            BoardView? boardView = FindLogicalDescendants<BoardView>(this).FirstOrDefault();
            if (boardView is not null && LogicalTreeHelper.GetParent(boardView) is Grid contentGrid)
            {
                _looperView = new LooperView
                {
                    DataContext = new LooperViewModel(),
                    Visibility = Visibility.Collapsed
                };
                contentGrid.Children.Add(_looperView);
                Panel.SetZIndex(_looperView, 50);
            }
        }

        if (_looperButton is null)
        {
            Button? mixerButton = FindLogicalDescendants<Button>(this)
                .FirstOrDefault(button => string.Equals(button.CommandParameter as string, "Mixer", StringComparison.Ordinal));
            if (mixerButton is not null && LogicalTreeHelper.GetParent(mixerButton) is StackPanel navigationPanel)
            {
                var icon = new TextBlock { Text = "\uE768", Width = 22 };
                icon.SetResourceReference(FrameworkElement.StyleProperty, "GbIconTextStyle");
                var label = new TextBlock { Text = "Looper", Margin = new Thickness(8, 0, 0, 0) };
                var content = new StackPanel { Orientation = Orientation.Horizontal };
                content.Children.Add(icon);
                content.Children.Add(label);
                _looperButton = new Button { Content = content, ToolTip = "GrassiLooper v1.4 workspace" };
                _looperButton.SetResourceReference(FrameworkElement.StyleProperty, "SidebarButtonStyle");
                _looperButton.Click += OnLooperNavigationClick;
                int mixerIndex = navigationPanel.Children.IndexOf(mixerButton);
                navigationPanel.Children.Insert(Math.Max(0, mixerIndex + 1), _looperButton);
            }
        }

        foreach (Button button in FindLogicalDescendants<Button>(this))
        {
            if (button.Tag as string == "LooperNavigationHooked") continue;
            if (button.CommandParameter is string parameter &&
                parameter is "Board" or "Voice" or "Mixer" or "Routing" or "Settings")
            {
                button.Click += OnPrimaryNavigationClick;
                button.Tag = "LooperNavigationHooked";
            }
        }

        _pageTitleText ??= FindBoundTextBlock("PageTitle");
        _pageSubtitleText ??= FindBoundTextBlock("PageSubtitle");
    }

    private TextBlock? FindBoundTextBlock(string path) => FindLogicalDescendants<TextBlock>(this)
        .FirstOrDefault(text => BindingOperations.GetBinding(text, TextBlock.TextProperty)?.Path.Path == path);

    private void OnLooperNavigationClick(object sender, RoutedEventArgs e)
    {
        InstallLooperWorkspace();
        if (_looperView is null) return;
        _viewModel.CurrentPage = (AppPage)int.MaxValue;
        _looperView.Visibility = Visibility.Visible;
        if (_pageTitleText is not null) _pageTitleText.SetCurrentValue(TextBlock.TextProperty, "Looper");
        if (_pageSubtitleText is not null) _pageSubtitleText.SetCurrentValue(TextBlock.TextProperty, "Build one sample-defined Master, then layer aligned recordings without disturbing the live Program route.");
        if (_looperButton is not null)
        {
            _looperButton.SetResourceReference(Control.BackgroundProperty, "AccentDarkBrush");
            _looperButton.SetResourceReference(Control.ForegroundProperty, "AccentBrush");
            _looperButton.FontWeight = FontWeights.SemiBold;
        }
    }

    private void OnPrimaryNavigationClick(object sender, RoutedEventArgs e)
    {
        if (_looperView is not null) _looperView.Visibility = Visibility.Collapsed;
        if (_looperButton is not null)
        {
            _looperButton.ClearValue(Control.BackgroundProperty);
            _looperButton.ClearValue(Control.ForegroundProperty);
            _looperButton.ClearValue(Control.FontWeightProperty);
        }
    }

    private static IEnumerable<T> FindLogicalDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is T match) yield return match;
            if (child is DependencyObject dependencyObject)
            {
                foreach (T descendant in FindLogicalDescendants<T>(dependencyObject)) yield return descendant;
            }
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_windowSource is not null)
        {
            _windowSource.RemoveHook(WindowMessageHook);
            _windowSource = null;
        }
        _viewModel.EditPadRequested -= OnEditPadRequested;
        StateChanged -= OnWindowStateChanged;
        _tray?.Dispose();
        _tray = null;
        _viewModel.Dispose();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        nint handle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(handle);
        _windowSource?.AddHook(WindowMessageHook);
        _viewModel.AttachWindowServices(handle, ToggleWindowVisibility);
        _tray = new TrayService(
            Dispatcher,
            ShowFromTray,
            () => { _viewModel.MicrophoneMuted = !_viewModel.MicrophoneMuted; _tray?.SetMuted(_viewModel.MicrophoneMuted); },
            _viewModel.TriggerStopAll,
            ExitFromTray);

        int renderingPolicy = 2;
        _ = DwmSetWindowAttribute(handle, 2, ref renderingPolicy, sizeof(int));
        var margins = new Margins { Left = 1, Right = 1, Top = 1, Bottom = 1 };
        _ = DwmExtendFrameIntoClientArea(handle, ref margins);
    }

    private nint WindowMessageHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (_viewModel.HandleWindowMessage(message, wParam))
        {
            handled = true;
            return nint.Zero;
        }

        const int WmGetMinMaxInfo = 0x0024;
        if (message != WmGetMinMaxInfo || lParam == nint.Zero) return nint.Zero;
        nint monitor = MonitorFromWindow(hwnd, 2U);
        if (monitor == nint.Zero) return nint.Zero;
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo)) return nint.Zero;
        MinMaxInfo minMax = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        minMax.MaxPosition.X = monitorInfo.WorkArea.Left - monitorInfo.MonitorArea.Left;
        minMax.MaxPosition.Y = monitorInfo.WorkArea.Top - monitorInfo.MonitorArea.Top;
        minMax.MaxSize.X = monitorInfo.WorkArea.Right - monitorInfo.WorkArea.Left;
        minMax.MaxSize.Y = monitorInfo.WorkArea.Bottom - monitorInfo.WorkArea.Top;
        Marshal.StructureToPtr(minMax, lParam, false);
        handled = true;
        return nint.Zero;
    }

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
    {
        ThemeManager.Toggle();
        UpdateChromeButtons();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        _exiting = true;
        Close();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        UpdateChromeButtons();
        if (WindowState == WindowState.Minimized && _viewModel.MinimizeToTray && !_exiting) HideToTray();
    }

    private void HideToTray()
    {
        WindowState = WindowState.Minimized;
        Hide();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void ToggleWindowVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized) HideToTray();
        else ShowFromTray();
    }

    private void ExitFromTray()
    {
        _exiting = true;
        Close();
    }

    private void UpdateChromeButtons()
    {
        if (ThemeToggleButton is not null)
        {
            ThemeToggleButton.Content = ThemeManager.IsDark ? "\uE706" : "\uE708";
            ThemeToggleButton.ToolTip = ThemeManager.IsDark ? "Switch to light theme" : "Switch to dark theme";
        }
        if (MaximizeButton is not null)
        {
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
            MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximize";
        }
    }

    private async void OnEditPadRequested(SoundPadModel pad)
    {
        var dialog = new PadEditorWindow(pad) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            await _viewModel.ApplyPadEditAsync(pad, dialog.PadTitle, dialog.AudioPath, dialog.PadVolume, dialog.Loop, dialog.RestartOnPress, dialog.Hotkey);
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo { public NativePoint Reserved; public NativePoint MaxSize; public NativePoint MaxPosition; public NativePoint MinTrackSize; public NativePoint MaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect MonitorArea; public NativeRect WorkArea; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left; public int Right; public int Top; public int Bottom; }

    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo monitorInfo);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(nint window, ref Margins margins);
}
