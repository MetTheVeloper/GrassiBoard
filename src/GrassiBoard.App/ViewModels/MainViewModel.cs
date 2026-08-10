using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using GrassiBoard.Infrastructure;
using GrassiBoard.Models;
using GrassiBoard.Services;
using GrassiBoard.Shared;
using Microsoft.Win32;

namespace GrassiBoard.ViewModels;

internal enum AppPage
{
    Board,
    Voice,
    Mixer,
    Routing,
    Settings
}

internal sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly NativeAudioEngine _engine = new();
    private readonly SoundboardStore _store;
    private readonly DispatcherTimer _meterTimer;
    private readonly DispatcherTimer _saveTimer;
    private readonly BuildInfo _build;
    private IReadOnlyList<AudioDevice> _captureDevices = [];
    private AppPage _currentPage;
    private AudioDevice? _selectedInput;
    private AudioDevice? _selectedOutput;
    private AudioDevice? _targetCapture;
    private bool _nativeReady;
    private bool _running;
    private bool _busy;
    private bool _microphoneMuted;
    private bool _voiceFxEnabled;
    private bool _preserveVocalCharacter = true;
    private double _pitch;
    private double _finePitch;
    private double _formant;
    private int _qualityIndex = 1;
    private double _micGain;
    private double _soundboardGain;
    private double _masterGain;
    private bool _noiseGateEnabled;
    private double _gateThreshold = -55.0;
    private bool _compressorEnabled;
    private double _compressorThreshold = -18.0;
    private double _compressorRatio = 3.0;
    private bool _limiterEnabled = true;
    private double _limiterCeiling = -1.0;
    private bool _duckingEnabled;
    private double _duckingAmount = 9.0;
    private bool _clippingProtectionEnabled = true;
    private double _pitchWetMix = 100.0;
    private int _presetIndex;
    private bool _applyingPreset;
    private string _engineStatus = "Loading audio engine...";
    private string _engineDetail = "Preparing native API";
    private string _virtualMicrophoneStatus = "Checking virtual microphone...";
    private string _virtualMicrophoneHint = "Open Routing to choose the virtual output.";
    private double _microphoneMeter;
    private double _soundboardMeter;
    private double _masterMeter;
    private string _microphoneDb = "−∞ dBFS";
    private string _soundboardDb = "−∞ dBFS";
    private string _masterDb = "−∞ dBFS";
    private AudioStatistics _statistics;
    private bool _disposed;

    public MainViewModel(SoundboardStore? store = null)
    {
        _store = store ?? new SoundboardStore();
        _build = BuildInfo.Load(Path.Combine(AppContext.BaseDirectory, "BuildInfo.json"));
        Pads = [];
        InputDevices = [];
        OutputDevices = [];

        NavigateCommand = new RelayCommand(parameter =>
        {
            if (parameter is AppPage page)
            {
                CurrentPage = page;
            }
            else if (parameter is string text && Enum.TryParse(text, out AppPage parsed))
            {
                CurrentPage = parsed;
            }
        });
        StartStopCommand = new AsyncRelayCommand(_ => StartStopAsync());
        RefreshDevicesCommand = new RelayCommand(_ => RefreshDevices(), _ => CanConfigureRouting);
        ToggleMuteCommand = new RelayCommand(_ => MicrophoneMuted = !MicrophoneMuted, _ => NativeReady);
        ToggleVoiceFxCommand = new RelayCommand(_ => VoiceFxEnabled = !VoiceFxEnabled, _ => NativeReady);
        StopAllCommand = new AsyncRelayCommand(_ => StopAllAsync(), _ => NativeReady && !IsBusy);
        ResetVoiceCommand = new RelayCommand(_ => ResetVoice(), _ => NativeReady);
        ResetMixerCommand = new RelayCommand(_ => ResetMixer(), _ => NativeReady);
        ApplyPresetCommand = new RelayCommand(_ => ApplySelectedPreset(), _ => NativeReady && PresetIndex > 0);
        AddPadsCommand = new AsyncRelayCommand(_ => ChooseAndAddPadsAsync(), _ => NativeReady);
        PlayPadCommand = new AsyncRelayCommand(parameter => PlayPadAsync(parameter as SoundPadModel), _ => NativeReady);
        StopPadCommand = new RelayCommand(parameter => StopPad(parameter as SoundPadModel));
        EditPadCommand = new RelayCommand(parameter =>
        {
            if (parameter is SoundPadModel pad)
            {
                EditPadRequested?.Invoke(pad);
            }
        });
        DeletePadCommand = new RelayCommand(parameter => DeletePad(parameter as SoundPadModel));
        CopyDiagnosticsCommand = new RelayCommand(_ => Clipboard.SetText(BuildDiagnostics()));

        _meterTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, OnMeterTick, Dispatcher.CurrentDispatcher);
        _meterTimer.Stop();
        _saveTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(450), DispatcherPriority.Background, OnSaveTick, Dispatcher.CurrentDispatcher);
        _saveTimer.Stop();
    }

    public event Action<SoundPadModel>? EditPadRequested;

    public ObservableCollection<SoundPadModel> Pads { get; }
    public ObservableCollection<AudioDevice> InputDevices { get; }
    public ObservableCollection<AudioDevice> OutputDevices { get; }

    public RelayCommand NavigateCommand { get; }
    public AsyncRelayCommand StartStopCommand { get; }
    public RelayCommand RefreshDevicesCommand { get; }
    public RelayCommand ToggleMuteCommand { get; }
    public RelayCommand ToggleVoiceFxCommand { get; }
    public AsyncRelayCommand StopAllCommand { get; }
    public RelayCommand ResetVoiceCommand { get; }
    public RelayCommand ResetMixerCommand { get; }
    public RelayCommand ApplyPresetCommand { get; }
    public AsyncRelayCommand AddPadsCommand { get; }
    public AsyncRelayCommand PlayPadCommand { get; }
    public RelayCommand StopPadCommand { get; }
    public RelayCommand EditPadCommand { get; }
    public RelayCommand DeletePadCommand { get; }
    public RelayCommand CopyDiagnosticsCommand { get; }

    public string Version => $"v{_build.Version}";
    public string Commit => _build.ShortCommit;
    public string NativeVersion => _engine.NativeVersion;
    public uint NativeApiVersion => _engine.ApiVersion;
    public bool HasPads => Pads.Count > 0;
    public bool IsBoardPage => CurrentPage == AppPage.Board;
    public bool IsVoicePage => CurrentPage == AppPage.Voice;
    public bool IsMixerPage => CurrentPage == AppPage.Mixer;
    public bool IsRoutingPage => CurrentPage == AppPage.Routing;
    public bool IsSettingsPage => CurrentPage == AppPage.Settings;

    public AppPage CurrentPage
    {
        get => _currentPage;
        set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(IsBoardPage));
                OnPropertyChanged(nameof(IsVoicePage));
                OnPropertyChanged(nameof(IsMixerPage));
                OnPropertyChanged(nameof(IsRoutingPage));
                OnPropertyChanged(nameof(IsSettingsPage));
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(PageSubtitle));
                OnPropertyChanged(nameof(DiagnosticsText));
            }
        }
    }

    public string PageTitle => CurrentPage switch
    {
        AppPage.Voice => "Voice",
        AppPage.Mixer => "Mixer",
        AppPage.Routing => "Routing",
        AppPage.Settings => "Settings",
        _ => "Board"
    };

    public string PageSubtitle => CurrentPage switch
    {
        AppPage.Voice => "Shape the microphone voice without affecting Sound Pads.",
        AppPage.Mixer => "Balance buses and control dynamics in the live output path.",
        AppPage.Routing => "Choose the physical microphone and virtual microphone route.",
        AppPage.Settings => "Diagnostics, build information, and support details.",
        _ => "Trigger sounds and keep essential voice controls close."
    };

    public AudioDevice? SelectedInput
    {
        get => _selectedInput;
        set
        {
            if (SetProperty(ref _selectedInput, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public AudioDevice? SelectedOutput
    {
        get => _selectedOutput;
        set
        {
            if (SetProperty(ref _selectedOutput, value))
            {
                UpdateVirtualMicrophoneStatus();
                RaiseCommandStates();
            }
        }
    }

    public bool NativeReady
    {
        get => _nativeReady;
        private set
        {
            if (SetProperty(ref _nativeReady, value))
            {
                OnPropertyChanged(nameof(CanConfigureRouting));
                RaiseCommandStates();
            }
        }
    }

    public bool IsRunning
    {
        get => _running;
        private set
        {
            if (SetProperty(ref _running, value))
            {
                OnPropertyChanged(nameof(EngineButtonLabel));
                OnPropertyChanged(nameof(CanConfigureRouting));
                OnPropertyChanged(nameof(EngineStateLabel));
                RaiseCommandStates();
            }
        }
    }

    public bool IsBusy
    {
        get => _busy;
        private set
        {
            if (SetProperty(ref _busy, value))
            {
                OnPropertyChanged(nameof(EngineButtonLabel));
                OnPropertyChanged(nameof(CanConfigureRouting));
                RaiseCommandStates();
            }
        }
    }

    public bool CanConfigureRouting => NativeReady && !IsRunning && !IsBusy;
    public string EngineButtonLabel => IsBusy ? "Please wait..." : IsRunning ? "Stop engine" : "Start engine";
    public string EngineStateLabel => IsRunning ? "LIVE" : NativeReady ? "READY" : "OFFLINE";

    public string EngineStatus
    {
        get => _engineStatus;
        private set => SetProperty(ref _engineStatus, value);
    }

    public string EngineDetail
    {
        get => _engineDetail;
        private set => SetProperty(ref _engineDetail, value);
    }

    public string VirtualMicrophoneStatus
    {
        get => _virtualMicrophoneStatus;
        private set => SetProperty(ref _virtualMicrophoneStatus, value);
    }

    public string VirtualMicrophoneHint
    {
        get => _virtualMicrophoneHint;
        private set => SetProperty(ref _virtualMicrophoneHint, value);
    }

    public bool MicrophoneMuted
    {
        get => _microphoneMuted;
        set
        {
            if (SetProperty(ref _microphoneMuted, value) && NativeReady)
            {
                _engine.SetMicrophoneMuted(value);
                OnPropertyChanged(nameof(MuteButtonLabel));
            }
        }
    }

    public string MuteButtonLabel => MicrophoneMuted ? "Unmute Mic" : "Mute Mic";

    public bool VoiceFxEnabled
    {
        get => _voiceFxEnabled;
        set
        {
            if (SetProperty(ref _voiceFxEnabled, value) && NativeReady)
            {
                _engine.SetVoiceFxEnabled(value);
                OnPropertyChanged(nameof(VoiceFxLabel));
            }
        }
    }

    public string VoiceFxLabel => VoiceFxEnabled ? "Voice FX ON" : "Voice FX OFF";

    public double Pitch
    {
        get => _pitch;
        set
        {
            double clamped = Math.Clamp(value, -12.0, 12.0);
            if (SetProperty(ref _pitch, clamped) && NativeReady)
            {
                _engine.SetPitch((float)clamped);
                OnPropertyChanged(nameof(PitchLabel));
            }
        }
    }

    public string PitchLabel => $"{Pitch:+0;-0;0} st";

    public double FinePitch
    {
        get => _finePitch;
        set
        {
            double clamped = Math.Clamp(value, -100.0, 100.0);
            if (SetProperty(ref _finePitch, clamped) && NativeReady)
            {
                _engine.SetFinePitch((float)clamped);
                OnPropertyChanged(nameof(FinePitchLabel));
            }
        }
    }

    public string FinePitchLabel => $"{FinePitch:+0;-0;0} cents";

    public double Formant
    {
        get => _formant;
        set
        {
            double clamped = Math.Clamp(value, -12.0, 12.0);
            if (SetProperty(ref _formant, clamped) && NativeReady)
            {
                _engine.SetFormant((float)clamped);
                OnPropertyChanged(nameof(FormantLabel));
            }
        }
    }

    public string FormantLabel => $"{Formant:+0.0;-0.0;0} st";

    public bool PreserveVocalCharacter
    {
        get => _preserveVocalCharacter;
        set
        {
            if (SetProperty(ref _preserveVocalCharacter, value) && NativeReady)
            {
                _engine.SetFormantPreservation(value);
            }
        }
    }

    public int QualityIndex
    {
        get => _qualityIndex;
        set
        {
            int clamped = Math.Clamp(value, 0, 2);
            if (SetProperty(ref _qualityIndex, clamped) && NativeReady)
            {
                _engine.SetQuality((uint)clamped);
                OnPropertyChanged(nameof(QualityLabel));
            }
        }
    }

    public string QualityLabel => QualityIndex switch
    {
        0 => "Low latency",
        2 => "High quality",
        _ => "Balanced"
    };

    public double MicGain
    {
        get => _micGain;
        set => SetMixerValue(ref _micGain, value, -24.0, 24.0, nameof(MicGain), nameof(MicGainLabel));
    }

    public string MicGainLabel => FormatSignedDb(MicGain);

    public double SoundboardGain
    {
        get => _soundboardGain;
        set => SetMixerValue(ref _soundboardGain, value, -24.0, 24.0, nameof(SoundboardGain), nameof(SoundboardGainLabel));
    }

    public string SoundboardGainLabel => FormatSignedDb(SoundboardGain);

    public double MasterGain
    {
        get => _masterGain;
        set => SetMixerValue(ref _masterGain, value, -24.0, 12.0, nameof(MasterGain), nameof(MasterGainLabel));
    }

    public string MasterGainLabel => FormatSignedDb(MasterGain);

    public bool NoiseGateEnabled
    {
        get => _noiseGateEnabled;
        set => SetMixerToggle(ref _noiseGateEnabled, value, nameof(NoiseGateEnabled));
    }

    public double GateThreshold
    {
        get => _gateThreshold;
        set => SetMixerValue(ref _gateThreshold, value, -80.0, -20.0, nameof(GateThreshold), nameof(GateThresholdLabel));
    }

    public string GateThresholdLabel => $"{GateThreshold:0} dB";

    public bool CompressorEnabled
    {
        get => _compressorEnabled;
        set => SetMixerToggle(ref _compressorEnabled, value, nameof(CompressorEnabled));
    }

    public double CompressorThreshold
    {
        get => _compressorThreshold;
        set => SetMixerValue(ref _compressorThreshold, value, -40.0, -3.0, nameof(CompressorThreshold), nameof(CompressorThresholdLabel));
    }

    public string CompressorThresholdLabel => $"{CompressorThreshold:0} dB";

    public double CompressorRatio
    {
        get => _compressorRatio;
        set => SetMixerValue(ref _compressorRatio, value, 1.0, 20.0, nameof(CompressorRatio), nameof(CompressorRatioLabel));
    }

    public string CompressorRatioLabel => $"{CompressorRatio:0.0}:1";

    public bool LimiterEnabled
    {
        get => _limiterEnabled;
        set => SetMixerToggle(ref _limiterEnabled, value, nameof(LimiterEnabled));
    }

    public double LimiterCeiling
    {
        get => _limiterCeiling;
        set => SetMixerValue(ref _limiterCeiling, value, -12.0, 0.0, nameof(LimiterCeiling), nameof(LimiterCeilingLabel));
    }

    public string LimiterCeilingLabel => $"{LimiterCeiling:0.0} dB";

    public bool DuckingEnabled
    {
        get => _duckingEnabled;
        set => SetMixerToggle(ref _duckingEnabled, value, nameof(DuckingEnabled));
    }

    public double DuckingAmount
    {
        get => _duckingAmount;
        set => SetMixerValue(ref _duckingAmount, value, 0.0, 30.0, nameof(DuckingAmount), nameof(DuckingAmountLabel));
    }

    public string DuckingAmountLabel => $"{DuckingAmount:0} dB";

    public bool ClippingProtectionEnabled
    {
        get => _clippingProtectionEnabled;
        set => SetMixerToggle(ref _clippingProtectionEnabled, value, nameof(ClippingProtectionEnabled));
    }

    public double PitchWetMix
    {
        get => _pitchWetMix;
        set => SetMixerValue(ref _pitchWetMix, value, 0.0, 100.0, nameof(PitchWetMix), nameof(PitchWetMixLabel));
    }

    public string PitchWetMixLabel => $"{PitchWetMix:0}% wet";

    public int PresetIndex
    {
        get => _presetIndex;
        set
        {
            int clamped = Math.Clamp(value, 0, 4);
            if (SetProperty(ref _presetIndex, clamped))
            {
                ApplyPresetCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public double MicrophoneMeter
    {
        get => _microphoneMeter;
        private set => SetProperty(ref _microphoneMeter, value);
    }

    public double SoundboardMeter
    {
        get => _soundboardMeter;
        private set => SetProperty(ref _soundboardMeter, value);
    }

    public double MasterMeter
    {
        get => _masterMeter;
        private set => SetProperty(ref _masterMeter, value);
    }

    public string MicrophoneDb
    {
        get => _microphoneDb;
        private set => SetProperty(ref _microphoneDb, value);
    }

    public string SoundboardDb
    {
        get => _soundboardDb;
        private set => SetProperty(ref _soundboardDb, value);
    }

    public string MasterDb
    {
        get => _masterDb;
        private set => SetProperty(ref _masterDb, value);
    }

    public string DiagnosticsText => BuildDiagnostics();

    public async Task InitializeAsync()
    {
        try
        {
            NativeResult result = _engine.Initialize();
            NativeReady = result == NativeResult.Ok && _engine.IsAvailable;
            if (!NativeReady)
            {
                EngineStatus = _engine.ApiVersion == NativeAudioEngine.ExpectedApiVersion
                    ? $"Engine creation failed · {result}"
                    : $"Native API mismatch · expected {NativeAudioEngine.ExpectedApiVersion}, got {_engine.ApiVersion}";
                EngineDetail = "Reinstall the matching portable build.";
                return;
            }

            ApplyVoiceState();
            ApplyMixerState();
            RefreshDevices();
            EngineStatus = "Audio workspace ready";
            EngineDetail = $"Native API {_engine.ApiVersion} · engine v{_engine.NativeVersion}";

            foreach (SoundPadModel pad in _store.Load())
            {
                AddPadToCollection(pad);
            }
            OnPropertyChanged(nameof(HasPads));

            _meterTimer.Start();
            foreach (SoundPadModel pad in Pads)
            {
                await LoadPadAsync(pad);
            }
        }
        catch (Exception exception) when (exception is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            EngineStatus = $"Native engine unavailable · {exception.GetType().Name}";
                EngineDetail = "Use the complete v0.9.0 portable package.";
        }
    }

    public async Task AddFilesAsync(IEnumerable<string> paths)
    {
        if (!NativeReady)
        {
            EngineDetail = "The native audio engine must be available before adding Sound Pads.";
            return;
        }
        foreach (string path in paths.Where(IsSupportedAudioPath))
        {
            var pad = new SoundPadModel
            {
                Title = Path.GetFileNameWithoutExtension(path),
                FilePath = Path.GetFullPath(path)
            };
            AddPadToCollection(pad);
            OnPropertyChanged(nameof(HasPads));
            ScheduleSave();
            await LoadPadAsync(pad);
        }
    }

    public async Task ApplyPadEditAsync(
        SoundPadModel pad,
        string title,
        string filePath,
        double volume,
        bool loop,
        bool restartOnPress)
    {
        bool fileChanged = !string.Equals(pad.FilePath, filePath, StringComparison.OrdinalIgnoreCase);
        pad.Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(filePath) : title.Trim();
        pad.FilePath = Path.GetFullPath(filePath);
        pad.Volume = volume;
        pad.Loop = loop;
        pad.RestartOnPress = restartOnPress;
        if (fileChanged || !pad.IsLoaded)
        {
            await LoadPadAsync(pad);
        }
        ScheduleSave();
    }

    private async Task ChooseAndAddPadsAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add Sound Pads",
            Filter = "Supported audio|*.wav;*.mp3|Wave audio|*.wav|MP3 audio|*.mp3",
            Multiselect = true,
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == true)
        {
            await AddFilesAsync(dialog.FileNames);
        }
    }

    private async Task LoadPadAsync(SoundPadModel pad)
    {
        pad.IsLoading = true;
        pad.IsLoaded = false;
        pad.Error = null;
        try
        {
            if (!File.Exists(pad.FilePath))
            {
                throw new FileNotFoundException("Audio file is missing. Edit the pad to choose it again.");
            }

            (DecodedAudio decoded, NativeResult result) = await Task.Run(() =>
            {
                DecodedAudio decodedAudio = AudioFileDecoder.Decode(pad.FilePath);
                NativeResult loadResult = _engine.LoadSound(pad.NativeKey, decodedAudio.Samples, decodedAudio.FrameCount);
                return (decodedAudio, loadResult);
            });
            if (result != NativeResult.Ok)
            {
                throw new InvalidOperationException($"Native clip load failed: {result}");
            }
            pad.DurationSeconds = decoded.Duration.TotalSeconds;
            pad.IsLoaded = true;
        }
        catch (Exception exception)
        {
            pad.Error = exception.Message;
            CrashReporter.Report(exception, $"Loading Sound Pad: {pad.FilePath}", false);
        }
        finally
        {
            pad.IsLoading = false;
        }
    }

    private async Task PlayPadAsync(SoundPadModel? pad)
    {
        if (pad is null)
        {
            return;
        }
        if (!IsRunning)
        {
            pad.Error = "Start the audio engine before playing a Sound Pad.";
            return;
        }
        if (!pad.IsLoaded)
        {
            await LoadPadAsync(pad);
        }
        if (!pad.IsLoaded)
        {
            return;
        }

        NativeResult result = _engine.PlaySound(pad.NativeKey, (float)pad.Volume, pad.Loop, pad.RestartOnPress);
        if (result != NativeResult.Ok)
        {
            pad.Error = result == NativeResult.QueueFull
                ? "Sound command queue is busy. Try again."
                : $"Playback failed: {result}";
            return;
        }
        pad.Error = null;
        pad.IsPlaying = true;
        pad.PlaybackStartedAt = DateTimeOffset.UtcNow;
    }

    private void StopPad(SoundPadModel? pad)
    {
        if (pad is null || !NativeReady)
        {
            return;
        }
        _engine.StopSound(pad.NativeKey);
        pad.IsPlaying = false;
    }

    private void StopAllSounds()
    {
        if (NativeReady)
        {
            _engine.StopAllSounds();
        }
        foreach (SoundPadModel pad in Pads)
        {
            pad.IsPlaying = false;
        }
    }

    private async Task StopAllAsync()
    {
        if (IsBusy || !NativeReady)
        {
            return;
        }

        IsBusy = true;
        try
        {
            StopAllSounds();
            NativeResult result = await Task.Run(_engine.Stop);
            IsRunning = false;
            EngineStatus = result == NativeResult.Ok ? "Audio engine stopped" : $"Stop failed · {result}";
            EngineDetail = result == NativeResult.Ok ? "All audio stopped · ready to start again" : _engine.ReadLastError();
            ResetMeters();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void DeletePad(SoundPadModel? pad)
    {
        if (pad is null)
        {
            return;
        }
        MessageBoxResult answer = MessageBox.Show(
            $"Remove ‘{pad.Title}’ from the board? The audio file will not be deleted.",
            "Remove Sound Pad",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        StopPad(pad);
        pad.PropertyChanged -= OnPadPropertyChanged;
        Pads.Remove(pad);
        OnPropertyChanged(nameof(HasPads));
        ScheduleSave();
    }

    private async Task StartStopAsync()
    {
        if (IsBusy || !NativeReady)
        {
            return;
        }
        IsBusy = true;
        try
        {
            if (!IsRunning)
            {
                if (SelectedInput is null || SelectedOutput is null)
                {
                    EngineStatus = "Select an input microphone and virtual output";
                    EngineDetail = "Open Routing to configure both devices.";
                    return;
                }

                EngineStatus = "Starting audio engine...";
                NativeResult result = await Task.Run(() => _engine.Start(SelectedInput.Id, SelectedOutput.Id));
                if (result != NativeResult.Ok)
                {
                    EngineStatus = $"Engine start failed · {result}";
                    EngineDetail = _engine.ReadLastError();
                    return;
                }
                IsRunning = true;
                EngineStatus = "Virtual microphone is live";
                EngineDetail = $"48 kHz · {QualityLabel} · Soundboard mixer ready";
            }
            else
            {
                StopAllSounds();
                NativeResult result = await Task.Run(_engine.Stop);
                IsRunning = false;
                EngineStatus = result == NativeResult.Ok ? "Audio engine stopped" : $"Stop failed · {result}";
                EngineDetail = result == NativeResult.Ok ? "Ready to start again" : _engine.ReadLastError();
                ResetMeters();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshDevices()
    {
        if (!NativeReady)
        {
            return;
        }
        try
        {
            AudioDevice? previousInput = SelectedInput;
            AudioDevice? previousOutput = SelectedOutput;
            IReadOnlyList<AudioDevice> inputs = _engine.EnumerateDevices(true);
            IReadOnlyList<AudioDevice> outputs = _engine.EnumerateDevices(false);
            _captureDevices = inputs;

            InputDevices.Clear();
            foreach (AudioDevice device in inputs)
            {
                InputDevices.Add(device);
            }
            OutputDevices.Clear();
            foreach (AudioDevice device in outputs)
            {
                OutputDevices.Add(device);
            }

            SelectedInput = inputs.FirstOrDefault(device => device.Id == previousInput?.Id) ??
                inputs.FirstOrDefault(device => device.IsDefault && !IsExternalVirtualEndpoint(device)) ??
                inputs.FirstOrDefault(device => !IsExternalVirtualEndpoint(device)) ??
                inputs.FirstOrDefault();
            SelectedOutput = outputs.FirstOrDefault(device => device.Id == previousOutput?.Id) ??
                outputs.FirstOrDefault(output => FindPairedCaptureEndpoint(output) is not null) ??
                outputs.FirstOrDefault(device => device.IsDefault) ??
                outputs.FirstOrDefault();
        }
        catch (Exception exception)
        {
            EngineStatus = "Device refresh failed";
            EngineDetail = exception.Message;
        }
    }

    private void UpdateVirtualMicrophoneStatus()
    {
        _targetCapture = SelectedOutput is null ? null : FindPairedCaptureEndpoint(SelectedOutput);
        if (_targetCapture is not null)
        {
            VirtualMicrophoneStatus = "Virtual microphone ready";
            VirtualMicrophoneHint = $"Choose ‘{_targetCapture.Name}’ as the microphone in Telegram, OBS, recorders, or games.";
        }
        else
        {
            VirtualMicrophoneStatus = "Virtual microphone route needs attention";
            VirtualMicrophoneHint = "Select the playback/input side of an installed external virtual cable.";
        }
        OnPropertyChanged(nameof(DiagnosticsText));
    }

    private AudioDevice? FindPairedCaptureEndpoint(AudioDevice output)
    {
        AudioEndpointDescriptor? match = VirtualCableMatcher.FindPairedCaptureEndpoint(
            output.ToDescriptor(),
            _captureDevices.Select(device => device.ToDescriptor()));
        return match is null ? null : _captureDevices.FirstOrDefault(device => device.Id == match.Id);
    }

    private static bool IsExternalVirtualEndpoint(AudioDevice device) =>
        VirtualCableMatcher.IsExternalVirtualEndpoint(device.ToDescriptor());

    private void ApplyVoiceState()
    {
        _engine.SetPitch((float)Pitch);
        _engine.SetFinePitch((float)FinePitch);
        _engine.SetVoiceFxEnabled(VoiceFxEnabled);
        _engine.SetFormant((float)Formant);
        _engine.SetFormantPreservation(PreserveVocalCharacter);
        _engine.SetQuality((uint)QualityIndex);
        _engine.SetMicrophoneMuted(MicrophoneMuted);
    }

    private void ApplyMixerState()
    {
        if (!NativeReady)
        {
            return;
        }

        MixerSettings settings = MixerSettings.CreateDefault();
        settings.MicGainDb = (float)MicGain;
        settings.SoundboardGainDb = (float)SoundboardGain;
        settings.MasterGainDb = (float)MasterGain;
        settings.GateThresholdDb = (float)GateThreshold;
        settings.CompressorThresholdDb = (float)CompressorThreshold;
        settings.CompressorRatio = (float)CompressorRatio;
        settings.LimiterCeilingDb = (float)LimiterCeiling;
        settings.DuckingAmountDb = (float)DuckingAmount;
        settings.PitchWetMix = (float)(PitchWetMix / 100.0);
        settings.GateEnabled = NoiseGateEnabled ? 1U : 0U;
        settings.CompressorEnabled = CompressorEnabled ? 1U : 0U;
        settings.LimiterEnabled = LimiterEnabled ? 1U : 0U;
        settings.DuckingEnabled = DuckingEnabled ? 1U : 0U;
        settings.ClippingProtectionEnabled = ClippingProtectionEnabled ? 1U : 0U;
        _engine.SetMixerSettings(in settings);
    }

    private void ResetMixer()
    {
        ApplyPreset(1);
        PresetIndex = 1;
    }

    private void ApplySelectedPreset()
    {
        if (PresetIndex > 0)
        {
            ApplyPreset(PresetIndex);
        }
    }

    private void ApplyPreset(int index)
    {
        _applyingPreset = true;
        try
        {
            switch (index)
            {
                case 2: // Broadcast
                    SetPresetValues(3.0, -2.0, -1.0, true, -50.0, true, -18.0, 4.0, true, -1.0, true, 6.0, true, 100.0);
                    break;
                case 3: // Streaming
                    SetPresetValues(2.0, 0.0, -1.0, true, -55.0, true, -16.0, 3.0, true, -1.0, true, 10.0, true, 85.0);
                    break;
                case 4: // Voice chat
                    SetPresetValues(4.0, -4.0, -2.0, true, -48.0, true, -20.0, 4.0, true, -1.0, true, 12.0, true, 70.0);
                    break;
                default: // Clean
                    SetPresetValues(0.0, 0.0, 0.0, false, -55.0, false, -18.0, 3.0, true, -1.0, false, 9.0, true, 100.0);
                    break;
            }
        }
        finally
        {
            _applyingPreset = false;
        }
        ApplyMixerState();
    }

    private void SetPresetValues(
        double micGain, double boardGain, double masterGain,
        bool gate, double gateThreshold,
        bool compressor, double compressorThreshold, double compressorRatio,
        bool limiter, double limiterCeiling,
        bool ducking, double duckingAmount,
        bool clippingProtection, double wetMix)
    {
        MicGain = micGain;
        SoundboardGain = boardGain;
        MasterGain = masterGain;
        NoiseGateEnabled = gate;
        GateThreshold = gateThreshold;
        CompressorEnabled = compressor;
        CompressorThreshold = compressorThreshold;
        CompressorRatio = compressorRatio;
        LimiterEnabled = limiter;
        LimiterCeiling = limiterCeiling;
        DuckingEnabled = ducking;
        DuckingAmount = duckingAmount;
        ClippingProtectionEnabled = clippingProtection;
        PitchWetMix = wetMix;
    }

    private void SetMixerValue(
        ref double field,
        double value,
        double minimum,
        double maximum,
        string propertyName,
        string labelName)
    {
        double clamped = double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : minimum;
        if (!SetProperty(ref field, clamped, propertyName))
        {
            return;
        }
        OnPropertyChanged(labelName);
        MarkMixerCustomAndApply();
    }

    private void SetMixerToggle(ref bool field, bool value, string propertyName)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            MarkMixerCustomAndApply();
        }
    }

    private void MarkMixerCustomAndApply()
    {
        if (!_applyingPreset)
        {
            PresetIndex = 0;
            ApplyMixerState();
        }
    }

    private static string FormatSignedDb(double value) => $"{value:+0.0;-0.0;0.0} dB";

    private void ResetVoice()
    {
        Pitch = 0.0;
        FinePitch = 0.0;
        Formant = 0.0;
    }

    private void OnMeterTick(object? sender, EventArgs e)
    {
        if (!NativeReady || _engine.GetStatistics(out _statistics) != NativeResult.Ok)
        {
            return;
        }

        MicrophoneMeter = ToMeter(_statistics.InputPeak);
        SoundboardMeter = ToMeter(_statistics.SoundboardPeak);
        MasterMeter = ToMeter(_statistics.MasterPeak);
        MicrophoneDb = FormatDb(_statistics.InputPeak);
        SoundboardDb = FormatDb(_statistics.SoundboardPeak);
        MasterDb = FormatDb(_statistics.MasterPeak);
        OnPropertyChanged(nameof(PitchLatencyMilliseconds));

        if (IsRunning && _statistics.Running == 0U)
        {
            IsRunning = false;
            EngineStatus = "Audio stream stopped unexpectedly";
            EngineDetail = _engine.ReadLastError();
            StopAllSounds();
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (SoundPadModel pad in Pads)
        {
            if (pad.IsPlaying && !pad.Loop && pad.DurationSeconds > 0.0 &&
                (now - pad.PlaybackStartedAt).TotalSeconds >= pad.DurationSeconds)
            {
                pad.IsPlaying = false;
            }
        }

        if (IsSettingsPage)
        {
            OnPropertyChanged(nameof(DiagnosticsText));
        }
    }

    private string BuildDiagnostics()
    {
        var text = new StringBuilder();
        text.AppendLine("GrassiBoard diagnostics");
        text.AppendLine($"Build: {_build.Version} ({_build.ShortCommit})");
        text.AppendLine($"Native: {_engine.NativeVersion} · API {_engine.ApiVersion}");
        text.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        text.AppendLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");
        text.AppendLine($"Engine: {(IsRunning ? "Running" : NativeReady ? "Ready" : "Unavailable")}");
        text.AppendLine($"Sample rate: {_statistics.SampleRate} Hz");
        text.AppendLine($"Buffers: capture {_statistics.CaptureBufferFrames}, render {_statistics.RenderBufferFrames}");
        text.AppendLine($"Ring fill: {_statistics.RingBufferFillFrames} frames");
        text.AppendLine($"Pitch latency: {_statistics.PitchLatencySamples} samples ({PitchLatencyMilliseconds:0.0} ms)");
        text.AppendLine($"Dropouts: U {_statistics.UnderrunCount} · O {_statistics.OverrunCount} · D {_statistics.DiscontinuityCount}");
        text.AppendLine($"Active Sound Pads: {_statistics.ActiveSoundCount}");
        text.AppendLine($"Microphone muted: {MicrophoneMuted}");
        text.AppendLine($"Input endpoint: {SelectedInput?.Name ?? "not selected"}");
        text.AppendLine($"Input ID: {SelectedInput?.Id ?? "not selected"}");
        text.AppendLine($"Virtual output: {SelectedOutput?.Name ?? "not selected"}");
        text.AppendLine($"Virtual output ID: {SelectedOutput?.Id ?? "not selected"}");
        text.AppendLine($"Target microphone: {_targetCapture?.Name ?? "not detected"}");
        text.AppendLine($"Latest error report: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GrassiBoard", "CrashReports", "latest.txt")}");
        return text.ToString().TrimEnd();
    }

    public double PitchLatencyMilliseconds => _statistics.SampleRate == 0U
        ? 0.0
        : _statistics.PitchLatencySamples * 1000.0 / _statistics.SampleRate;

    private void AddPadToCollection(SoundPadModel pad)
    {
        if (pad.Id == Guid.Empty)
        {
            pad.Id = Guid.NewGuid();
        }
        pad.PropertyChanged += OnPadPropertyChanged;
        Pads.Add(pad);
    }

    private void OnPadPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SoundPadModel.Title) or nameof(SoundPadModel.FilePath) or
            nameof(SoundPadModel.Volume) or nameof(SoundPadModel.Loop) or nameof(SoundPadModel.RestartOnPress))
        {
            ScheduleSave();
        }
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void OnSaveTick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        try
        {
            _store.Save(Pads);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            EngineDetail = $"Soundboard save failed: {exception.Message}";
            CrashReporter.Report(exception, "Saving Soundboard layout", false);
        }
    }

    private void ResetMeters()
    {
        MicrophoneMeter = 0.0;
        SoundboardMeter = 0.0;
        MasterMeter = 0.0;
        MicrophoneDb = "−∞ dBFS";
        SoundboardDb = "−∞ dBFS";
        MasterDb = "−∞ dBFS";
    }

    private void RaiseCommandStates()
    {
        StartStopCommand.RaiseCanExecuteChanged();
        RefreshDevicesCommand.RaiseCanExecuteChanged();
        ToggleMuteCommand.RaiseCanExecuteChanged();
        ToggleVoiceFxCommand.RaiseCanExecuteChanged();
        StopAllCommand.RaiseCanExecuteChanged();
        ResetVoiceCommand.RaiseCanExecuteChanged();
        ResetMixerCommand.RaiseCanExecuteChanged();
        ApplyPresetCommand.RaiseCanExecuteChanged();
        AddPadsCommand.RaiseCanExecuteChanged();
        PlayPadCommand.RaiseCanExecuteChanged();
    }

    private static bool IsSupportedAudioPath(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase);
    }

    internal static double ToMeter(float linear)
    {
        if (!float.IsFinite(linear) || linear <= 0.0F)
        {
            return 0.0;
        }
        double db = 20.0 * Math.Log10(linear);
        return Math.Clamp((db + 60.0) / 60.0 * 100.0, 0.0, 100.0);
    }

    private static string FormatDb(float linear) =>
        !float.IsFinite(linear) || linear <= 0.000001F
            ? "−∞ dBFS"
            : $"{20.0 * Math.Log10(linear):0.0} dBFS";

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _meterTimer.Stop();
        _saveTimer.Stop();
        try
        {
            _store.Save(Pads);
        }
        catch (IOException)
        {
            // The application is closing; a prior successful save remains intact.
        }
        StopAllSounds();
        _engine.Dispose();
    }
}
