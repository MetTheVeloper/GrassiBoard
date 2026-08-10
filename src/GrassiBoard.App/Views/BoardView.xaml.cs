using System.Windows;
using System.Windows.Controls;
using GrassiBoard.ViewModels;

namespace GrassiBoard.Views;

public partial class BoardView : System.Windows.Controls.UserControl
{
    private bool _mediaTimelinePointerDown;

    public BoardView()
    {
        InitializeComponent();
    }

    private void OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            await viewModel.AddFilesAsync(paths);
        }
    }

    private void OnMediaTimelinePointerDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _mediaTimelinePointerDown = true;
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.BeginMediaTimelineSeek();
        }
    }

    private void OnMediaTimelinePointerUp(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        CommitMediaTimelineSeek();

    private void OnMediaTimelineLostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_mediaTimelinePointerDown && e.LeftButton == System.Windows.Input.MouseButtonState.Released)
        {
            CommitMediaTimelineSeek();
        }
    }

    private void CommitMediaTimelineSeek()
    {
        if (!_mediaTimelinePointerDown)
        {
            return;
        }

        _mediaTimelinePointerDown = false;
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.CommitMediaTimelineSeek();
        }
    }
}
