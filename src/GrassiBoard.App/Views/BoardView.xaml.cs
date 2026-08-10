using System.Windows;
using System.Windows.Controls;
using GrassiBoard.ViewModels;

namespace GrassiBoard.Views;

public partial class BoardView : System.Windows.Controls.UserControl
{
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
}
