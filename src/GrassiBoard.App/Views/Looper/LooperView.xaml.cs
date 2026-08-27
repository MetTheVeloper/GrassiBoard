using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GrassiBoard.Models;
using GrassiBoard.Services;
using GrassiBoard.Services.Looper;
using GrassiBoard.ViewModels;

namespace GrassiBoard.Views.Looper;

public partial class LooperView : UserControl
{
    private readonly LooperRecordService _recordService = new();
    private readonly DispatcherTimer _recordTimer;
    private bool _routingBound;
    private bool _syncingMonitor;
    private bool _finishingRecord;
    private MainViewModel? _mainViewModel;

    public LooperView()
    {
        InitializeComponent();
        _recordTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, OnRecordTimerTick, Dispatcher.CurrentDispatcher);
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

    private async void OnRecordFirstLoopClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LooperViewModel viewModel || _finishingRecord) return;

        if (_recordService.IsRecording)
        {
            await FinishFirstLoopRecordingAsync(viewModel);
            return;
        }

        if (!viewModel.EngineReady)
        {
            RecordStatusText.Text = "Start the normal GrassiBoard audio engine first, then record the Master.";
            return;
        }

        bool started = await _recordService.StartAsync();
        if (!started)
        {
            RecordStatusText.Text = _recordService.LastError;
            return;
        }

        RecordFirstLoopButton.Content = "Stop Recording";
        ImportAudioButton.IsEnabled = false;
        NewProjectButton.IsEnabled = false;
        RecordStatusText.Text = "Recording processed Voice… live microphone monitoring remains off.";
        _recordTimer.Start();
    }

    private async Task FinishFirstLoopRecordingAsync(LooperViewModel viewModel)
    {
        if (_finishingRecord) return;
        _finishingRecord = true;
        _recordTimer.Stop();
        try
        {
            LooperRecordedTake? take = await _recordService.StopAsync();
            ResetRecordUi();
            if (take is null)
            {
                RecordStatusText.Text = _recordService.LastError;
                return;
            }

            RecordStatusText.Text = "Preparing recorded waveform…";
            string takePath = await WriteTakeWaveAsync(take);
            await viewModel.LoadImportedAudioAsync(takePath);
            RecordStatusText.Text = take.SourceMode == LooperRecordSourceMode.Remote
                ? "Phone Mic Take captured. Trim it, audition the seam, then Set As Master Loop."
                : "Microphone Take captured. Trim it, audition the seam, then Set As Master Loop.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ResetRecordUi();
            RecordStatusText.Text = $"Could not prepare the recorded Take: {exception.Message}";
        }
        finally
        {
            _finishingRecord = false;
        }
    }

    private void OnRecordTimerTick(object? sender, EventArgs e)
    {
        if (!_recordService.IsRecording || DataContext is not LooperViewModel viewModel) return;
        if (!_recordService.TryGetState(out LooperRecordNativeState state)) return;

        string source = state.SourceMode == (uint)LooperRecordSourceMode.Remote ? "Phone Mic" : "Windows Mic";
        TimeSpan elapsed = TimeSpan.FromSeconds(state.CapturedFrames / (double)LooperRecordService.SampleRate);
        RecordStatusText.Text = $"Recording {elapsed:m\\:ss\\.f} · {source} · processed Voice FX printed · live mic monitor OFF";

        if (state.SourceChanged != 0U || state.Active == 0U)
        {
            _ = FinishFirstLoopRecordingAsync(viewModel);
        }
    }

    private static Task<string> WriteTakeWaveAsync(LooperRecordedTake take)
    {
        return Task.Run(() =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "GrassiBoard", "LooperTakes");
            Directory.CreateDirectory(directory);
            string sourceLabel = take.SourceMode == LooperRecordSourceMode.Remote ? "Phone Mic Take" : "Microphone Take";
            string path = Path.Combine(directory, $"{sourceLabel} {DateTime.Now:yyyyMMdd-HHmmss-fff}.wav");

            checked
            {
                int dataBytes = take.StereoSamples.Length * sizeof(float);
                using FileStream stream = File.Create(path);
                using var writer = new BinaryWriter(stream);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataBytes);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)3);
                writer.Write((short)LooperRecordService.Channels);
                writer.Write(LooperRecordService.SampleRate);
                writer.Write(LooperRecordService.SampleRate * LooperRecordService.Channels * sizeof(float));
                writer.Write((short)(LooperRecordService.Channels * sizeof(float)));
                writer.Write((short)32);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(dataBytes);
                foreach (float sample in take.StereoSamples) writer.Write(float.IsFinite(sample) ? sample : 0.0F);
            }
            return path;
        });
    }

    private void ResetRecordUi()
    {
        _recordTimer.Stop();
        RecordFirstLoopButton.Content = "Record First Loop";
        ImportAudioButton.IsEnabled = true;
        NewProjectButton.IsEnabled = true;
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedMonitorOutput)) SyncMonitorSelection();
    }

    private void SyncMonitorSelection()
    {
        if (_mainViewModel is null) return;
        _syncingMonitor = true;
        try { LooperMonitorOutputCombo.SelectedItem = _mainViewModel.SelectedMonitorOutput; }
        finally { _syncingMonitor = false; }
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
        if (!IsVisible)
        {
            if (_recordService.IsRecording)
            {
                _recordService.Cancel();
                ResetRecordUi();
            }
            if (DataContext is LooperViewModel viewModel) viewModel.OnWorkspaceHidden();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        IsVisibleChanged -= OnIsVisibleChanged;
        Unloaded -= OnUnloaded;
        _recordTimer.Stop();
        _recordService.Dispose();
        if (_mainViewModel is not null) _mainViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
        _mainViewModel = null;
        if (DataContext is IDisposable disposable) disposable.Dispose();
    }
}
