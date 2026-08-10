using System.ComponentModel;
using System.Windows;
using GrassiBoard.Models;
using GrassiBoard.Services;
using GrassiBoard.ViewModels;
using GrassiBoard.Views;

namespace GrassiBoard;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow() : this(new MainViewModel())
    {
    }

    internal MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
        StateChanged += OnWindowStateChanged;
        _viewModel.EditPadRequested += OnEditPadRequested;
        UpdateChromeButtons();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _viewModel.EditPadRequested -= OnEditPadRequested;
        StateChanged -= OnWindowStateChanged;
        _viewModel.Dispose();
    }

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
    {
        ThemeManager.Toggle();
        UpdateChromeButtons();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnWindowStateChanged(object? sender, EventArgs e) => UpdateChromeButtons();

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
            await _viewModel.ApplyPadEditAsync(
                pad,
                dialog.PadTitle,
                dialog.AudioPath,
                dialog.PadVolume,
                dialog.Loop,
                dialog.RestartOnPress);
        }
    }
}
