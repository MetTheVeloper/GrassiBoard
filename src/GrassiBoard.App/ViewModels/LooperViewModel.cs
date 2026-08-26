using System.IO;
using GrassiBoard.Infrastructure;
using GrassiBoard.Models.Looper;
using GrassiBoard.Services.Looper;
using Microsoft.Win32;

namespace GrassiBoard.ViewModels;

internal sealed class LooperViewModel : ObservableObject
{
    private readonly LooperProjectStore _projectStore;
    private readonly WaveformAnalysisService _analysisService;
    private WaveformAnalysisResult? _pendingImport;
    private double _selectionStart;
    private double _selectionEnd = 1.0;
    private bool _busy;
    private string _statusMessage = "Create a Master Loop to start the project.";

    public LooperViewModel(
        LooperProjectStore? projectStore = null,
        WaveformAnalysisService? analysisService = null)
    {
        _projectStore = projectStore ?? new LooperProjectStore();
        _analysisService = analysisService ?? new WaveformAnalysisService();
        ImportAudioCommand = new AsyncRelayCommand(_ => ChooseImportAsync(), _ => CanImportAudio);
        SetAsMasterCommand = new RelayCommand(_ => SetAsMaster(), _ => _pendingImport is not null && !IsBusy);
        CancelImportCommand = new RelayCommand(_ => CancelImport(), _ => _pendingImport is not null && !IsBusy);
        NewProjectCommand = new RelayCommand(_ => NewProject(), _ => !IsBusy);
    }

    public AsyncRelayCommand ImportAudioCommand { get; }
    public RelayCommand SetAsMasterCommand { get; }
    public RelayCommand CancelImportCommand { get; }
    public RelayCommand NewProjectCommand { get; }

    public LooperProjectModel Project => _projectStore.Current;
    public LooperMasterModel? Master => Project.Master;
    public bool HasMaster => Master is not null;
    public bool HasPendingImport => _pendingImport is not null;
    public bool IsEmptyProject => !HasMaster && !HasPendingImport;
    public bool HasChildTracks => Project.Tracks.Count > 0;
    public bool CanImportAudio => !IsBusy && Project.CanRedefineMaster;
    public WaveformEnvelope PendingWaveform => _pendingImport?.Envelope ?? WaveformEnvelope.Empty;
    public string PendingFileName { get; private set; } = string.Empty;
    public string PendingDurationLabel => _pendingImport is null ? string.Empty : FormatTime(_pendingImport.Duration);
    public string MasterDurationLabel => Master is null ? string.Empty : FormatTime(Master.Duration);
    public string MasterFrameLabel => Master is null ? string.Empty : $"{Master.FrameCount:N0} frames @ {Master.SampleRate:N0} Hz";
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public bool IsBusy
    {
        get => _busy;
        private set
        {
            if (!SetProperty(ref _busy, value)) return;
            OnPropertyChanged(nameof(CanImportAudio));
            ImportAudioCommand.RaiseCanExecuteChanged();
            SetAsMasterCommand.RaiseCanExecuteChanged();
            CancelImportCommand.RaiseCanExecuteChanged();
            NewProjectCommand.RaiseCanExecuteChanged();
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
                OnPropertyChanged(nameof(SelectionLabel));
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
                OnPropertyChanged(nameof(SelectionLabel));
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
        IsBusy = true;
        StatusMessage = "Analyzing waveform…";
        try
        {
            WaveformAnalysisResult result = await _analysisService.AnalyzeFileAsync(path, 2_048, cancellationToken);
            _pendingImport = result;
            PendingFileName = Path.GetFileName(path);
            _selectionStart = 0.0;
            _selectionEnd = 1.0;
            OnPropertyChanged(nameof(PendingWaveform));
            OnPropertyChanged(nameof(PendingFileName));
            OnPropertyChanged(nameof(PendingDurationLabel));
            OnPropertyChanged(nameof(SelectionStart));
            OnPropertyChanged(nameof(SelectionEnd));
            OnPropertyChanged(nameof(SelectionLabel));
            OnPropertyChanged(nameof(HasPendingImport));
            OnPropertyChanged(nameof(IsEmptyProject));
            StatusMessage = "Drag the START / END handles, then set the selection as Master Loop.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or InvalidDataException)
        {
            StatusMessage = exception.Message;
            throw;
        }
        finally
        {
            IsBusy = false;
            SetAsMasterCommand.RaiseCanExecuteChanged();
            CancelImportCommand.RaiseCanExecuteChanged();
        }
    }

    internal void SetAsMaster()
    {
        if (_pendingImport is null) return;

        long totalFrames = _pendingImport.FrameCount;
        long startFrame = Math.Clamp((long)Math.Floor(SelectionStart * totalFrames), 0L, Math.Max(0L, totalFrames - 1L));
        long endFrame = Math.Clamp((long)Math.Ceiling(SelectionEnd * totalFrames), startFrame + 1L, totalFrames);
        int startSample = checked((int)(startFrame * WaveformAnalysisService.TargetChannels));
        int sampleCount = checked((int)((endFrame - startFrame) * WaveformAnalysisService.TargetChannels));
        float[] selectedSamples = new float[sampleCount];
        Array.Copy(_pendingImport.Samples, startSample, selectedSamples, 0, sampleCount);

        var master = new LooperMasterModel
        {
            SourceFileName = PendingFileName,
            SourcePath = PendingFileName,
            SourceStartFrame = startFrame,
            SourceEndFrame = endFrame,
            FrameCount = endFrame - startFrame,
            Samples = selectedSamples,
            Waveform = _analysisService.BuildEnvelope(selectedSamples)
        };
        _projectStore.SetMaster(master);
        _pendingImport = null;
        PendingFileName = string.Empty;
        OnPropertyChanged(nameof(Project));
        OnPropertyChanged(nameof(Master));
        OnPropertyChanged(nameof(HasMaster));
        OnPropertyChanged(nameof(HasPendingImport));
        OnPropertyChanged(nameof(IsEmptyProject));
        OnPropertyChanged(nameof(CanImportAudio));
        OnPropertyChanged(nameof(PendingWaveform));
        OnPropertyChanged(nameof(PendingFileName));
        OnPropertyChanged(nameof(PendingDurationLabel));
        OnPropertyChanged(nameof(MasterDurationLabel));
        OnPropertyChanged(nameof(MasterFrameLabel));
        StatusMessage = "Master Loop is defined. Transport and local monitoring arrive in Gate 2.";
        ImportAudioCommand.RaiseCanExecuteChanged();
        SetAsMasterCommand.RaiseCanExecuteChanged();
        CancelImportCommand.RaiseCanExecuteChanged();
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
            // The user-facing error is already exposed through StatusMessage.
        }
    }

    private void CancelImport()
    {
        _pendingImport = null;
        PendingFileName = string.Empty;
        OnPropertyChanged(nameof(PendingWaveform));
        OnPropertyChanged(nameof(PendingFileName));
        OnPropertyChanged(nameof(PendingDurationLabel));
        OnPropertyChanged(nameof(HasPendingImport));
        OnPropertyChanged(nameof(IsEmptyProject));
        StatusMessage = HasMaster ? "Master Loop unchanged." : "Create a Master Loop to start the project.";
        SetAsMasterCommand.RaiseCanExecuteChanged();
        CancelImportCommand.RaiseCanExecuteChanged();
    }

    private void NewProject()
    {
        _projectStore.Reset();
        _pendingImport = null;
        PendingFileName = string.Empty;
        _selectionStart = 0.0;
        _selectionEnd = 1.0;
        OnPropertyChanged(nameof(Project));
        OnPropertyChanged(nameof(Master));
        OnPropertyChanged(nameof(HasMaster));
        OnPropertyChanged(nameof(HasPendingImport));
        OnPropertyChanged(nameof(IsEmptyProject));
        OnPropertyChanged(nameof(HasChildTracks));
        OnPropertyChanged(nameof(CanImportAudio));
        OnPropertyChanged(nameof(PendingWaveform));
        OnPropertyChanged(nameof(MasterDurationLabel));
        OnPropertyChanged(nameof(MasterFrameLabel));
        StatusMessage = "Create a Master Loop to start the project.";
        ImportAudioCommand.RaiseCanExecuteChanged();
    }

    private static string FormatTime(TimeSpan value) => value.TotalHours >= 1.0
        ? value.ToString(@"h\:mm\:ss\.fff")
        : value.ToString(@"m\:ss\.fff");

    private static string FormatSeconds(double seconds) => TimeSpan.FromSeconds(Math.Max(0.0, seconds)).ToString(@"m\:ss\.fff");
}
