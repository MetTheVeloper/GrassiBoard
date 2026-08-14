using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using GrassiBoard.Infrastructure;
using GrassiBoard.Models;
using GrassiBoard.Services;
using GrassiBoard.Services.Remote;
using GrassiBoard.Shared;
using GrassiBoard.Views;
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
    private readonly Dispatcher _dispatcher;
    private readonly SoundboardStore _store;
    private readonly ProfileStore _profileStore;
    private readonly ProfileDocument _profileDocument;
    private readonly MediaDeckService _mediaDeck;
    private readonly GlobalHotkeyService _hotkeys;
    private readonly RemoteStatePublisher _remoteStatePublisher;
    private readonly RemoteServerService _remoteServer;
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
    private ProfileModel _activeProfile;
    private ProfileModel? _selectedProfile;
    private UserPresetModel? _selectedUserPreset;
    private CancellationTokenSource? _presetTransition;
    private string _hotkeyStatus = "Global hotkeys initialize with the window.";
    private AudioDevice? _selectedMonitorOutput;
    private double _mediaPosition;
    private bool _mediaSeeking;
    private bool _mediaTimelineSeeking;
    private long _mediaPositionHoldUntil;
    private bool _automaticRecoveryArmed;
    private bool _automaticRecoveryInProgress;
    private bool _forcedRecoveryMute;
    private string _failedInputDeviceId = string.Empty;
    private DateTimeOffset _nextRecoveryAttemptUtc;
    private bool _remoteEnabled;
    private string _remoteStatus = "Remote Control is off";
    private string _remoteAddress = string.Empty;
    private string _remoteOnboardingAddress = string.Empty;
    private string _remoteDiscoveryStatus = "mDNS discovery is off";
    private string _remoteNetworkHint = "Enable Remote Control while the PC and phone are on the same private Wi-Fi/LAN.";
    private string _remotePairingCode = string.Empty;
    private DateTimeOffset _remotePairingExpiresAt;
    private BitmapImage? _remotePairingQr;

    public MainViewModel(SoundboardStore? store = null, ProfileStore? profileStore = null, RemoteSettingsStore? remoteSettingsStore = null)
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _store = store ?? new SoundboardStore();
        string profilePath = Path.Combine(
            Path.GetDirectoryName(_store.StoragePath) ?? Path.GetTempPath(), "profiles.json");
        _profileStore = profileStore ?? new ProfileStore(profilePath);
        _profileDocument = _profileStore.Load();
        if (_profileDocument.Profiles.Count == 0)
        {
            var migrated = new ProfileModel { Name = "Default", Pads = _store.Load().ToList() };
            _profileDocument.Profiles.Add(migrated);
            _profileDocument.ActiveProfileId = migrated.Id;
        }
        _activeProfile = _profileDocument.Profiles.FirstOrDefault(
            profile => profile.Id == _profileDocument.ActiveProfileId) ?? _profileDocument.Profiles[0];
        _profileDocument.ActiveProfileId = _activeProfile.Id;
        _build = BuildInfo.Load(Path.Combine(AppContext.BaseDirectory, "BuildInfo.json"));
        Pads = [];
        Profiles = new ObservableCollection<ProfileModel>(_profileDocument.Profiles);
        UserPresets = [];
        InputDevices = [];
        OutputDevices = [];
        _mediaDeck = new MediaDeckService(_engine, () => IsRunning);
        _hotkeys = new GlobalHotkeyService(Dispatcher.CurrentDispatcher);
        remoteSettingsStore ??= new RemoteSettingsStore();
        RemoteSettingsDocument remoteSettings = remoteSettingsStore.Load();
        var remotePairing = new RemotePairingService(remoteSettingsStore, remoteSettings);
        _remoteStatePublisher = new RemoteStatePublisher();
        var remoteCommandDispatcher = new RemoteCommandDispatcher(this, _dispatcher);
#if REMOTE_MONITOR_SPIKE
        var remoteMonitorSpike = new RemoteMonitorWebRtcSpikeService(_engine, _mediaDeck);
        var remotePhoneMicSpike = new RemotePhoneMicWebRtcSpikeService(_engine);
        _remoteServer = new RemoteServerService(
            remoteSettingsStore, remoteSettings, remotePairing, remoteCommandDispatcher, _remoteStatePublisher, remoteMonitorSpike, remotePhoneMicSpike);
#else
        _remoteServer = new RemoteServerService(
            remoteSettingsStore, remoteSettings, remotePairing, remoteCommandDispatcher, _remoteStatePublisher);
#endif
        _remoteServer.Changed += OnRemoteServerChanged;
        _remoteEnabled = remoteSettings.Enabled;
        RemoteClients = [];
        PropertyChanged += OnRemoteObservableChanged;
        Pads.CollectionChanged += OnRemoteCollectionChanged;
        LoadProfileState(_activeProfile, populateCollections: true);
        _selectedProfile = _activeProfile;

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
        ApplyPresetCommand = new AsyncRelayCommand(_ => ApplySelectedPresetAsync(), _ => NativeReady && PresetIndex > 0);
        ApplyUserPresetCommand = new AsyncRelayCommand(_ => ApplySelectedUserPresetAsync(), _ => NativeReady && SelectedUserPreset is not null);
        SaveUserPresetCommand = new RelayCommand(_ => SaveUserPreset());
        UpdateUserPresetCommand = new RelayCommand(_ => UpdateUserPreset(), _ => SelectedUserPreset is not null);
        DuplicateUserPresetCommand = new RelayCommand(_ => DuplicateUserPreset(), _ => SelectedUserPreset is not null);
        RenameUserPresetCommand = new RelayCommand(_ => RenameUserPreset(), _ => SelectedUserPreset is not null);
        DeleteUserPresetCommand = new RelayCommand(_ => DeleteUserPreset(), _ => SelectedUserPreset is not null);
        ApplyProfileCommand = new AsyncRelayCommand(_ => ApplySelectedProfileAsync(), _ => SelectedProfile is not null && SelectedProfile != _activeProfile);
        NewProfileCommand = new RelayCommand(_ => NewProfile());
        DuplicateProfileCommand = new RelayCommand(_ => DuplicateProfile());
        RenameProfileCommand = new RelayCommand(_ => RenameProfile());
        DeleteProfileCommand = new RelayCommand(_ => DeleteProfile(), _ => Profiles.Count > 1);
        ApplyHotkeysCommand = new RelayCommand(_ => { RefreshHotkeys(); ScheduleSave(); });
        LoadMediaCommand = new AsyncRelayCommand(_ => ChooseMediaAsync());
        MediaPlayPauseCommand = new RelayCommand(_ => MediaPlayPause());
        MediaStopCommand = new RelayCommand(_ => { _mediaDeck.Stop(); RefreshMediaState(); });
        MediaBackCommand = new RelayCommand(_ => { _mediaDeck.Skip(-10.0); RefreshMediaState(); });
        MediaForwardCommand = new RelayCommand(_ => { _mediaDeck.Skip(10.0); RefreshMediaState(); });
        ClearMediaCommand = new RelayCommand(_ => { _mediaDeck.Clear(); RefreshMediaState(); ScheduleSave(); });
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
        RegenerateRemotePairingCommand = new RelayCommand(_ => { _remoteServer.RegeneratePairing(); RefreshRemoteUi(); }, _ => _remoteServer.IsRunning);
        RestartRemoteServerCommand = new AsyncRelayCommand(_ => RestartRemoteServerAsync(), _ => RemoteEnabled);
        RevokeRemoteClientCommand = new AsyncRelayCommand(parameter => RevokeRemoteClientAsync(parameter as RemoteClientDisplay), parameter => parameter is RemoteClientDisplay);

        UserPresets.CollectionChanged += OnUserPresetsChanged;

        _meterTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, OnMeterTick, Dispatcher.CurrentDispatcher);
        _meterTimer.Stop();
        _saveTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(450), DispatcherPriority.Background, OnSaveTick, Dispatcher.CurrentDispatcher);
        _saveTimer.Stop();
    }

    public event Action<SoundPadModel>? EditPadRequested;

    public ObservableCollection<SoundPadModel> Pads { get; }
    public ObservableCollection<ProfileModel> Profiles { get; }
    public ObservableCollection<UserPresetModel> UserPresets { get; }
    public ObservableCollection<AudioDevice> InputDevices { get; }
    public ObservableCollection<AudioDevice> OutputDevices { get; }
    public ObservableCollection<RemoteClientDisplay> RemoteClients { get; }

    public RelayCommand NavigateCommand { get; }
    public AsyncRelayCommand StartStopCommand { get; }
    public RelayCommand RefreshDevicesCommand { get; }
    public RelayCommand ToggleMuteCommand { get; }
    public RelayCommand ToggleVoiceFxCommand { get; }
    public AsyncRelayCommand StopAllCommand { get; }
    public RelayCommand ResetVoiceCommand { get; }
    public RelayCommand ResetMixerCommand { get; }
    public AsyncRelayCommand ApplyPresetCommand { get; }
    public AsyncRelayCommand ApplyUserPresetCommand { get; }
    public RelayCommand SaveUserPresetCommand { get; }
    public RelayCommand UpdateUserPresetCommand { get; }
    public RelayCommand DuplicateUserPresetCommand { get; }
    public RelayCommand RenameUserPresetCommand { get; }
    public RelayCommand DeleteUserPresetCommand { get; }
    public AsyncRelayCommand ApplyProfileCommand { get; }
    public RelayCommand NewProfileCommand { get; }
    public RelayCommand DuplicateProfileCommand { get; }
    public RelayCommand RenameProfileCommand { get; }
    public RelayCommand DeleteProfileCommand { get; }
    public RelayCommand ApplyHotkeysCommand { get; }
    public AsyncRelayCommand LoadMediaCommand { get; }
    public RelayCommand MediaPlayPauseCommand { get; }
    public RelayCommand MediaStopCommand { get; }
    public RelayCommand MediaBackCommand { get; }
    public RelayCommand MediaForwardCommand { get; }
    public RelayCommand ClearMediaCommand { get; }
    public AsyncRelayCommand AddPadsCommand { get; }
    public AsyncRelayCommand PlayPadCommand { get; }
    public RelayCommand StopPadCommand { get; }
    public RelayCommand EditPadCommand { get; }
    public RelayCommand DeletePadCommand { get; }
    public RelayCommand CopyDiagnosticsCommand { get; }
    public RelayCommand RegenerateRemotePairingCommand { get; }
    public AsyncRelayCommand RestartRemoteServerCommand { get; }
    public AsyncRelayCommand RevokeRemoteClientCommand { get; }

    public string Version => $"v{_build.Version}";
    public string Commit => _build.ShortCommit;
    public string NativeVersion => _engine.NativeVersion;
    internal string ActiveProfileName => _activeProfile.Name;

    public bool RemoteEnabled
    {
        get => _remoteEnabled;
        set
        {
            if (!SetProperty(ref _remoteEnabled, value)) return;
            try
            {
                _remoteServer.SetEnabledPreference(value);
                _ = ApplyRemoteEnabledAsync(value);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _remoteEnabled = !value;
                OnPropertyChanged(nameof(RemoteEnabled));
                CrashReporter.Report(exception, "Remote Control preference", false);
                RemoteStatus = "Could not save Remote Control settings";
                RemoteNetworkHint = "Check that GrassiBoard can write to your user AppData folder, then try again.";
            }
        }
    }

    public string RemoteStatus { get => _remoteStatus; private set => SetProperty(ref _remoteStatus, value); }
    public string RemoteAddress { get => _remoteAddress; private set => SetProperty(ref _remoteAddress, value); }
    public string RemoteOnboardingAddress { get => _remoteOnboardingAddress; private set => SetProperty(ref _remoteOnboardingAddress, value); }
    public string RemoteDiscoveryStatus { get => _remoteDiscoveryStatus; private set => SetProperty(ref _remoteDiscoveryStatus, value); }
    public string RemoteNetworkHint { get => _remoteNetworkHint; private set => SetProperty(ref _remoteNetworkHint, value); }
    public string RemotePairingCode { get => _remotePairingCode; private set => SetProperty(ref _remotePairingCode, value); }
    public BitmapImage? RemotePairingQr { get => _remotePairingQr; private set => SetProperty(ref _remotePairingQr, value); }
    public bool RemoteServerRunning => _remoteServer.IsRunning;
    public string RemotePairingExpiryLabel
    {
        get
        {
            if (_remotePairingExpiresAt == DateTimeOffset.MinValue) return string.Empty;
            TimeSpan remaining = _remotePairingExpiresAt - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero) return "Pairing code expired — regenerate it.";
            return $"Expires in {Math.Max(0, (int)remaining.TotalMinutes):00}:{remaining.Seconds:00}";
        }
    }
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
                ScheduleSave();
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
                ScheduleSave();
            }
        }
    }

    public AudioDevice? SelectedMonitorOutput
    {
        get => _selectedMonitorOutput;
        set
        {
            if (SetProperty(ref _selectedMonitorOutput, value))
            {
                _mediaDeck.MonitorDeviceId = value?.Id ?? string.Empty;
                ScheduleSave();
            }
        }
    }

    public ProfileModel? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                ApplyProfileCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ActiveProfileLabel => $"Active: {_activeProfile.Name}";

    public UserPresetModel? SelectedUserPreset
    {
        get => _selectedUserPreset;
        set
        {
            if (SetProperty(ref _selectedUserPreset, value))
            {
                OnPropertyChanged(nameof(SelectedUserPresetHotkey));
                RaisePresetCommandStates();
            }
        }
    }

    public string SelectedUserPresetHotkey
    {
        get => SelectedUserPreset?.Hotkey ?? string.Empty;
        set
        {
            if (SelectedUserPreset is null) return;
            SelectedUserPreset.Hotkey = value;
            OnPropertyChanged();
            RefreshHotkeys();
            ScheduleSave();
        }
    }

    public bool MinimizeToTray
    {
        get => _activeProfile.Preferences.MinimizeToTray;
        set { if (_activeProfile.Preferences.MinimizeToTray != value) { _activeProfile.Preferences.MinimizeToTray = value; OnPropertyChanged(); ScheduleSave(); } }
    }

    public bool StartMinimized
    {
        get => _activeProfile.Preferences.StartMinimized;
        set { if (_activeProfile.Preferences.StartMinimized != value) { _activeProfile.Preferences.StartMinimized = value; OnPropertyChanged(); ScheduleSave(); } }
    }

    public bool StartWithWindows
    {
        get => _activeProfile.Preferences.StartWithWindows;
        set
        {
            if (_activeProfile.Preferences.StartWithWindows == value) return;
            if (!StartupManager.SetEnabled(value))
            {
                HotkeyStatus = "Windows startup preference could not be changed.";
                return;
            }
            _activeProfile.Preferences.StartWithWindows = value;
            OnPropertyChanged();
            ScheduleSave();
        }
    }

    public string MuteHotkey { get => _activeProfile.Preferences.MuteHotkey; set => SetHotkey(value, (p, v) => p.MuteHotkey = v, nameof(MuteHotkey)); }
    public string StopAllHotkey { get => _activeProfile.Preferences.StopAllHotkey; set => SetHotkey(value, (p, v) => p.StopAllHotkey = v, nameof(StopAllHotkey)); }
    public string VoiceFxHotkey { get => _activeProfile.Preferences.VoiceFxHotkey; set => SetHotkey(value, (p, v) => p.VoiceFxHotkey = v, nameof(VoiceFxHotkey)); }
    public string PushToTalkHotkey { get => _activeProfile.Preferences.PushToTalkHotkey; set => SetHotkey(value, (p, v) => p.PushToTalkHotkey = v, nameof(PushToTalkHotkey)); }
    public string ShowHideHotkey { get => _activeProfile.Preferences.ShowHideHotkey; set => SetHotkey(value, (p, v) => p.ShowHideHotkey = v, nameof(ShowHideHotkey)); }
    public string MediaPlayPauseHotkey { get => _activeProfile.Preferences.MediaPlayPauseHotkey; set => SetHotkey(value, (p, v) => p.MediaPlayPauseHotkey = v, nameof(MediaPlayPauseHotkey)); }
    public string MediaStopHotkey { get => _activeProfile.Preferences.MediaStopHotkey; set => SetHotkey(value, (p, v) => p.MediaStopHotkey = v, nameof(MediaStopHotkey)); }
    public string MediaBackHotkey { get => _activeProfile.Preferences.MediaBackHotkey; set => SetHotkey(value, (p, v) => p.MediaBackHotkey = v, nameof(MediaBackHotkey)); }
    public string MediaForwardHotkey { get => _activeProfile.Preferences.MediaForwardHotkey; set => SetHotkey(value, (p, v) => p.MediaForwardHotkey = v, nameof(MediaForwardHotkey)); }

    public string HotkeyStatus
    {
        get => _hotkeyStatus;
        private set => SetProperty(ref _hotkeyStatus, value);
    }

    public string MediaFileName => _mediaDeck.FileName;
    public string MediaError => _mediaDeck.Error ?? string.Empty;
    public bool HasMedia => !string.IsNullOrWhiteSpace(_mediaDeck.FilePath);
    public bool MediaHasError => !string.IsNullOrWhiteSpace(_mediaDeck.Error);
    public bool MediaPlaying => _mediaDeck.IsPlaying;
    public string MediaPlayPauseLabel => MediaPlaying ? "Pause" : "Play";
    public string MediaTimeLabel => $"{FormatTime(_mediaDeck.PositionSeconds)} / {FormatTime(_mediaDeck.DurationSeconds)}";
    public double MediaDuration => Math.Max(0.01, _mediaDeck.DurationSeconds);
    public double MediaMeter => ToMeter(_statistics.MediaPeak > 0.0F ? _statistics.MediaPeak : _mediaDeck.Peak);
    public string MediaDb => FormatDb(_statistics.MediaPeak > 0.0F ? _statistics.MediaPeak : _mediaDeck.Peak);
    public double MediaBufferPercent
    {
        get
        {
            if (_statistics.MediaBufferCapacityFrames == 0U)
            {
                return 0.0;
            }

            uint alignmentFrames = _statistics.MediaActive != 0U ? _statistics.MediaAlignmentFrames : 0U;
            uint readAheadFrames = _statistics.MediaBufferFillFrames > alignmentFrames
                ? _statistics.MediaBufferFillFrames - alignmentFrames
                : 0U;
            return readAheadFrames * 100.0 / _statistics.MediaBufferCapacityFrames;
        }
    }

    public double MediaPosition
    {
        get => _mediaPosition;
        set
        {
            double clamped = Math.Clamp(double.IsFinite(value) ? value : 0.0, 0.0, MediaDuration);
            if (SetProperty(ref _mediaPosition, clamped) && !_mediaSeeking && !_mediaTimelineSeeking)
            {
                _mediaDeck.Seek(clamped);
                _mediaPositionHoldUntil = Environment.TickCount64 + 350L;
            }
        }
    }

    internal void BeginMediaTimelineSeek() => _mediaTimelineSeeking = true;

    internal void CommitMediaTimelineSeek()
    {
        if (!_mediaTimelineSeeking)
        {
            return;
        }

        _mediaTimelineSeeking = false;
        _mediaDeck.Seek(_mediaPosition);
        _mediaPositionHoldUntil = Environment.TickCount64 + 350L;
        RefreshMediaState();
    }

    public double MediaVolume
    {
        get => _mediaDeck.Volume;
        set
        {
            double clamped = Math.Clamp(double.IsFinite(value) ? value : 0.8, 0.0, 1.5);
            if (Math.Abs(_mediaDeck.Volume - clamped) < 0.0001) return;
            _mediaDeck.Volume = clamped;
            _activeProfile.Preferences.MediaVolume = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MediaVolumeLabel));
            ScheduleSave();
        }
    }

    public string MediaVolumeLabel => $"{MediaVolume:P0}";

    public bool MediaMonitorEnabled
    {
        get => _mediaDeck.MonitorEnabled;
        set { if (_mediaDeck.MonitorEnabled != value) { _mediaDeck.MonitorEnabled = value; _activeProfile.Preferences.MediaMonitorEnabled = value; OnPropertyChanged(); ScheduleSave(); } }
    }

    public bool MediaSendEnabled
    {
        get => _mediaDeck.SendEnabled;
        set { if (_mediaDeck.SendEnabled != value) { _mediaDeck.SendEnabled = value; _activeProfile.Preferences.MediaSendEnabled = value; OnPropertyChanged(); ScheduleSave(); } }
    }

    public double MediaSyncOffsetMilliseconds
    {
        get => _mediaDeck.SyncOffsetMilliseconds;
        set
        {
            double clamped = Math.Round(Math.Clamp(double.IsFinite(value) ? value : 0.0, -100.0, 100.0));
            if (Math.Abs(_mediaDeck.SyncOffsetMilliseconds - clamped) < 0.001) return;
            _mediaDeck.SyncOffsetMilliseconds = clamped;
            _activeProfile.Preferences.MediaSyncOffsetMilliseconds = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MediaSyncOffsetLabel));
            ScheduleSave();
        }
    }

    public string MediaSyncOffsetLabel => $"{MediaSyncOffsetMilliseconds:+0;-0;0} ms";

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
                ApplyEffectiveMicrophoneMute();
                OnPropertyChanged(nameof(MuteButtonLabel));
                ScheduleSave();
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
                ScheduleSave();
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
                ScheduleSave();
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
                ScheduleSave();
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
                ScheduleSave();
            }
        }
    }

    public string FormantLabel => $"{Formant.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture)} st";

    public bool PreserveVocalCharacter
    {
        get => _preserveVocalCharacter;
        set
        {
            if (SetProperty(ref _preserveVocalCharacter, value) && NativeReady)
            {
                _engine.SetFormantPreservation(value);
                ScheduleSave();
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
                ScheduleSave();
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

    public string GateThresholdLabel => $"{GateThreshold.ToString("0", CultureInfo.InvariantCulture)} dB";

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

    public string CompressorThresholdLabel => $"{CompressorThreshold.ToString("0", CultureInfo.InvariantCulture)} dB";

    public double CompressorRatio
    {
        get => _compressorRatio;
        set => SetMixerValue(ref _compressorRatio, value, 1.0, 20.0, nameof(CompressorRatio), nameof(CompressorRatioLabel));
    }

    public string CompressorRatioLabel => $"{CompressorRatio.ToString("0.0", CultureInfo.InvariantCulture)}:1";

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

    public string LimiterCeilingLabel => $"{LimiterCeiling.ToString("0.0", CultureInfo.InvariantCulture)} dB";

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

    public string DuckingAmountLabel => $"{DuckingAmount.ToString("0", CultureInfo.InvariantCulture)} dB";

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
        await InitializeRemoteAsync();
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

            OnPropertyChanged(nameof(HasPads));

            _meterTimer.Start();
            foreach (SoundPadModel pad in Pads)
            {
                await LoadPadAsync(pad);
            }
            if (!string.IsNullOrWhiteSpace(_activeProfile.Preferences.LastMediaPath))
            {
                await _mediaDeck.LoadAsync(_activeProfile.Preferences.LastMediaPath);
                RefreshMediaState();
            }
            RefreshHotkeys();
        }
        catch (Exception exception) when (exception is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            EngineStatus = $"Native engine unavailable · {exception.GetType().Name}";
            EngineDetail = "Use the complete matching GrassiBoard package.";
        }
    }

    private async Task InitializeRemoteAsync()
    {
        if (RemoteEnabled) await ApplyRemoteEnabledAsync(true);
        else RefreshRemoteUi();
    }

    private async Task ApplyRemoteEnabledAsync(bool enabled)
    {
        try
        {
            if (enabled) await _remoteServer.StartAsync();
            else await _remoteServer.StopAsync();
        }
        catch (Exception exception)
        {
            CrashReporter.Report(exception, "Remote Control lifecycle", false);
        }
        RefreshRemoteUi();
    }

    private async Task RestartRemoteServerAsync()
    {
        if (!RemoteEnabled) return;
        await _remoteServer.RestartAsync();
        RefreshRemoteUi();
    }

    private async Task RevokeRemoteClientAsync(RemoteClientDisplay? client)
    {
        if (client is null) return;
        await _remoteServer.RevokeClientAsync(client.Id);
        RefreshRemoteUi();
    }

    private void OnRemoteServerChanged()
    {
        if (_dispatcher.CheckAccess()) RefreshRemoteUi();
        else _dispatcher.BeginInvoke(RefreshRemoteUi);
    }

    private void RefreshRemoteUi()
    {
        RemoteStatus = _remoteServer.Status;
        RemoteAddress = _remoteServer.Address;
        RemoteOnboardingAddress = _remoteServer.OnboardingAddress;
        RemoteDiscoveryStatus = _remoteServer.DiscoveryStatus;
        RemoteNetworkHint = _remoteServer.NetworkHint;
        RemotePairingInfo? pairing = _remoteServer.CurrentPairing;
        RemotePairingCode = pairing?.Code ?? string.Empty;
        _remotePairingExpiresAt = pairing?.ExpiresAt ?? DateTimeOffset.MinValue;
        RemotePairingQr = RemoteQrCodeService.Create(pairing?.Url);
        RemoteClients.Clear();
        foreach (RemoteClientDisplay client in _remoteServer.GetClientDisplays()) RemoteClients.Add(client);
        OnPropertyChanged(nameof(RemoteServerRunning));
        OnPropertyChanged(nameof(RemotePairingExpiryLabel));
        RegenerateRemotePairingCommand.RaiseCanExecuteChanged();
        RestartRemoteServerCommand.RaiseCanExecuteChanged();
    }

    private void OnRemoteObservableChanged(object? sender, PropertyChangedEventArgs e) => _remoteStatePublisher.Invalidate();

    private void OnRemoteCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => _remoteStatePublisher.Invalidate();

    internal async Task RemoteStartEngineAsync()
    {
        if (!IsRunning) await StartStopAsync();
    }

    internal async Task RemoteStopEngineAsync()
    {
        if (IsRunning) await StartStopAsync();
    }

    internal Task RemoteStopAllAsync() => StopAllAsync();

    internal void RemoteResetVoice() => ResetVoice();

    internal async Task<bool> RemoteApplyUserPresetAsync(Guid presetId)
    {
        UserPresetModel? preset = UserPresets.FirstOrDefault(item => item.Id == presetId);
        if (preset is null) return false;
        await ApplySnapshotSmoothAsync(preset.State);
        return true;
    }

    internal async Task<bool> RemotePlayPadAsync(Guid padId)
    {
        SoundPadModel? pad = Pads.FirstOrDefault(item => item.Id == padId);
        if (pad is null) return false;
        await PlayPadAsync(pad);
        return true;
    }

    internal bool RemoteStopPad(Guid padId)
    {
        SoundPadModel? pad = Pads.FirstOrDefault(item => item.Id == padId);
        if (pad is null) return false;
        StopPad(pad);
        return true;
    }

    internal void RemoteMediaPlayPause() => MediaPlayPause();
    internal void RemoteMediaStop() { _mediaDeck.Stop(); RefreshMediaState(); }
    internal void RemoteMediaSkip(double seconds) { _mediaDeck.Skip(seconds); RefreshMediaState(); }
    internal void RemoteMediaSeek(double seconds) { MediaPosition = seconds; RefreshMediaState(); }

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
        bool restartOnPress,
        string hotkey)
    {
        bool fileChanged = !string.Equals(pad.FilePath, filePath, StringComparison.OrdinalIgnoreCase);
        pad.Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(filePath) : title.Trim();
        pad.FilePath = Path.GetFullPath(filePath);
        pad.Volume = volume;
        pad.Loop = loop;
        pad.RestartOnPress = restartOnPress;
        pad.Hotkey = hotkey;
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
        _automaticRecoveryArmed = false;
        try
        {
            _mediaDeck.Stop();
            StopAllSounds();
            NativeResult result = await Task.Run(_engine.Stop);
            _mediaDeck.SetEngineRunning(false);
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
                _forcedRecoveryMute = false;
                _failedInputDeviceId = string.Empty;
                _automaticRecoveryArmed = true;
                ApplyEffectiveMicrophoneMute();
                _mediaDeck.SetEngineRunning(true);
                EngineStatus = "Virtual microphone is live";
                EngineDetail = $"48 kHz · {QualityLabel} · Media/Soundboard mixer ready";
            }
            else
            {
                _automaticRecoveryArmed = false;
                _mediaDeck.Stop();
                StopAllSounds();
                NativeResult result = await Task.Run(_engine.Stop);
                _mediaDeck.SetEngineRunning(false);
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
                inputs.FirstOrDefault(device => device.Id == _activeProfile.InputDeviceId) ??
                inputs.FirstOrDefault(device => device.IsDefault && !IsExternalVirtualEndpoint(device)) ??
                inputs.FirstOrDefault(device => !IsExternalVirtualEndpoint(device)) ??
                inputs.FirstOrDefault();
            SelectedOutput = outputs.FirstOrDefault(device => device.Id == previousOutput?.Id) ??
                outputs.FirstOrDefault(device => device.Id == _activeProfile.OutputDeviceId) ??
                outputs.FirstOrDefault(output => FindPairedCaptureEndpoint(output) is not null) ??
                outputs.FirstOrDefault(device => device.IsDefault) ??
                outputs.FirstOrDefault();
            SelectedMonitorOutput = outputs.FirstOrDefault(device => device.Id == _activeProfile.MonitorDeviceId) ??
                outputs.FirstOrDefault(device => device.Id != SelectedOutput?.Id && !IsExternalVirtualEndpoint(device)) ??
                outputs.FirstOrDefault(device => device.IsDefault) ?? outputs.FirstOrDefault();
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
        if (!NativeReady)
        {
            return;
        }

        _engine.SetPitch((float)Pitch);
        _engine.SetFinePitch((float)FinePitch);
        _engine.SetVoiceFxEnabled(VoiceFxEnabled);
        _engine.SetFormant((float)Formant);
        _engine.SetFormantPreservation(PreserveVocalCharacter);
        _engine.SetQuality((uint)QualityIndex);
        ApplyEffectiveMicrophoneMute();
    }

    private void ApplyEffectiveMicrophoneMute() =>
        _engine.SetMicrophoneMuted(MicrophoneMuted || _forcedRecoveryMute);

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

    private async Task ApplySelectedPresetAsync()
    {
        if (PresetIndex > 0)
        {
            await ApplySnapshotSmoothAsync(CreateBuiltInSnapshot(PresetIndex));
        }
    }

    private AudioStateSnapshot CreateBuiltInSnapshot(int index)
    {
        AudioStateSnapshot target = CaptureAudioState();
        switch (index)
        {
            case 2:
                SetSnapshotMixer(target, 3.0, -2.0, -1.0, true, -50.0, true, -18.0, 4.0, true, -1.0, true, 6.0, true, 100.0);
                break;
            case 3:
                SetSnapshotMixer(target, 2.0, 0.0, -1.0, true, -55.0, true, -16.0, 3.0, true, -1.0, true, 10.0, true, 85.0);
                break;
            case 4:
                SetSnapshotMixer(target, 4.0, -4.0, -2.0, true, -48.0, true, -20.0, 4.0, true, -1.0, true, 12.0, true, 70.0);
                break;
            default:
                SetSnapshotMixer(target, 0.0, 0.0, 0.0, false, -55.0, false, -18.0, 3.0, true, -1.0, false, 9.0, true, 100.0);
                break;
        }
        return target;
    }

    internal void ApplyPreset(int index)
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

    private static void SetSnapshotMixer(
        AudioStateSnapshot state,
        double micGain, double boardGain, double masterGain,
        bool gate, double gateThreshold,
        bool compressor, double compressorThreshold, double compressorRatio,
        bool limiter, double limiterCeiling,
        bool ducking, double duckingAmount,
        bool clippingProtection, double wetMix)
    {
        state.MicGain = micGain;
        state.SoundboardGain = boardGain;
        state.MasterGain = masterGain;
        state.NoiseGateEnabled = gate;
        state.GateThreshold = gateThreshold;
        state.CompressorEnabled = compressor;
        state.CompressorThreshold = compressorThreshold;
        state.CompressorRatio = compressorRatio;
        state.LimiterEnabled = limiter;
        state.LimiterCeiling = limiterCeiling;
        state.DuckingEnabled = ducking;
        state.DuckingAmount = duckingAmount;
        state.ClippingProtectionEnabled = clippingProtection;
        state.PitchWetMix = wetMix;
    }

    private AudioStateSnapshot CaptureAudioState() => new()
    {
        VoiceFxEnabled = VoiceFxEnabled,
        Pitch = Pitch,
        FinePitch = FinePitch,
        Formant = Formant,
        PreserveVocalCharacter = PreserveVocalCharacter,
        QualityIndex = QualityIndex,
        MicGain = MicGain,
        SoundboardGain = SoundboardGain,
        MasterGain = MasterGain,
        NoiseGateEnabled = NoiseGateEnabled,
        GateThreshold = GateThreshold,
        CompressorEnabled = CompressorEnabled,
        CompressorThreshold = CompressorThreshold,
        CompressorRatio = CompressorRatio,
        LimiterEnabled = LimiterEnabled,
        LimiterCeiling = LimiterCeiling,
        DuckingEnabled = DuckingEnabled,
        DuckingAmount = DuckingAmount,
        ClippingProtectionEnabled = ClippingProtectionEnabled,
        PitchWetMix = PitchWetMix
    };

    private async Task ApplySnapshotSmoothAsync(AudioStateSnapshot target)
    {
        // Managed-only callers (including smoke tests and a native-engine-unavailable UI)
        // do not have a pumping WPF Dispatcher for an animated transition. Apply the
        // authoritative state immediately; smooth transitions are only meaningful when
        // the native engine is available to render them.
        if (!NativeReady)
        {
            ApplySnapshotImmediate(target);
            return;
        }

        _presetTransition?.Cancel();
        _presetTransition?.Dispose();
        var transition = new CancellationTokenSource();
        _presetTransition = transition;
        CancellationToken token = transition.Token;
        AudioStateSnapshot start = CaptureAudioState();
        const int steps = 12;
        _applyingPreset = true;
        try
        {
            if (target.VoiceFxEnabled) VoiceFxEnabled = true;
            if (target.PreserveVocalCharacter) PreserveVocalCharacter = true;
            if (target.NoiseGateEnabled) NoiseGateEnabled = true;
            if (target.CompressorEnabled) CompressorEnabled = true;
            if (target.LimiterEnabled) LimiterEnabled = true;
            if (target.DuckingEnabled) DuckingEnabled = true;
            if (target.ClippingProtectionEnabled) ClippingProtectionEnabled = true;

            for (int step = 1; step <= steps; ++step)
            {
                token.ThrowIfCancellationRequested();
                double mix = step / (double)steps;
                Pitch = Lerp(start.Pitch, target.Pitch, mix);
                FinePitch = Lerp(start.FinePitch, target.FinePitch, mix);
                Formant = Lerp(start.Formant, target.Formant, mix);
                MicGain = Lerp(start.MicGain, target.MicGain, mix);
                SoundboardGain = Lerp(start.SoundboardGain, target.SoundboardGain, mix);
                MasterGain = Lerp(start.MasterGain, target.MasterGain, mix);
                GateThreshold = Lerp(start.GateThreshold, target.GateThreshold, mix);
                CompressorThreshold = Lerp(start.CompressorThreshold, target.CompressorThreshold, mix);
                CompressorRatio = Lerp(start.CompressorRatio, target.CompressorRatio, mix);
                LimiterCeiling = Lerp(start.LimiterCeiling, target.LimiterCeiling, mix);
                DuckingAmount = Lerp(start.DuckingAmount, target.DuckingAmount, mix);
                PitchWetMix = Lerp(start.PitchWetMix, target.PitchWetMix, mix);
                ApplyMixerState();
                await Task.Delay(TimeSpan.FromMilliseconds(200.0 / steps), token);
            }

            VoiceFxEnabled = target.VoiceFxEnabled;
            PreserveVocalCharacter = target.PreserveVocalCharacter;
            QualityIndex = target.QualityIndex;
            NoiseGateEnabled = target.NoiseGateEnabled;
            CompressorEnabled = target.CompressorEnabled;
            LimiterEnabled = target.LimiterEnabled;
            DuckingEnabled = target.DuckingEnabled;
            ClippingProtectionEnabled = target.ClippingProtectionEnabled;
            ApplyVoiceState();
            ApplyMixerState();
        }
        catch (OperationCanceledException)
        {
            // A newer preset transition owns the current targets.
        }
        finally
        {
            if (ReferenceEquals(_presetTransition, transition))
            {
                _applyingPreset = false;
                ScheduleSave();
            }
        }
    }

    private void ApplySnapshotImmediate(AudioStateSnapshot target)
    {
        _applyingPreset = true;
        try
        {
            VoiceFxEnabled = target.VoiceFxEnabled;
            Pitch = target.Pitch;
            FinePitch = target.FinePitch;
            Formant = target.Formant;
            PreserveVocalCharacter = target.PreserveVocalCharacter;
            QualityIndex = target.QualityIndex;
            MicGain = target.MicGain;
            SoundboardGain = target.SoundboardGain;
            MasterGain = target.MasterGain;
            NoiseGateEnabled = target.NoiseGateEnabled;
            GateThreshold = target.GateThreshold;
            CompressorEnabled = target.CompressorEnabled;
            CompressorThreshold = target.CompressorThreshold;
            CompressorRatio = target.CompressorRatio;
            LimiterEnabled = target.LimiterEnabled;
            LimiterCeiling = target.LimiterCeiling;
            DuckingEnabled = target.DuckingEnabled;
            DuckingAmount = target.DuckingAmount;
            ClippingProtectionEnabled = target.ClippingProtectionEnabled;
            PitchWetMix = target.PitchWetMix;
        }
        finally
        {
            _applyingPreset = false;
            ScheduleSave();
        }
    }

    private static double Lerp(double start, double end, double mix) => start + (end - start) * mix;

    private async Task ApplySelectedUserPresetAsync()
    {
        if (SelectedUserPreset is not null)
        {
            await ApplySnapshotSmoothAsync(SelectedUserPreset.State);
        }
    }

    private void SaveUserPreset()
    {
        string? name = TextPromptWindow.Prompt("Save preset", "Name this Voice + Mixer preset");
        if (name is null) return;
        var preset = new UserPresetModel { Name = name, State = CaptureAudioState() };
        SubscribePreset(preset);
        UserPresets.Add(preset);
        SelectedUserPreset = preset;
        ScheduleSave();
        RefreshHotkeys();
    }

    private void UpdateUserPreset()
    {
        if (SelectedUserPreset is null) return;
        SelectedUserPreset.State = CaptureAudioState();
        OnPropertyChanged(nameof(SelectedUserPreset));
        ScheduleSave();
    }

    private void DuplicateUserPreset()
    {
        if (SelectedUserPreset is null) return;
        UserPresetModel duplicate = SelectedUserPreset.Clone();
        SubscribePreset(duplicate);
        UserPresets.Add(duplicate);
        SelectedUserPreset = duplicate;
        ScheduleSave();
    }

    private void RenameUserPreset()
    {
        if (SelectedUserPreset is null) return;
        string? name = TextPromptWindow.Prompt("Rename preset", "Preset name", SelectedUserPreset.Name);
        if (name is null) return;
        SelectedUserPreset.Name = name;
        ScheduleSave();
    }

    private void DeleteUserPreset()
    {
        if (SelectedUserPreset is null) return;
        if (MessageBox.Show($"Delete preset ‘{SelectedUserPreset.Name}’?", "Delete preset",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        SelectedUserPreset.PropertyChanged -= OnUserPresetPropertyChanged;
        UserPresets.Remove(SelectedUserPreset);
        SelectedUserPreset = UserPresets.FirstOrDefault();
        ScheduleSave();
        RefreshHotkeys();
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
            ScheduleSave();
        }
    }

    private static string FormatSignedDb(double value) =>
        $"{value.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture)} dB";

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
        RefreshMediaState();
        OnPropertyChanged(nameof(PitchLatencyMilliseconds));
        OnPropertyChanged(nameof(MediaAlignmentMilliseconds));
        OnPropertyChanged(nameof(EstimatedTotalLatencyMilliseconds));

        if (IsRunning && _statistics.Running == 0U)
        {
            IsRunning = false;
            EngineStatus = "Audio stream stopped unexpectedly";
            EngineDetail = _engine.ReadLastError();
            StopAllSounds();
            if (_automaticRecoveryArmed)
            {
                _failedInputDeviceId = SelectedInput?.Id ?? string.Empty;
                _ = RecoverAudioRouteAsync();
            }
        }
        else if (!IsRunning && _automaticRecoveryArmed &&
                 !_automaticRecoveryInProgress && DateTimeOffset.UtcNow >= _nextRecoveryAttemptUtc)
        {
            _ = RecoverAudioRouteAsync();
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

        OnPropertyChanged(nameof(RemotePairingExpiryLabel));
        if (IsSettingsPage)
        {
            OnPropertyChanged(nameof(DiagnosticsText));
        }
    }

    internal void AttachWindowServices(nint window, Action showHide)
    {
        _hotkeys.Attach(window);
        _showHideAction = showHide;
        RefreshHotkeys();
    }

    private async Task RecoverAudioRouteAsync()
    {
        if (_automaticRecoveryInProgress || !_automaticRecoveryArmed || _disposed)
        {
            return;
        }

        _automaticRecoveryInProgress = true;
        IsBusy = true;
        try
        {
            _mediaDeck.SetEngineRunning(false);
            await Task.Run(_engine.Stop);

            (IReadOnlyList<AudioDevice> inputs, IReadOnlyList<AudioDevice> outputs) = await Task.Run(() =>
                (_engine.EnumerateDevices(true), _engine.EnumerateDevices(false)));

            _captureDevices = inputs;
            AudioDevice? previousOutput = SelectedOutput;
            AudioDevice? previousMonitor = SelectedMonitorOutput;
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

            AudioDevice? recoveredInput = DeviceRecoveryPolicy.SelectNextInput(inputs, _failedInputDeviceId);
            AudioDevice? recoveredOutput = outputs.FirstOrDefault(device => device.Id == previousOutput?.Id) ??
                outputs.FirstOrDefault(output => FindPairedCaptureEndpoint(output) is not null) ??
                outputs.FirstOrDefault(device => device.IsDefault) ??
                outputs.FirstOrDefault();

            SelectedInput = recoveredInput;
            SelectedOutput = recoveredOutput;
            SelectedMonitorOutput = outputs.FirstOrDefault(device => device.Id == previousMonitor?.Id) ??
                outputs.FirstOrDefault(device => device.Id != recoveredOutput?.Id && !IsExternalVirtualEndpoint(device)) ??
                outputs.FirstOrDefault(device => device.IsDefault) ??
                outputs.FirstOrDefault();

            if (recoveredInput is null || recoveredOutput is null)
            {
                _forcedRecoveryMute = true;
                ApplyEffectiveMicrophoneMute();
                IsRunning = false;
                EngineStatus = recoveredInput is null
                    ? "Waiting for an available microphone"
                    : "Waiting for the virtual output";
                EngineDetail = "The virtual microphone is safely muted. GrassiBoard will retry automatically without changing Voice or Mixer settings.";
                _nextRecoveryAttemptUtc = DateTimeOffset.UtcNow.AddSeconds(2);
                return;
            }

            EngineStatus = $"Recovering with {recoveredInput.Name}...";
            NativeResult result = await Task.Run(() => _engine.Start(recoveredInput.Id, recoveredOutput.Id));
            if (result != NativeResult.Ok)
            {
                _forcedRecoveryMute = true;
                ApplyEffectiveMicrophoneMute();
                IsRunning = false;
                EngineStatus = "Automatic microphone recovery is retrying";
                EngineDetail = $"{result} · {_engine.ReadLastError()}";
                _failedInputDeviceId = recoveredInput.Id;
                _nextRecoveryAttemptUtc = DateTimeOffset.UtcNow.AddSeconds(2);
                return;
            }

            _forcedRecoveryMute = false;
            ApplyVoiceState();
            ApplyMixerState();
            IsRunning = true;
            _mediaDeck.SetEngineRunning(true);
            EngineStatus = "Virtual microphone recovered";
            EngineDetail = $"Switched automatically to {recoveredInput.Name} · Voice and Mixer settings preserved";
            _failedInputDeviceId = string.Empty;
            _nextRecoveryAttemptUtc = DateTimeOffset.MinValue;
        }
        catch (Exception exception)
        {
            _forcedRecoveryMute = true;
            ApplyEffectiveMicrophoneMute();
            IsRunning = false;
            EngineStatus = "Automatic microphone recovery is retrying";
            EngineDetail = exception.Message;
            _nextRecoveryAttemptUtc = DateTimeOffset.UtcNow.AddSeconds(2);
            CrashReporter.Report(exception, "Automatic audio device recovery", false);
        }
        finally
        {
            IsBusy = false;
            _automaticRecoveryInProgress = false;
        }
    }

    internal bool HandleWindowMessage(int message, nint wParam) => _hotkeys.HandleMessage(message, wParam);

    internal void TriggerStopAll() => _ = StopAllAsync();

    private Action? _showHideAction;

    private void RefreshHotkeys()
    {
        var registrations = new List<HotkeyRegistration>
        {
            new(MuteHotkey, "Mute/Unmute microphone", () => MicrophoneMuted = !MicrophoneMuted),
            new(StopAllHotkey, "Stop All", () => _ = StopAllAsync()),
            new(VoiceFxHotkey, "Voice FX", () => VoiceFxEnabled = !VoiceFxEnabled),
            new(ShowHideHotkey, "Show/Hide GrassiBoard", () => _showHideAction?.Invoke()),
            new(MediaPlayPauseHotkey, "Media Play/Pause", MediaPlayPause),
            new(MediaStopHotkey, "Media Stop", () => { _mediaDeck.Stop(); RefreshMediaState(); }),
            new(MediaBackHotkey, "Media -10 seconds", () => { _mediaDeck.Skip(-10.0); RefreshMediaState(); }),
            new(MediaForwardHotkey, "Media +10 seconds", () => { _mediaDeck.Skip(10.0); RefreshMediaState(); })
        };
        registrations.AddRange(Pads.Where(pad => !string.IsNullOrWhiteSpace(pad.Hotkey)).Select(pad =>
            new HotkeyRegistration(pad.Hotkey, $"Sound Pad ‘{pad.Title}’", () => _ = PlayPadAsync(pad))));
        registrations.AddRange(UserPresets.Where(preset => !string.IsNullOrWhiteSpace(preset.Hotkey)).Select(preset =>
            new HotkeyRegistration(preset.Hotkey, $"Preset ‘{preset.Name}’", () => _ = ApplySnapshotSmoothAsync(preset.State))));
        HotkeyStatus = _hotkeys.Refresh(registrations, PushToTalkHotkey, held => MicrophoneMuted = !held);
    }

    private void SetHotkey(string value, Action<AppPreferences, string> update, string propertyName)
    {
        string safe = value?.Trim() ?? string.Empty;
        update(_activeProfile.Preferences, safe);
        OnPropertyChanged(propertyName);
        ScheduleSave();
    }

    private async Task ChooseMediaAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load Local Media",
            Filter = "Supported media|*.wav;*.mp3;*.flac;*.aac;*.m4a;*.mp4;*.mov;*.wma;*.aiff|Audio|*.wav;*.mp3;*.flac;*.aac;*.m4a;*.wma;*.aiff|Video audio track|*.mp4;*.mov",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog() != true) return;
        await _mediaDeck.LoadAsync(dialog.FileName);
        _activeProfile.Preferences.LastMediaPath = dialog.FileName;
        RefreshMediaState();
        ScheduleSave();
    }

    private void MediaPlayPause()
    {
        _mediaDeck.PlayPause();
        RefreshMediaState();
    }

    private void RefreshMediaState()
    {
        if (!_mediaTimelineSeeking && Environment.TickCount64 >= _mediaPositionHoldUntil)
        {
            _mediaSeeking = true;
            try
            {
                MediaPosition = _mediaDeck.PositionSeconds;
            }
            finally
            {
                _mediaSeeking = false;
            }
        }
        OnPropertyChanged(nameof(MediaFileName));
        OnPropertyChanged(nameof(MediaError));
        OnPropertyChanged(nameof(HasMedia));
        OnPropertyChanged(nameof(MediaHasError));
        OnPropertyChanged(nameof(MediaPlaying));
        OnPropertyChanged(nameof(MediaPlayPauseLabel));
        OnPropertyChanged(nameof(MediaTimeLabel));
        OnPropertyChanged(nameof(MediaDuration));
        OnPropertyChanged(nameof(MediaMeter));
        OnPropertyChanged(nameof(MediaDb));
        OnPropertyChanged(nameof(MediaBufferPercent));
    }

    private static string FormatTime(double seconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(Math.Max(0.0, double.IsFinite(seconds) ? seconds : 0.0));
        return time.TotalHours >= 1.0 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
    }

    private void CaptureActiveProfile()
    {
        _activeProfile.InputDeviceId = SelectedInput?.Id ?? _activeProfile.InputDeviceId;
        _activeProfile.OutputDeviceId = SelectedOutput?.Id ?? _activeProfile.OutputDeviceId;
        _activeProfile.MonitorDeviceId = SelectedMonitorOutput?.Id ?? _activeProfile.MonitorDeviceId;
        _activeProfile.AudioState = CaptureAudioState();
        _activeProfile.Pads = Pads.ToList();
        _activeProfile.UserPresets = UserPresets.ToList();
        _activeProfile.Preferences.MediaVolume = MediaVolume;
        _activeProfile.Preferences.MediaMonitorEnabled = MediaMonitorEnabled;
        _activeProfile.Preferences.MediaSendEnabled = MediaSendEnabled;
        _activeProfile.Preferences.MediaSyncOffsetMilliseconds = MediaSyncOffsetMilliseconds;
        _activeProfile.Preferences.LastMediaPath = _mediaDeck.FilePath;
        _profileDocument.ActiveProfileId = _activeProfile.Id;
    }

    private void LoadProfileState(ProfileModel profile, bool populateCollections)
    {
        AudioStateSnapshot state = profile.AudioState ?? new AudioStateSnapshot();
        _voiceFxEnabled = state.VoiceFxEnabled;
        _pitch = Safe(state.Pitch, -12.0, 12.0, 0.0);
        _finePitch = Safe(state.FinePitch, -100.0, 100.0, 0.0);
        _formant = Safe(state.Formant, -12.0, 12.0, 0.0);
        _preserveVocalCharacter = state.PreserveVocalCharacter;
        _qualityIndex = Math.Clamp(state.QualityIndex, 0, 2);
        _micGain = Safe(state.MicGain, -24.0, 24.0, 0.0);
        _soundboardGain = Safe(state.SoundboardGain, -24.0, 24.0, 0.0);
        _masterGain = Safe(state.MasterGain, -24.0, 12.0, 0.0);
        _noiseGateEnabled = state.NoiseGateEnabled;
        _gateThreshold = Safe(state.GateThreshold, -80.0, -20.0, -55.0);
        _compressorEnabled = state.CompressorEnabled;
        _compressorThreshold = Safe(state.CompressorThreshold, -40.0, -3.0, -18.0);
        _compressorRatio = Safe(state.CompressorRatio, 1.0, 20.0, 3.0);
        _limiterEnabled = state.LimiterEnabled;
        _limiterCeiling = Safe(state.LimiterCeiling, -12.0, 0.0, -1.0);
        _duckingEnabled = state.DuckingEnabled;
        _duckingAmount = Safe(state.DuckingAmount, 0.0, 30.0, 9.0);
        _clippingProtectionEnabled = state.ClippingProtectionEnabled;
        _pitchWetMix = Safe(state.PitchWetMix, 0.0, 100.0, 100.0);
        _mediaDeck.Volume = Safe(profile.Preferences.MediaVolume, 0.0, 1.5, 0.8);
        _mediaDeck.MonitorEnabled = profile.Preferences.MediaMonitorEnabled;
        _mediaDeck.SendEnabled = profile.Preferences.MediaSendEnabled;
        _mediaDeck.SyncOffsetMilliseconds = Safe(
            profile.Preferences.MediaSyncOffsetMilliseconds, -100.0, 100.0, 0.0);

        if (populateCollections)
        {
            foreach (SoundPadModel pad in profile.Pads ?? []) AddPadToCollection(pad);
            foreach (UserPresetModel preset in profile.UserPresets ?? [])
            {
                SubscribePreset(preset);
                UserPresets.Add(preset);
            }
            _selectedUserPreset = UserPresets.FirstOrDefault();
        }
        RaiseAllAudioProperties();
        RaisePreferenceProperties();
    }

    private static double Safe(double value, double minimum, double maximum, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;

    private async Task ApplySelectedProfileAsync()
    {
        if (SelectedProfile is null || SelectedProfile == _activeProfile) return;
        CaptureActiveProfile();
        SaveProfiles();
        if (IsRunning) await StopAllAsync();
        foreach (SoundPadModel pad in Pads) pad.PropertyChanged -= OnPadPropertyChanged;
        foreach (UserPresetModel preset in UserPresets) preset.PropertyChanged -= OnUserPresetPropertyChanged;
        Pads.Clear();
        UserPresets.Clear();
        _activeProfile = SelectedProfile;
        _profileDocument.ActiveProfileId = _activeProfile.Id;
        LoadProfileState(_activeProfile, populateCollections: true);
        if (!StartupManager.SetEnabled(_activeProfile.Preferences.StartWithWindows))
        {
            HotkeyStatus = "The selected profile loaded, but its Windows startup preference could not be applied.";
        }
        ApplyVoiceState();
        ApplyMixerState();
        RefreshDevices();
        OnPropertyChanged(nameof(HasPads));
        OnPropertyChanged(nameof(ActiveProfileLabel));
        foreach (SoundPadModel pad in Pads) await LoadPadAsync(pad);
        if (!string.IsNullOrWhiteSpace(_activeProfile.Preferences.LastMediaPath))
            await _mediaDeck.LoadAsync(_activeProfile.Preferences.LastMediaPath);
        else _mediaDeck.Clear();
        RefreshMediaState();
        RefreshHotkeys();
        RaiseCommandStates();
        ScheduleSave();
    }

    private void NewProfile()
    {
        string? name = TextPromptWindow.Prompt("New profile", "Profile name", "New profile");
        if (name is null) return;
        var profile = new ProfileModel { Name = name };
        Profiles.Add(profile);
        _profileDocument.Profiles.Add(profile);
        SelectedProfile = profile;
        DeleteProfileCommand.RaiseCanExecuteChanged();
        ScheduleSave();
    }

    private void DuplicateProfile()
    {
        CaptureActiveProfile();
        ProfileModel duplicate = _activeProfile.Clone();
        Profiles.Add(duplicate);
        _profileDocument.Profiles.Add(duplicate);
        SelectedProfile = duplicate;
        DeleteProfileCommand.RaiseCanExecuteChanged();
        ScheduleSave();
    }

    private void RenameProfile()
    {
        ProfileModel profile = SelectedProfile ?? _activeProfile;
        string? name = TextPromptWindow.Prompt("Rename profile", "Profile name", profile.Name);
        if (name is null) return;
        profile.Name = name;
        if (profile == _activeProfile) OnPropertyChanged(nameof(ActiveProfileLabel));
        ScheduleSave();
    }

    private void DeleteProfile()
    {
        ProfileModel? profile = SelectedProfile;
        if (profile is null || Profiles.Count <= 1) return;
        if (profile == _activeProfile)
        {
            MessageBox.Show("Switch to another profile before deleting the active profile.", "Delete profile",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show($"Delete profile ‘{profile.Name}’?", "Delete profile",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Profiles.Remove(profile);
        _profileDocument.Profiles.Remove(profile);
        SelectedProfile = _activeProfile;
        DeleteProfileCommand.RaiseCanExecuteChanged();
        ScheduleSave();
    }

    private void RaiseAllAudioProperties()
    {
        string[] names = [nameof(VoiceFxEnabled), nameof(VoiceFxLabel), nameof(Pitch), nameof(PitchLabel),
            nameof(FinePitch), nameof(FinePitchLabel), nameof(Formant), nameof(FormantLabel),
            nameof(PreserveVocalCharacter), nameof(QualityIndex), nameof(QualityLabel), nameof(MicGain),
            nameof(MicGainLabel), nameof(SoundboardGain), nameof(SoundboardGainLabel), nameof(MasterGain),
            nameof(MasterGainLabel), nameof(NoiseGateEnabled), nameof(GateThreshold), nameof(GateThresholdLabel),
            nameof(CompressorEnabled), nameof(CompressorThreshold), nameof(CompressorThresholdLabel),
            nameof(CompressorRatio), nameof(CompressorRatioLabel), nameof(LimiterEnabled), nameof(LimiterCeiling),
            nameof(LimiterCeilingLabel), nameof(DuckingEnabled), nameof(DuckingAmount), nameof(DuckingAmountLabel),
            nameof(ClippingProtectionEnabled), nameof(PitchWetMix), nameof(PitchWetMixLabel)];
        foreach (string name in names) OnPropertyChanged(name);
    }

    private void RaisePreferenceProperties()
    {
        string[] names = [nameof(MinimizeToTray), nameof(StartMinimized), nameof(StartWithWindows), nameof(MuteHotkey),
            nameof(StopAllHotkey), nameof(VoiceFxHotkey), nameof(PushToTalkHotkey), nameof(ShowHideHotkey),
            nameof(MediaPlayPauseHotkey), nameof(MediaStopHotkey), nameof(MediaBackHotkey), nameof(MediaForwardHotkey),
            nameof(MediaVolume), nameof(MediaMonitorEnabled), nameof(MediaSendEnabled),
            nameof(MediaSyncOffsetMilliseconds), nameof(MediaSyncOffsetLabel)];
        foreach (string name in names) OnPropertyChanged(name);
    }

    private void SubscribePreset(UserPresetModel preset) => preset.PropertyChanged += OnUserPresetPropertyChanged;

    private void OnUserPresetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _remoteStatePublisher.Invalidate();
        ScheduleSave();
        if (e.PropertyName == nameof(UserPresetModel.Hotkey)) RefreshHotkeys();
    }

    private void OnUserPresetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _remoteStatePublisher.Invalidate();
        RaisePresetCommandStates();
        ScheduleSave();
    }

    private void RaisePresetCommandStates()
    {
        ApplyUserPresetCommand.RaiseCanExecuteChanged();
        UpdateUserPresetCommand.RaiseCanExecuteChanged();
        DuplicateUserPresetCommand.RaiseCanExecuteChanged();
        RenameUserPresetCommand.RaiseCanExecuteChanged();
        DeleteUserPresetCommand.RaiseCanExecuteChanged();
    }

    private void SaveProfiles()
    {
        CaptureActiveProfile();
        _profileStore.Save(_profileDocument);
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
        text.AppendLine($"Media alignment: {_statistics.MediaAlignmentFrames} samples ({MediaAlignmentMilliseconds:0.0} ms) on virtual send; includes the measured microphone pre-render path and local monitor estimate");
        text.AppendLine($"Media sync calibration: {MediaSyncOffsetMilliseconds:+0;-0;0} ms (negative advances Media; positive delays Media)");
        text.AppendLine($"Reported total latency: {EstimatedTotalLatencyMilliseconds:0.0} ms");
        text.AppendLine($"Dropouts: U {_statistics.UnderrunCount} · O {_statistics.OverrunCount} · D {_statistics.DiscontinuityCount}");
        text.AppendLine($"Active Sound Pads: {_statistics.ActiveSoundCount}");
        text.AppendLine($"Media: {(_mediaDeck.IsPlaying ? "Playing" : "Stopped")} · buffer {_statistics.MediaBufferFillFrames}/{_statistics.MediaBufferCapacityFrames} · underruns {_statistics.MediaUnderrunCount}");
        text.AppendLine($"Media monitor: {MediaMonitorEnabled} · send: {MediaSendEnabled} · device: {SelectedMonitorOutput?.Name ?? "not selected"}");
        text.AppendLine($"Profile: {_activeProfile.Name} · user presets {UserPresets.Count}");
        text.AppendLine($"Hotkeys: {HotkeyStatus.Replace(Environment.NewLine, " | ")}");
        text.AppendLine($"Remote: {(RemoteServerRunning ? "Running" : "Off")} · paired {RemoteClients.Count} · address {RemoteAddress}");
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

    public double MediaAlignmentMilliseconds => _statistics.SampleRate == 0U
        ? 0.0
        : _statistics.MediaAlignmentFrames * 1000.0 / _statistics.SampleRate;

    public double EstimatedTotalLatencyMilliseconds => _statistics.SampleRate == 0U
        ? 0.0
        : (_statistics.CaptureBufferFrames + _statistics.PitchLatencySamples +
            _statistics.RingBufferFillFrames + _statistics.RenderBufferFrames) * 1000.0 / _statistics.SampleRate;

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
        _remoteStatePublisher.Invalidate();
        if (e.PropertyName is nameof(SoundPadModel.Title) or nameof(SoundPadModel.FilePath) or
            nameof(SoundPadModel.Volume) or nameof(SoundPadModel.Loop) or nameof(SoundPadModel.RestartOnPress) or
            nameof(SoundPadModel.Hotkey))
        {
            ScheduleSave();
            if (e.PropertyName == nameof(SoundPadModel.Hotkey)) RefreshHotkeys();
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
            SaveProfiles();
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
        ApplyProfileCommand.RaiseCanExecuteChanged();
        DeleteProfileCommand.RaiseCanExecuteChanged();
        RaisePresetCommandStates();
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
            : $"{(20.0 * Math.Log10(linear)).ToString("0.0", CultureInfo.InvariantCulture)} dBFS";

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _automaticRecoveryArmed = false;
        _meterTimer.Stop();
        _saveTimer.Stop();
        _presetTransition?.Cancel();
        try
        {
            _store.Save(Pads);
            SaveProfiles();
        }
        catch (IOException)
        {
            // The application is closing; a prior successful save remains intact.
        }
        StopAllSounds();
        _remoteServer.Changed -= OnRemoteServerChanged;
        PropertyChanged -= OnRemoteObservableChanged;
        Pads.CollectionChanged -= OnRemoteCollectionChanged;
        try { _remoteServer.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch (Exception) { }
        _mediaDeck.Dispose();
        _hotkeys.Dispose();
        _engine.Dispose();
    }
}
