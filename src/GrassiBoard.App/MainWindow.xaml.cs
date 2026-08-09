using System.ComponentModel;
using System.Windows;
using GrassiBoard.Models;
using GrassiBoard.ViewModels;
using GrassiBoard.Views;

namespace GrassiBoard;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
        _viewModel.EditPadRequested += OnEditPadRequested;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _viewModel.EditPadRequested -= OnEditPadRequested;
        _viewModel.Dispose();
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
