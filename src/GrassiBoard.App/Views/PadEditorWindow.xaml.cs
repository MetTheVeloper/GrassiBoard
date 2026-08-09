using System.Windows;
using GrassiBoard.Models;
using Microsoft.Win32;

namespace GrassiBoard.Views;

public partial class PadEditorWindow : Window
{
    internal PadEditorWindow(SoundPadModel pad)
    {
        InitializeComponent();
        TitleBox.Text = pad.Title;
        PathBox.Text = pad.FilePath;
        VolumeSlider.Value = pad.Volume;
        LoopCheck.IsChecked = pad.Loop;
        RestartCheck.IsChecked = pad.RestartOnPress;
        UpdateVolumeLabel();
    }

    public string PadTitle => TitleBox.Text;
    public string AudioPath => PathBox.Text;
    public double PadVolume => VolumeSlider.Value;
    public bool Loop => LoopCheck.IsChecked == true;
    public bool RestartOnPress => RestartCheck.IsChecked == true;

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose Sound Pad audio",
            Filter = "Supported audio|*.wav;*.mp3|Wave audio|*.wav|MP3 audio|*.mp3",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            PathBox.Text = dialog.FileName;
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VolumeLabel is not null)
        {
            UpdateVolumeLabel();
        }
    }

    private void UpdateVolumeLabel() => VolumeLabel.Text = $"{VolumeSlider.Value:P0}";

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PathBox.Text))
        {
            MessageBox.Show(this, "Choose a WAV or MP3 file.", "Audio file required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
