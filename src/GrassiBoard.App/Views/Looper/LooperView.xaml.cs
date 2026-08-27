using System.Windows;
using System.Windows.Controls;
using GrassiBoard.ViewModels;

namespace GrassiBoard.Views.Looper;

public partial class LooperView : UserControl
{
    private bool _routingBound;

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
        DataContext = new LooperViewModel(
            monitorDeviceIdProvider: () => mainViewModel.SelectedMonitorOutput?.Id);
        _routingBound = true;
    }

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
        if (DataContext is IDisposable disposable) disposable.Dispose();
    }
}
