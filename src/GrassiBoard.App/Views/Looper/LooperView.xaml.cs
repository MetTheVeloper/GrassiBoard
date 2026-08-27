using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using GrassiBoard.Models;
using GrassiBoard.ViewModels;

namespace GrassiBoard.Views.Looper;

public partial class LooperView : UserControl
{
    private bool _routingBound;
    private bool _syncingMonitor;
    private MainViewModel? _mainViewModel;

    public LooperView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        IsVisibleChanged += OnIsVisibleChanged;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_routingBound || Window.GetWindow(this)?.DataContext is not MainViewModel mainViewModel) return;

        if (DataContext is IDisposable disposable) disposable.Dispose();
        _mainViewModel = mainViewModel;
        DataContext = new LooperViewModel(
            monitorDeviceIdProvider: () => mainViewModel.SelectedMonitorOutput?.Id,
            monitorDeviceNameProvider: () => mainViewModel.SelectedMonitorOutput?.Name);

        LooperMonitorOutputCombo.ItemsSource = mainViewModel.OutputDevices;
        SyncMonitorSelection();
        mainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;
        _routingBound = true;
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedMonitorOutput))
        {
            SyncMonitorSelection();
        }
    }

    private void SyncMonitorSelection()
    {
        if (_mainViewModel is null) return;
        _syncingMonitor = true;
        try
        {
            LooperMonitorOutputCombo.SelectedItem = _mainViewModel.SelectedMonitorOutput;
        }
        finally
        {
            _syncingMonitor = false;
        }
    }

    private void OnMonitorOutputSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingMonitor || _mainViewModel is null) return;
        if (LooperMonitorOutputCombo.SelectedItem is not AudioDevice selected) return;
        _mainViewModel.SelectedMonitorOutput = selected;
        if (DataContext is LooperViewModel viewModel) viewModel.OnMonitorOutputChanged();
    }

    private void OnZoomInClick(object sender, RoutedEventArgs e) => PendingWaveformEditor.ZoomIn();
    private void OnZoomOutClick(object sender, RoutedEventArgs e) => PendingWaveformEditor.ZoomOut();
    private void OnFitSelectionClick(object sender, RoutedEventArgs e) => PendingWaveformEditor.ZoomToSelection();

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsVisible && DataContext is LooperViewModel viewModel)
        {
            viewModel.OnWorkspaceHidden();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        IsVisibleChanged -= OnIsVisibleChanged;
        Unloaded -= OnUnloaded;
        if (_mainViewModel is not null) _mainViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
        _mainViewModel = null;
        if (DataContext is IDisposable disposable) disposable.Dispose();
    }
}