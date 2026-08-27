using System.IO;
using System.Windows.Threading;
using GrassiBoard.Infrastructure;
using GrassiBoard.Models.Looper;
using GrassiBoard.Services;
using GrassiBoard.Services.Looper;
using Microsoft.Win32;

namespace GrassiBoard.ViewModels;

internal sealed class LooperViewModel : ObservableObject, IDisposable
{
    private enum PlaybackContext
    {
        None,
        PendingSelection,
        Master
    }

    private const int EditorWaveformBuckets = 65_536;

    private readonly LooperProjectStore _projectStore;
    private readonly WaveformAnalysisService _analysisService;
    private readonly LooperMonitorService _monitorService;
    private readonly Func<string?> _monitorDeviceNameProvider;
    private readonly DispatcherTimer _transportTimer;
    private readonly DispatcherTimer _selectionRestartTimer;
    private WaveformAnalysisResult? _pendingImport;
    private string _pendingSourcePath = string.Empty;
    private PlaybackContext _playbackContext;
    private LooperNativeState _nativeState;
    private bool _selectionRestartShouldPlay;
    private bool _editingMaster;
    private double _selectionStart;
    private double _selectionEnd = 1.0;
    private double _pendingPlayheadPosition;
    private double _masterPlayheadPosition;
    private bool _lastEngineReady;
    private bool _busy;
    private bool _disposed;
    private string _statusMessage = "Create a Master Loop to start the project.";

    public LooperViewModel(
        LooperProjectStore? projectStore = null,
        WaveformAnalysisService? analysisService = null,
        Func<string?>? monitorDeviceIdProvider = null,
        Func<string?>? monitorDeviceNameProvider = null,
        LooperMonitorService? monitorService = null)
    {
        _projectStore = projectStore ?? new LooperProjectStore();
        _analysisService = analysisService ?? new WaveformAnalysisService();
        _monitorDeviceNameProvider = monitorDeviceNameProvider ?? (() => null);
        _monitorService = monitorService ?? new LooperMonitorService(monitorDeviceIdProvider);

        ImportAudioCommand = new AsyncRelayCommand(_ => ChooseImportAsync(), _ => CanImportAudio);
        EditMasterCommand = new AsyncRelayCommand(_ => EditMasterAsync(), _ => CanEditMaster);
        SetAsMasterCommand = new RelayCommand(_ => SetAsMaster(), _ => _pendingImport is not null && !IsBusy);
        CancelImportCommand = new RelayCommand(_ => CancelImport(), _ => _pendingImport is not null && !IsBusy);
        NewProjectCommand = new RelayCommand(_ => NewProject(), _ => !IsBusy);
        AuditionPlayPauseCommand = new RelayCommand(_ => ToggleAudition(), _ => CanAuditionSelection);
        AuditionSeekCommand = new RelayCommand(SeekPendingSelection, _ => CanAuditionSelection);
        TransportPlayPauseCommand = new RelayCommand(_ => ToggleMasterPlayback(), _ => CanUseMasterTransport);
        TransportStopCommand = new RelayCommand(_ => StopPlayback(), _ => HasMaster && !IsBusy);

        _transportTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(33), DispatcherPriority.Background, OnTransportTick, Dispatcher.CurrentDispatcher);
        _transportTimer.Start();
        _selectionRestartTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(85), DispatcherPriority.Background, OnSelectionRestartTick, Dispatcher.CurrentDispatcher);
        _selectionRestartTimer.Stop();
        _lastEngineReady = _monitorService.IsProgramEngineRunning;
    }

    public AsyncRelayCommand ImportAudioCommand { get; }
    public AsyncRelayCommand EditMasterCommand { get; }
    public RelayCommand SetAsMasterCommand { get; }
    public RelayCommand CancelImportCommand { get; }
    public RelayCommand NewProjectCommand { get; }
    public RelayCommand AuditionPlayPauseCommand { get; }
    public RelayCommand AuditionSeekCommand { get; }
    public RelayCommand TransportPlayPauseCommand { get; }
    public RelayCommand TransportStopCommand { get; }

    public LooperProjectModel Project => _projectStore.Current;
    public LooperMasterModel? Master => Project.Master;
    public bool HasMaster => Master is not null;
    public bool HasPendingImport => _pendingImport is not null;
    public bool IsEmptyProject => !HasMaster && !HasPendingImport;
    public bool HasChildTracks => Project.Tracks.Count > 0;
    public bool IsEditingMaster => _editingMaster;
    public bool EngineReady => _monitorService.IsProgramEngineRunning;
    public bool CanImportAudio => !IsBusy && Project.CanRedefineMaster;
    public bool CanEditMaster => Master is not null && !IsBusy && Project.CanRedefineMaster;
    public bool CanAuditionSelection => _pendingImport is not null && !IsBusy && EngineReady;
    public bool CanUseMasterTransport => Master is not null && !IsBusy && EngineReady;
    public WaveformEnvelope PendingWaveform => _pendingImport?.Envelope ?? WaveformEnvelope.Empty;
    public string PendingFileName { get; private set; } = string.Empty;
    public string PendingDurationLabel => _pendingImport is null ? string.Empty : FormatTime(_pendingImport.Duration);
    public string MasterDurationLabel => Master is null ? string.Empty : FormatTime(Master.Duration);
    public string MasterFrameLabel => Master is null ? string.Empty : $"{Master.FrameCount:N0} frames @ {Master.SampleRate:N0} Hz";
    public bool IsMasterPlaying => _playbackContext == PlaybackContext.Master &&
        _nativeState.Transport == (uint)LooperTransportState.Playing;
    public bool IsAuditionPlaying => _playbackContext == PlaybackContext.PendingSelection &&
        _nativeState.Transport == (uint)LooperTransportState.Playing;
    public string TransportPlayPauseLabel => IsMasterPlaying ? "Pause" : "Play";
    public string AuditionPlayPauseLabel => IsAuditionPlaying ? "Pause" : "Play selection";
    public string SetMasterActionLabel => IsEditingMaster ? "Apply Master Changes" : "Set As Master Loop";
    public double PendingPlayheadPosition { get => _pendingPlayheadPosition; private set => SetProperty(ref _pendingPlayheadPosition, value); }
    public double MasterPlayheadPosition { get => _masterPlayheadPosition; private set => SetProperty(ref _masterPlayheadPosition, value); }
    public string MasterPositionLabel => Master is null
        ? string.Empty
        : $"{FormatSeconds(_nativeState.PlayheadFrame / (double)LooperMonitorService.SampleRate)} / {MasterDurationLabel}";
    public string EngineRequirementLabel => EngineReady
        ? "Program engine: Running · native Looper clock active"
        : "Program engine: Stopped · start GrassiBoard before Play";
    public string MonitorRouteLabel
    {
        get
        {
            string? sharedName = _monitorDeviceNameProvider()?.Trim();
            return $"Local monitor: {(string.IsNullOrWhiteSpace(sharedName) ? _monitorService.MonitorDeviceName : sharedName)}";
        }
    }
    public string LoopSafetyLabel => Master is null
        ? $"Hard safety limit: {LooperMonitorService.MaxSupportedLoopMinutes} min · 48 kHz stereo float"
        : $"Master memory: {FormatMebibytes(Master.FrameCount * 2L * sizeof(float))} managed + same native copy · hard max {LooperMonitorService.MaxSupportedLoopMinutes} min";
    public string MonitorDiagnosticsLabel =>
        $"Local monitor corrections {_monitorService.LocalDriftCorrectionCount:N0} · underruns {_monitorService.LocalUnderrunCount:N0}";
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public bool IsBusy
    {
        get => _busy;
        private set
        {
            if (!SetProperty(ref _busy, value)) return;
            RaiseAvailabilityProperties();
        }
    }

    public double SelectionStart
    {
        get => _selectionStart;
        set
        {
            double maximum = Math.Max(0.0, SelectionEnd - MinimumSelectionWidth);
            if (SetProperty(ref _selectionStart, Math.Clamp(double.IsFinite(value) ? value : 0.0, 0.0, maximum)))
            {
                OnSelectionChanged();
            }
        }
    }

    public double SelectionEnd
    {
        get => _selectionEnd;
        set
        {
            double minimum = Math.Min(1.0, SelectionStart + MinimumSelectionWidth);
            if (SetProperty(ref _selectionEnd, Math.Clamp(double.IsFinite(value) ? value : 1.0, minimum, 1.0)))
            {
                OnSelectionChanged();
            }
        }
    }

    public string SelectionLabel
    {
        get
        {
            if (_pendingImport is null) return string.Empty;
            double total = _pendingImport.Duration.TotalSeconds;
            return $"{FormatSeconds(SelectionStart * total)} → {FormatSeconds(SelectionEnd * total)}  ·  {FormatSeconds((SelectionEnd - SelectionStart) * total)} selected";
        }
    }

    private double MinimumSelectionWidth => _pendingImport is { FrameCount: > 0 }
        ? Math.Min(0.01, 1.0 / _pendingImport.FrameCount)
        : 0.001;

    public async Task LoadImportedAudioAsync(string path, CancellationToken cancellationToken = default)
    {
        StopPlayback(clearSource: true);
        IsBusy = true;
        StatusMessage = "Analyzing waveform…";
        try
        {
            WaveformAnalysisResult result = await _analysisService.AnalyzeFileAsync(path, EditorWaveformBuckets, cancellationToken);
            _pendingImport = result;
            _pendingSourcePath = path;
            PendingFileName = Path.GetFileName(path);
            _editingMaster = false;
            _selectionStart = 0.0;
            _selectionEnd = 1.0;
            PendingPlayheadPosition = 0.0;
            RaisePendingProperties();
            StatusMessage = "Drag START / END, click or drag the white playhead to audition any point, then set it as Master.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or InvalidDataException)
        {
            StatusMessage = exception.Message;
            throw;
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private async Task EditMasterAsync()
    {
        LooperMasterModel? master = Master;
        if (master is null || !Project.CanRedefineMaster) return;
        if (string.IsNullOrWhiteSpace(master.SourcePath) || !File.Exists(master.SourcePath))
        {
            StatusMessage = "The original Master source file is missing. Reconnect the source file before editing its trim.";
            return;
        }

        StopPlayback(clearSource: true);
        IsBusy = true;
        StatusMessage = "Opening the original Master source…";
        try
        {
            WaveformAnalysisResult result = await _analysisService.AnalyzeFileAsync(master.SourcePath, EditorWaveformBuckets);
            _pendingImport = result;
            _pendingSourcePath = master.SourcePath;
            PendingFileName = master.SourceFileName;
            _editingMaster = true;
            _selectionStart = Math.Clamp(master.SourceStartFrame / (double)Math.Max(1L, result.FrameCount), 0.0, 1.0);
            _selectionEnd = Math.Clamp(master.SourceEndFrame / (double)Math.Max(1L, result.FrameCount), _selectionStart + MinimumSelectionWidth, 1.0);
            PendingPlayheadPosition = _selectionStart;
            RaisePendingProperties();
            StatusMessage = "Editing Master trim. Cancel keeps the current Master unchanged; Apply commits the new START / END.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or InvalidDataException)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    internal void SetAsMaster()
    {
        if (_pendingImport is null) return;
        StopPlayback(clearSource: true);
        (long startFrame, long endFrame) = GetSelectionFrames();
        long selectedFrameCount = endFrame - startFrame;
        if (selectedFrameCount > LooperMonitorService.MaxSupportedLoopFrames)
        {
            StatusMessage = $"Master selection exceeds the {LooperMonitorService.MaxSupportedLoopMinutes}-minute safety limit.";
            return;
        }

        int startSample = checked((int)(startFrame * WaveformAnalysisService.TargetChannels));
        int sampleCount = checked((int)(selectedFrameCount * WaveformAnalysisService.TargetChannels));
        float[] selectedSamples = new float[sampleCount];
        Array.Copy(_pendingImport.Samples, startSample, selectedSamples, 0, sampleCount);

        var master = new LooperMasterModel
        {
            SourceFileName = PendingFileName,
            SourcePath = _pendingSourcePath,
            SourceStartFrame = startFrame,
            SourceEndFrame = endFrame,
            FrameCount = selectedFrameCount,
            Samples = selectedSamples,
            Waveform = _analysisService.BuildEnvelope(selectedSamples)
        };
        _projectStore.SetMaster(master);
        bool wasEditing = _editingMaster;
        ClearPendingEditor();
        MasterPlayheadPosition = 0.0;
        RaiseMasterProperties();
        StatusMessage = EngineReady
            ? (wasEditing ? "Master trim updated. Native Gate 2 transport is ready." : "Master Loop is defined. Native Gate 2 transport is ready.")
            : "Master Loop is defined. Start the GrassiBoard engine to use native transport.";
        RaiseAvailabilityProperties();
    }

    private async Task ChooseImportAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Master audio",
            Filter = "Audio files (*.wav;*.mp3)|*.wav;*.mp3|WAV files (*.wav)|*.wav|MP3 files (*.mp3)|*.mp3",
            Multiselect = false,
            CheckFileExists = true
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            await LoadImportedAudioAsync(dialog.FileName);
        }
        catch
        {
        }
    }

    private void CancelImport()
    {
        if (_playbackContext == PlaybackContext.PendingSelection) StopPlayback(clearSource: true);
        bool wasEditing = _editingMaster;
        ClearPendingEditor();
        StatusMessage = HasMaster
            ? (wasEditing ? "Master edit cancelled; current Master is unchanged." : "Master Loop unchanged.")
            : "Create a Master Loop to start the project.";
        RaiseAvailabilityProperties();
    }

    private void ClearPendingEditor()
    {
        _pendingImport = null;
        _pendingSourcePath = string.Empty;
        PendingFileName = string.Empty;
        _editingMaster = false;
        PendingPlayheadPosition = 0.0;
        RaisePendingProperties();
    }

    private void NewProject()
    {
        StopPlayback(clearSource: true);
        _projectStore.Reset();
        _pendingImport = null;
        _pendingSourcePath = string.Empty;
        PendingFileName = string.Empty;
        _editingMaster = false;
        _selectionStart = 0.0;
        _selectionEnd = 1.0;
        PendingPlayheadPosition = 0.0;
        MasterPlayheadPosition = 0.0;
        RaiseMasterProperties();
        RaisePendingProperties();
        StatusMessage = "Create a Master Loop to start the project.";
        RaiseAvailabilityProperties();
    }

    private void ToggleAudition()
    {
        if (_pendingImport is null) return;
        _selectionRestartTimer.Stop();
        if (_playbackContext == PlaybackContext.PendingSelection && IsAuditionPlaying)
        {
            _monitorService.Pause();
            RefreshNativeState();
            PendingPlayheadPosition = SelectionStart +
                (_pendingImport.FrameCount == 0L ? 0.0 : _nativeState.PlayheadFrame / (double)_pendingImport.FrameCount);
            StatusMessage = "Selection preview paused.";
            RaiseTransportProperties();
            return;
        }

        if (_playbackContext != PlaybackContext.PendingSelection || _nativeState.LoopFrames == 0U)
        {
            if (!ConfigurePendingSelection()) return;
        }
        if (_monitorService.Play())
        {
            StatusMessage = $"Looping the selected range on {CurrentMonitorName}.";
        }
        else
        {
            StatusMessage = _monitorService.LastError;
        }
        RefreshNativeState();
        RaiseTransportProperties();
    }

    private void SeekPendingSelection(object? parameter)
    {
        if (_pendingImport is null || !TryReadNormalized(parameter, out double normalized)) return;
        _selectionRestartTimer.Stop();
        _selectionRestartShouldPlay = false;

        bool resume = IsAuditionPlaying;
        if (_playbackContext != PlaybackContext.PendingSelection || _nativeState.LoopFrames == 0U)
        {
            if (!ConfigurePendingSelection()) return;
            resume = false;
        }

        (long startFrame, long endFrame) = GetSelectionFrames();
        long sourceFrame = Math.Clamp(
            (long)Math.Floor(Math.Clamp(normalized, SelectionStart, SelectionEnd) * _pendingImport.FrameCount),
            startFrame,
            endFrame - 1L);
        ulong relativeFrame = checked((ulong)(sourceFrame - startFrame));
        NativeAudioEngine? engine = NativeAudioEngine.FindRunningProcessEngine();
        if (engine is null)
        {
            StatusMessage = "Program engine stopped before seek.";
            return;
        }

        _monitorService.Pause();
        NativeResult result = engine.SeekLooper(relativeFrame);
        if (result != NativeResult.Ok)
        {
            StatusMessage = $"Looper seek failed ({result}).";
            return;
        }
        PendingPlayheadPosition = sourceFrame / (double)_pendingImport.FrameCount;
        if (resume && !_monitorService.Play())
        {
            StatusMessage = _monitorService.LastError;
        }
        else
        {
            StatusMessage = $"Selection playhead moved to {FormatSeconds(sourceFrame / (double)LooperMonitorService.SampleRate)}.";
        }
        RefreshNativeState();
        RaiseTransportProperties();
    }

    private void ToggleMasterPlayback()
    {
        if (Master is null) return;
        if (_playbackContext == PlaybackContext.Master && IsMasterPlaying)
        {
            _monitorService.Pause();
            RefreshNativeState();
            StatusMessage = "Master Loop paused.";
            RaiseTransportProperties();
            return;
        }

        if (_playbackContext != PlaybackContext.Master || _nativeState.LoopFrames != (ulong)Master.FrameCount)
        {
            if (!ConfigureMaster()) return;
        }
        if (_monitorService.Play())
        {
            StatusMessage = $"Master Loop playing on {CurrentMonitorName}.";
        }
        else
        {
            StatusMessage = _monitorService.LastError;
        }
        RefreshNativeState();
        RaiseTransportProperties();
    }

    private void StopPlayback(bool clearSource = false)
    {
        _selectionRestartTimer.Stop();
        _selectionRestartShouldPlay = false;
        _monitorService.Stop();
        PendingPlayheadPosition = _pendingImport is null ? 0.0 : SelectionStart;
        MasterPlayheadPosition = 0.0;
        if (clearSource)
        {
            _monitorService.Clear();
            _playbackContext = PlaybackContext.None;
            _nativeState = default;
        }
        else
        {
            RefreshNativeState();
        }
        RaiseTransportProperties();
    }

    private bool ConfigurePendingSelection()
    {
        if (_pendingImport is null) return false;
        (long startFrame, long endFrame) = GetSelectionFrames();
        NativeResult result = _monitorService.ConfigureLoop(_pendingImport.Samples, startFrame, endFrame - startFrame);
        if (result != NativeResult.Ok)
        {
            StatusMessage = _monitorService.LastError;
            return false;
        }
        _playbackContext = PlaybackContext.PendingSelection;
        PendingPlayheadPosition = startFrame / (double)_pendingImport.FrameCount;
        MasterPlayheadPosition = 0.0;
        RefreshNativeState();
        RaiseTransportProperties();
        return true;
    }

    private bool ConfigureMaster()
    {
        if (Master is null) return false;
        NativeResult result = _monitorService.ConfigureLoop(Master.Samples, 0L, Master.FrameCount);
        if (result != NativeResult.Ok)
        {
            StatusMessage = _monitorService.LastError;
            return false;
        }
        _playbackContext = PlaybackContext.Master;
        MasterPlayheadPosition = 0.0;
        PendingPlayheadPosition = _pendingImport is null ? 0.0 : SelectionStart;
        RefreshNativeState();
        RaiseTransportProperties();
        return true;
    }

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectionLabel));
        PendingPlayheadPosition = SelectionStart;
        if (_pendingImport is null || _playbackContext != PlaybackContext.PendingSelection) return;

        _selectionRestartShouldPlay = IsAuditionPlaying || _selectionRestartShouldPlay;
        _monitorService.Stop();
        _nativeState = default;
        _selectionRestartTimer.Stop();
        _selectionRestartTimer.Start();
        RaiseTransportProperties();
    }

    private void OnSelectionRestartTick(object? sender, EventArgs e)
    {
        _selectionRestartTimer.Stop();
        if (_disposed || _pendingImport is null || _playbackContext != PlaybackContext.PendingSelection) return;
        bool resume = _selectionRestartShouldPlay;
        _selectionRestartShouldPlay = false;
        if (!ConfigurePendingSelection()) return;
        if (resume && !_monitorService.Play()) StatusMessage = _monitorService.LastError;
        RefreshNativeState();
        RaiseTransportProperties();
    }

    private void OnTransportTick(object? sender, EventArgs e)
    {
        if (_disposed) return;
        bool engineReady = _monitorService.IsProgramEngineRunning;
        if (engineReady != _lastEngineReady)
        {
            _lastEngineReady = engineReady;
            OnPropertyChanged(nameof(EngineReady));
            OnPropertyChanged(nameof(EngineRequirementLabel));
            RaiseAvailabilityProperties();
            if (!engineReady && _playbackContext != PlaybackContext.None)
            {
                _monitorService.Stop();
                _nativeState = default;
                MasterPlayheadPosition = 0.0;
                PendingPlayheadPosition = _pendingImport is null ? 0.0 : SelectionStart;
                StatusMessage = "Program engine stopped; Looper transport returned to a safe stopped state.";
            }
        }

        RefreshNativeState();
        if (_playbackContext == PlaybackContext.Master && Master is not null && _nativeState.LoopFrames > 0U)
        {
            MasterPlayheadPosition = Math.Clamp(_nativeState.PlayheadFrame / (double)_nativeState.LoopFrames, 0.0, 1.0);
        }
        else if (_playbackContext == PlaybackContext.PendingSelection && _pendingImport is not null && _nativeState.LoopFrames > 0U)
        {
            (long startFrame, _) = GetSelectionFrames();
            PendingPlayheadPosition = Math.Clamp(
                (startFrame + (long)_nativeState.PlayheadFrame) / (double)_pendingImport.FrameCount, 0.0, 1.0);
        }

        if (!string.IsNullOrWhiteSpace(_monitorService.LastError) &&
            !string.Equals(StatusMessage, _monitorService.LastError, StringComparison.Ordinal))
        {
            StatusMessage = _monitorService.LastError;
        }
        OnPropertyChanged(nameof(MonitorDiagnosticsLabel));
        OnPropertyChanged(nameof(MonitorRouteLabel));
        RaiseTransportProperties();
    }

    internal void OnMonitorOutputChanged()
    {
        if (_playbackContext != PlaybackContext.None) StopPlayback();
        OnPropertyChanged(nameof(MonitorRouteLabel));
        StatusMessage = $"Local monitor changed to {CurrentMonitorName}. Press Play to continue on the new output.";
    }

    private string CurrentMonitorName
    {
        get
        {
            string? name = _monitorDeviceNameProvider()?.Trim();
            return string.IsNullOrWhiteSpace(name) ? _monitorService.MonitorDeviceName : name;
        }
    }

    private void RefreshNativeState()
    {
        _nativeState = _monitorService.TryGetState(out LooperNativeState state) ? state : default;
    }

    private (long StartFrame, long EndFrame) GetSelectionFrames()
    {
        if (_pendingImport is null) return (0L, 0L);
        long totalFrames = _pendingImport.FrameCount;
        long startFrame = Math.Clamp((long)Math.Floor(SelectionStart * totalFrames), 0L, Math.Max(0L, totalFrames - 1L));
        long endFrame = Math.Clamp((long)Math.Ceiling(SelectionEnd * totalFrames), startFrame + 1L, totalFrames);
        return (startFrame, endFrame);
    }

    private static bool TryReadNormalized(object? value, out double normalized)
    {
        switch (value)
        {
            case double number when double.IsFinite(number):
                normalized = Math.Clamp(number, 0.0, 1.0);
                return true;
            case float number when float.IsFinite(number):
                normalized = Math.Clamp(number, 0.0F, 1.0F);
                return true;
            default:
                normalized = 0.0;
                return false;
        }
    }

    private void RaisePendingProperties()
    {
        OnPropertyChanged(nameof(PendingWaveform));
        OnPropertyChanged(nameof(PendingFileName));
        OnPropertyChanged(nameof(PendingDurationLabel));
        OnPropertyChanged(nameof(SelectionStart));
        OnPropertyChanged(nameof(SelectionEnd));
        OnPropertyChanged(nameof(SelectionLabel));
        OnPropertyChanged(nameof(HasPendingImport));
        OnPropertyChanged(nameof(IsEmptyProject));
        OnPropertyChanged(nameof(IsEditingMaster));
        OnPropertyChanged(nameof(SetMasterActionLabel));
    }

    private void RaiseMasterProperties()
    {
        OnPropertyChanged(nameof(Project));
        OnPropertyChanged(nameof(Master));
        OnPropertyChanged(nameof(HasMaster));
        OnPropertyChanged(nameof(HasChildTracks));
        OnPropertyChanged(nameof(IsEmptyProject));
        OnPropertyChanged(nameof(MasterDurationLabel));
        OnPropertyChanged(nameof(MasterFrameLabel));
        OnPropertyChanged(nameof(MasterPositionLabel));
        OnPropertyChanged(nameof(LoopSafetyLabel));
    }

    private void RaiseAvailabilityProperties()
    {
        OnPropertyChanged(nameof(CanImportAudio));
        OnPropertyChanged(nameof(CanEditMaster));
        OnPropertyChanged(nameof(CanAuditionSelection));
        OnPropertyChanged(nameof(CanUseMasterTransport));
        OnPropertyChanged(nameof(EngineReady));
        OnPropertyChanged(nameof(EngineRequirementLabel));
        RaiseCommandStates();
    }

    private void RaiseTransportProperties()
    {
        OnPropertyChanged(nameof(IsMasterPlaying));
        OnPropertyChanged(nameof(IsAuditionPlaying));
        OnPropertyChanged(nameof(TransportPlayPauseLabel));
        OnPropertyChanged(nameof(AuditionPlayPauseLabel));
        OnPropertyChanged(nameof(MasterPositionLabel));
        OnPropertyChanged(nameof(MonitorRouteLabel));
    }

    private void RaiseCommandStates()
    {
        ImportAudioCommand.RaiseCanExecuteChanged();
        EditMasterCommand.RaiseCanExecuteChanged();
        SetAsMasterCommand.RaiseCanExecuteChanged();
        CancelImportCommand.RaiseCanExecuteChanged();
        NewProjectCommand.RaiseCanExecuteChanged();
        AuditionPlayPauseCommand.RaiseCanExecuteChanged();
        AuditionSeekCommand.RaiseCanExecuteChanged();
        TransportPlayPauseCommand.RaiseCanExecuteChanged();
        TransportStopCommand.RaiseCanExecuteChanged();
    }

    internal void OnWorkspaceHidden() => StopPlayback();

    private static string FormatTime(TimeSpan value) => value.TotalHours >= 1.0
        ? value.ToString(@"h\:mm\:ss\.fff")
        : value.ToString(@"m\:ss\.fff");

    private static string FormatSeconds(double seconds) =>
        TimeSpan.FromSeconds(Math.Max(0.0, double.IsFinite(seconds) ? seconds : 0.0)).ToString(@"m\:ss\.fff");

    private static string FormatMebibytes(long bytes) => $"{bytes / 1_048_576.0:0.0} MiB";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _transportTimer.Stop();
        _selectionRestartTimer.Stop();
        _monitorService.Dispose();
    }
}