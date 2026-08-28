using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using GrassiBoard.Models;
using GrassiBoard.Models.Looper;
using GrassiBoard.Services;
using GrassiBoard.Services.Looper;
using GrassiBoard.ViewModels;

namespace GrassiBoard.Views.Looper;

public partial class LooperView : UserControl
{
    private const ulong ArmBoundaryWindowFrames = 480U; // 10 ms; Gate 5 owns final latency alignment.
    private const long MaxManagedChildTrackBytes = 256L * 1024L * 1024L;

    private static readonly Color[] LayerPalette =
    [
        Color.FromRgb(113, 172, 255),
        Color.FromRgb(177, 132, 255),
        Color.FromRgb(255, 132, 179),
        Color.FromRgb(255, 178, 91),
        Color.FromRgb(96, 208, 164),
        Color.FromRgb(89, 199, 225),
        Color.FromRgb(227, 139, 255),
        Color.FromRgb(238, 213, 101)
    ];

    private readonly LooperRecordService _recordService = new();
    private readonly WaveformAnalysisService _waveformAnalysisService = new();
    private readonly DispatcherTimer _recordTimer;
    private readonly DispatcherTimer _armTimer;
    private bool _routingBound;
    private bool _syncingMonitor;
    private bool _finishingRecord;
    private bool _nativeLayerGraphDirty = true;
    private MainViewModel? _mainViewModel;
    private LooperTrackModel? _selectedTrack;
    private LooperTrackModel? _armedTrack;
    private LooperTrackModel? _recordingLayer;
    private LooperLayerRecordState _layerRecordState;
    private ulong _armedPreviousPlayhead;
    private uint _nextNativeTrackId = 1U;

    public LooperView()
    {
        InitializeComponent();
        LayerModeCombo.SelectedIndex = 0;
        _recordTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(20), DispatcherPriority.Background, OnRecordTimerTick, Dispatcher.CurrentDispatcher);
        _armTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(4), DispatcherPriority.Send, OnArmTimerTick, Dispatcher.CurrentDispatcher);
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
        UpdateLayerUi();
    }

    private async void OnRecordFirstLoopClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LooperViewModel viewModel || _finishingRecord || _layerRecordState != LooperLayerRecordState.Idle) return;

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
            ResetFirstRecordUi();
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
            ResetFirstRecordUi();
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

        if (_recordingLayer is not null)
        {
            string source = state.SourceMode == (uint)LooperRecordSourceMode.Remote ? "Phone Mic" : "Windows Mic";
            TimeSpan elapsed = TimeSpan.FromSeconds(state.CapturedFrames / (double)LooperRecordService.SampleRate);
            LayerRecordStatusText.Text = $"RECORDING {elapsed:m\\:ss\\.ff} · {source} · {_recordingLayer.RecordMode} · processed Voice · live mic monitor OFF";

            if (_recordingLayer.RecordMode == LooperLayerRecordMode.OneCycle &&
                viewModel.Master is { FrameCount: > 0 } master &&
                state.CapturedFrames >= (ulong)master.FrameCount)
            {
                _ = FinishLayerRecordingAsync(_recordingLayer, stopTransportAfter: false);
                return;
            }

            if (state.SourceChanged != 0U || state.Active == 0U)
            {
                _ = FinishLayerRecordingAsync(_recordingLayer, stopTransportAfter: false);
            }
            return;
        }

        string firstSource = state.SourceMode == (uint)LooperRecordSourceMode.Remote ? "Phone Mic" : "Windows Mic";
        TimeSpan firstElapsed = TimeSpan.FromSeconds(state.CapturedFrames / (double)LooperRecordService.SampleRate);
        RecordStatusText.Text = $"Recording {firstElapsed:m\\:ss\\.f} · {firstSource} · processed Voice FX printed · live mic monitor OFF";

        if (state.SourceChanged != 0U || state.Active == 0U)
        {
            _ = FinishFirstLoopRecordingAsync(viewModel);
        }
    }

    private async void OnLayerRecordClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LooperViewModel viewModel || _selectedTrack is null || viewModel.Master is null || _finishingRecord) return;

        if (_layerRecordState == LooperLayerRecordState.Armed)
        {
            CancelLayerArm("Layer arm cancelled before the boundary.");
            return;
        }
        if (_layerRecordState == LooperLayerRecordState.Recording)
        {
            await FinishLayerRecordingAsync(_recordingLayer ?? _selectedTrack, stopTransportAfter: false);
            return;
        }
        if (!viewModel.EngineReady)
        {
            LayerRecordStatusText.Text = "Start the normal GrassiBoard audio engine before recording a child layer.";
            return;
        }
        if (!EnsureNativeLayerGraph(viewModel)) return;

        NativeAudioEngine? engine = NativeAudioEngine.FindRunningProcessEngine();
        if (engine is null || engine.GetLooperState(out LooperNativeState state) != NativeResult.Ok)
        {
            LayerRecordStatusText.Text = "Native Looper state is unavailable.";
            return;
        }

        bool midCycle = state.PlayheadFrame > ArmBoundaryWindowFrames && state.LoopFrames > 0U;
        if (state.Transport == (uint)LooperTransportState.Playing && midCycle)
        {
            ArmLayer(_selectedTrack, state.PlayheadFrame);
            return;
        }

        if (state.Transport == (uint)LooperTransportState.Paused && midCycle)
        {
            viewModel.TransportPlayPauseCommand.Execute(null);
            ArmLayer(_selectedTrack, state.PlayheadFrame);
            return;
        }

        await StartLayerRecordingAsync(viewModel, _selectedTrack);
    }

    private void ArmLayer(LooperTrackModel track, ulong playhead)
    {
        _armedTrack = track;
        _armedPreviousPlayhead = playhead;
        _layerRecordState = LooperLayerRecordState.Armed;
        _armTimer.Start();
        LayerRecordStatusText.Text = $"ARMED · {track.Name} will start recording at the next Master boundary. Press Record Layer again to cancel.";
        UpdateLayerUi();
    }

    private async void OnArmTimerTick(object? sender, EventArgs e)
    {
        if (_layerRecordState != LooperLayerRecordState.Armed || _armedTrack is null || DataContext is not LooperViewModel viewModel)
        {
            _armTimer.Stop();
            return;
        }

        NativeAudioEngine? engine = NativeAudioEngine.FindRunningProcessEngine();
        if (engine is null || engine.GetLooperState(out LooperNativeState state) != NativeResult.Ok)
        {
            CancelLayerArm("Audio engine stopped while the layer was armed.");
            return;
        }

        if (state.Transport != (uint)LooperTransportState.Playing)
        {
            return;
        }

        bool wrapped = state.PlayheadFrame < _armedPreviousPlayhead;
        bool atBoundary = state.PlayheadFrame <= ArmBoundaryWindowFrames && (_armedPreviousPlayhead > ArmBoundaryWindowFrames || wrapped);
        _armedPreviousPlayhead = state.PlayheadFrame;
        if (!atBoundary) return;

        LooperTrackModel track = _armedTrack;
        _armTimer.Stop();
        _armedTrack = null;
        await StartLayerRecordingAsync(viewModel, track);
    }

    private async Task StartLayerRecordingAsync(LooperViewModel viewModel, LooperTrackModel track)
    {
        if (_recordService.IsRecording || viewModel.Master is null) return;
        if (!EnsureNativeLayerGraph(viewModel)) return;

        bool started = await _recordService.StartAsync();
        if (!started)
        {
            _layerRecordState = LooperLayerRecordState.Idle;
            LayerRecordStatusText.Text = _recordService.LastError;
            UpdateLayerUi();
            return;
        }

        track.UndoSamples = track.Samples.ToArray();
        _recordingLayer = track;
        _layerRecordState = LooperLayerRecordState.Recording;
        _armedTrack = null;
        _armTimer.Stop();

        NativeAudioEngine? engine = NativeAudioEngine.FindRunningProcessEngine();
        if (engine is not null && engine.GetLooperState(out LooperNativeState state) == NativeResult.Ok &&
            state.Transport != (uint)LooperTransportState.Playing)
        {
            viewModel.TransportPlayPauseCommand.Execute(null);
        }

        _recordTimer.Start();
        LayerRecordStatusText.Text = $"RECORDING · {track.Name} · {track.RecordMode} · processed Voice FX printed · live mic monitor OFF";
        UpdateLayerUi();
    }

    private async Task FinishLayerRecordingAsync(LooperTrackModel track, bool stopTransportAfter)
    {
        if (_finishingRecord || _layerRecordState != LooperLayerRecordState.Recording || DataContext is not LooperViewModel viewModel || viewModel.Master is null) return;
        _finishingRecord = true;
        _recordTimer.Stop();
        try
        {
            LooperRecordedTake? take = await _recordService.StopAsync();
            _layerRecordState = LooperLayerRecordState.Idle;
            _recordingLayer = null;
            if (take is null)
            {
                track.UndoSamples = null;
                LayerRecordStatusText.Text = _recordService.LastError;
                UpdateLayerUi();
                if (stopTransportAfter) viewModel.TransportStopCommand.Execute(null);
                return;
            }

            float[] previous = track.UndoSamples ?? track.Samples.ToArray();
            float[] composed = LooperLayerComposer.Compose(track.RecordMode, previous, take.StereoSamples, viewModel.Master.FrameCount);
            NativeAudioEngine? engine = NativeAudioEngine.FindRunningProcessEngine();
            if (engine is null)
            {
                track.UndoSamples = null;
                LayerRecordStatusText.Text = "Audio engine stopped before the child layer could be committed.";
                UpdateLayerUi();
                return;
            }

            NativeResult result = engine.SetLooperTrackAudio(track.NativeTrackId, composed, viewModel.Master.FrameCount);
            if (result != NativeResult.Ok)
            {
                track.UndoSamples = null;
                LayerRecordStatusText.Text = $"Native child Track commit failed ({result}); the previous layer was preserved.";
                UpdateLayerUi();
                return;
            }

            engine.SetLooperTrackMix(track.NativeTrackId, (float)track.Gain, (float)track.Pan, track.Muted, track.Solo);
            track.UndoSamples = previous;
            track.Samples = composed;
            track.Waveform = _waveformAnalysisService.BuildEnvelope(composed, channels: 1, preferredBucketCount: 4_096);
            track.ActiveStartFrame = 0L;
            track.ActiveEndFrame = viewModel.Master.FrameCount;
            track.HasRecordedAudio = true;
            viewModel.Project.ModifiedAtUtc = DateTimeOffset.UtcNow;
            LayerUndoButton.IsEnabled = true;

            string source = take.SourceMode == LooperRecordSourceMode.Remote ? "Phone Mic" : "Windows Mic";
            LayerRecordStatusText.Text = $"{track.RecordMode} committed · {source} · exactly {viewModel.Master.FrameCount:N0} Master-aligned frames.";
            _nativeLayerGraphDirty = false;
            UpdateLayerUi();
            if (stopTransportAfter) viewModel.TransportStopCommand.Execute(null);
        }
        finally
        {
            _finishingRecord = false;
        }
    }

    private void OnLayerCancelClick(object sender, RoutedEventArgs e)
    {
        if (_layerRecordState == LooperLayerRecordState.Armed)
        {
            CancelLayerArm("Armed layer cancelled; nothing was recorded.");
            return;
        }
        if (_layerRecordState != LooperLayerRecordState.Recording || _recordingLayer is null) return;

        LooperTrackModel track = _recordingLayer;
        _recordService.Cancel();
        _recordTimer.Stop();
        track.UndoSamples = null;
        _recordingLayer = null;
        _layerRecordState = LooperLayerRecordState.Idle;
        LayerRecordStatusText.Text = "Take discarded. The pre-record layer is unchanged.";
        UpdateLayerUi();
    }

    private void CancelLayerArm(string message)
    {
        _armTimer.Stop();
        _armedTrack = null;
        _layerRecordState = LooperLayerRecordState.Idle;
        LayerRecordStatusText.Text = message;
        UpdateLayerUi();
    }

    private void OnLayerUndoClick(object sender, RoutedEventArgs e)
    {
        if (_layerRecordState != LooperLayerRecordState.Idle || _selectedTrack?.UndoSamples is not { } undo || DataContext is not LooperViewModel viewModel || viewModel.Master is null) return;
        NativeAudioEngine? engine = NativeAudioEngine.FindRunningProcessEngine();
        if (engine is null)
        {
            LayerRecordStatusText.Text = "Start the audio engine before Undo so native playback stays synchronized.";
            return;
        }
        NativeResult result = engine.SetLooperTrackAudio(_selectedTrack.NativeTrackId, undo, viewModel.Master.FrameCount);
        if (result != NativeResult.Ok)
        {
            LayerRecordStatusText.Text = $"Undo failed ({result}).";
            return;
        }
        _selectedTrack.Samples = undo;
        _selectedTrack.Waveform = _waveformAnalysisService.BuildEnvelope(undo, channels: 1, preferredBucketCount: 4_096);
        _selectedTrack.UndoSamples = null;
        _selectedTrack.HasRecordedAudio = undo.Any(sample => Math.Abs(sample) > 1.0e-7F);
        LayerRecordStatusText.Text = $"Undo restored {_selectedTrack.Name} to its pre-record state.";
        UpdateLayerUi();
    }

    private void OnAddLayerClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LooperViewModel viewModel || viewModel.Master is null || _layerRecordState != LooperLayerRecordState.Idle) return;
        if (viewModel.Project.Tracks.Count >= LooperProjectStore.MaxTracks)
        {
            LayerRecordStatusText.Text = $"Gate 4 safety ceiling reached: {LooperProjectStore.MaxTracks} child layers.";
            return;
        }

        long bytesAfterAdd = checked((viewModel.Project.Tracks.Count + 1L) * viewModel.Master.FrameCount * sizeof(float));
        if (bytesAfterAdd > MaxManagedChildTrackBytes)
        {
            LayerRecordStatusText.Text = "Child-layer memory safety ceiling reached for this Master length (256 MiB managed PCM).";
            return;
        }

        uint trackId = _nextNativeTrackId++;
        int frameCount = checked((int)viewModel.Master.FrameCount);
        var track = new LooperTrackModel
        {
            NativeTrackId = trackId,
            Name = $"Layer {viewModel.Project.Tracks.Count + 1}",
            DisplayColor = LayerPalette[viewModel.Project.Tracks.Count % LayerPalette.Length],
            Samples = new float[frameCount],
            ActiveStartFrame = 0L,
            ActiveEndFrame = viewModel.Master.FrameCount,
            RecordMode = LooperLayerRecordMode.OneCycle
        };
        track.PropertyChanged += OnTrackPropertyChanged;
        viewModel.Project.Tracks.Add(track);
        viewModel.Project.ModifiedAtUtc = DateTimeOffset.UtcNow;
        _nativeLayerGraphDirty = true;
        _selectedTrack = track;
        TrackListBox.SelectedItem = track;
        LayerModeCombo.SelectedIndex = 0;
        EnsureNativeLayerGraph(viewModel);
        LayerRecordStatusText.Text = $"{track.Name} ready. Record while stopped for frame 0, or while playing to arm the next boundary.";
        UpdateLayerUi();
    }

    private void OnDeleteLayerClick(object sender, RoutedEventArgs e)
    {
        if (_selectedTrack is null || DataContext is not LooperViewModel viewModel || _layerRecordState != LooperLayerRecordState.Idle) return;
        LooperTrackModel doomed = _selectedTrack;
        NativeAudioEngine? engine = NativeAudioEngine.FindRunningProcessEngine();
        if (engine is not null) engine.RemoveLooperTrack(doomed.NativeTrackId);
        doomed.PropertyChanged -= OnTrackPropertyChanged;
        int index = viewModel.Project.Tracks.IndexOf(doomed);
        viewModel.Project.Tracks.Remove(doomed);
        viewModel.Project.ModifiedAtUtc = DateTimeOffset.UtcNow;
        _selectedTrack = viewModel.Project.Tracks.Count == 0
            ? null
            : viewModel.Project.Tracks[Math.Clamp(index, 0, viewModel.Project.Tracks.Count - 1)];
        TrackListBox.SelectedItem = _selectedTrack;
        LayerRecordStatusText.Text = $"{doomed.Name} deleted. Master length can be edited again after every child layer is removed.";
        UpdateLayerUi();
    }

    private void OnTrackSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_layerRecordState != LooperLayerRecordState.Idle)
        {
            TrackListBox.SelectedItem = _recordingLayer ?? _armedTrack ?? _selectedTrack;
            return;
        }
        _selectedTrack = TrackListBox.SelectedItem as LooperTrackModel;
        if (_selectedTrack is not null) LayerModeCombo.SelectedIndex = (int)_selectedTrack.RecordMode;
        UpdateLayerUi();
    }

    private void OnLayerModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selectedTrack is null || LayerModeCombo.SelectedIndex < 0 || _layerRecordState != LooperLayerRecordState.Idle) return;
        _selectedTrack.RecordMode = (LooperLayerRecordMode)LayerModeCombo.SelectedIndex;
        UpdateLayerUi();
    }

    private void OnTrackPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LooperTrackModel track || _nativeLayerGraphDirty) return;
        if (e.PropertyName is not (nameof(LooperTrackModel.Muted) or nameof(LooperTrackModel.Solo) or nameof(LooperTrackModel.Gain) or nameof(LooperTrackModel.Pan))) return;
        NativeAudioEngine? engine = NativeAudioEngine.FindRunningProcessEngine();
        engine?.SetLooperTrackMix(track.NativeTrackId, (float)track.Gain, (float)track.Pan, track.Muted, track.Solo);
    }

    private bool EnsureNativeLayerGraph(LooperViewModel viewModel)
    {
        if (viewModel.Master is null) return false;
        NativeAudioEngine? engine = NativeAudioEngine.FindRunningProcessEngine();
        if (engine is null)
        {
            _nativeLayerGraphDirty = true;
            LayerRecordStatusText.Text = "Start the normal GrassiBoard audio engine to activate child-layer playback.";
            return false;
        }

        if (engine.GetLooperState(out LooperNativeState state) != NativeResult.Ok || state.LoopFrames != (ulong)viewModel.Master.FrameCount)
        {
            NativeResult masterResult = engine.LoadLooperMaster(viewModel.Master.Samples, 0L, viewModel.Master.FrameCount);
            if (masterResult != NativeResult.Ok)
            {
                LayerRecordStatusText.Text = $"Could not prepare native Master for child layers ({masterResult}).";
                return false;
            }
        }

        if (!_nativeLayerGraphDirty) return true;
        foreach (LooperTrackModel track in viewModel.Project.Tracks)
        {
            NativeResult result = engine.SetLooperTrackAudio(track.NativeTrackId, track.Samples, viewModel.Master.FrameCount);
            if (result != NativeResult.Ok)
            {
                LayerRecordStatusText.Text = $"Could not synchronize {track.Name} to native Looper ({result}).";
                return false;
            }
            engine.SetLooperTrackMix(track.NativeTrackId, (float)track.Gain, (float)track.Pan, track.Muted, track.Solo);
        }
        _nativeLayerGraphDirty = false;
        return true;
    }

    private async void OnTransportStopClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LooperViewModel viewModel) return;
        if (_layerRecordState == LooperLayerRecordState.Armed) CancelLayerArm("Armed layer cancelled by Stop.");
        if (_layerRecordState == LooperLayerRecordState.Recording && _recordingLayer is not null)
        {
            await FinishLayerRecordingAsync(_recordingLayer, stopTransportAfter: true);
            return;
        }
        viewModel.TransportStopCommand.Execute(null);
    }

    private void OnTransportPlayPauseClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LooperViewModel viewModel) return;
        if (_layerRecordState == LooperLayerRecordState.Recording)
        {
            LayerRecordStatusText.Text = "Pause is locked during an active Take. Stop/commit or Cancel/Discard the Take first.";
            return;
        }
        if (!EnsureNativeLayerGraph(viewModel) && viewModel.Project.Tracks.Count > 0) return;
        viewModel.TransportPlayPauseCommand.Execute(null);
    }

    private void OnNewProjectClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LooperViewModel viewModel) return;
        if (_recordService.IsRecording) _recordService.Cancel();
        _recordTimer.Stop();
        _armTimer.Stop();
        foreach (LooperTrackModel track in viewModel.Project.Tracks) track.PropertyChanged -= OnTrackPropertyChanged;
        _selectedTrack = null;
        _armedTrack = null;
        _recordingLayer = null;
        _layerRecordState = LooperLayerRecordState.Idle;
        _nativeLayerGraphDirty = true;
        viewModel.NewProjectCommand.Execute(null);
        UpdateLayerUi();
    }

    private void UpdateLayerUi()
    {
        bool hasTrack = _selectedTrack is not null;
        ActiveLayerNameText.Text = _selectedTrack?.Name ?? "No layer selected";
        LayerRecordButton.IsEnabled = hasTrack && !_finishingRecord;
        LayerRecordButton.Content = _layerRecordState switch
        {
            LooperLayerRecordState.Armed => "Cancel Arm",
            LooperLayerRecordState.Recording => "Stop Layer",
            _ => "Record Layer"
        };
        LayerCancelButton.IsEnabled = _layerRecordState is LooperLayerRecordState.Armed or LooperLayerRecordState.Recording;
        LayerUndoButton.IsEnabled = _layerRecordState == LooperLayerRecordState.Idle && _selectedTrack?.UndoSamples is not null;
        DeleteLayerButton.IsEnabled = hasTrack && _layerRecordState == LooperLayerRecordState.Idle;
        AddLayerButton.IsEnabled = _layerRecordState == LooperLayerRecordState.Idle;
        LayerModeCombo.IsEnabled = hasTrack && _layerRecordState == LooperLayerRecordState.Idle;
        TrackListBox.IsEnabled = _layerRecordState == LooperLayerRecordState.Idle;

        if (DataContext is LooperViewModel viewModel)
        {
            bool canRedefineMaster = viewModel.Project.Tracks.Count == 0 && _layerRecordState == LooperLayerRecordState.Idle;
            EditMasterButton.IsEnabled = canRedefineMaster;
            ImportDifferentMasterButton.IsEnabled = canRedefineMaster;
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

    private void ResetFirstRecordUi()
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
        if (_layerRecordState != LooperLayerRecordState.Idle)
        {
            LayerRecordStatusText.Text = "Finish or discard the active layer Take before changing monitor output.";
            SyncMonitorSelection();
            return;
        }
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
            _armTimer.Stop();
            _armedTrack = null;
            _layerRecordState = LooperLayerRecordState.Idle;
            if (_recordService.IsRecording)
            {
                _recordService.Cancel();
                _recordingLayer?.UndoSamples = null;
                _recordingLayer = null;
                ResetFirstRecordUi();
            }
            UpdateLayerUi();
            if (DataContext is LooperViewModel viewModel) viewModel.OnWorkspaceHidden();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        IsVisibleChanged -= OnIsVisibleChanged;
        Unloaded -= OnUnloaded;
        _recordTimer.Stop();
        _armTimer.Stop();
        _recordService.Dispose();
        if (DataContext is LooperViewModel viewModel)
        {
            foreach (LooperTrackModel track in viewModel.Project.Tracks) track.PropertyChanged -= OnTrackPropertyChanged;
        }
        if (_mainViewModel is not null) _mainViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
        _mainViewModel = null;
        if (DataContext is IDisposable disposable) disposable.Dispose();
    }
}
