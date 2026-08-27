using GrassiBoard.Shared;
using GrassiBoard;
using GrassiBoard.Infrastructure;
using GrassiBoard.Models;
using GrassiBoard.Services;
using GrassiBoard.Services.Remote;
using GrassiBoard.ViewModels;
using System.Windows;
using System.Windows.Threading;
using System.Text.Json;
using System.Xml.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;

if (args is ["--diagnose-add-pad", string audioPath])
{
    string diagnosticRoot = Path.Combine(Path.GetTempPath(), $"GrassiBoard-pad-{Guid.NewGuid():N}");
    Directory.CreateDirectory(diagnosticRoot);
    using var viewModel = new MainViewModel(new SoundboardStore(Path.Combine(diagnosticRoot, "soundboard.json")));
    await viewModel.InitializeAsync();
    int existingPadCount = viewModel.Pads.Count;
    await viewModel.AddFilesAsync([audioPath]);
    SoundPadModel pad = viewModel.Pads.Skip(existingPadCount).Single();
    Console.WriteLine($"Pad diagnostic: loaded={pad.IsLoaded}; error={pad.Error ?? "none"}");
    bool loaded = pad.IsLoaded;
    viewModel.Dispose();
    Directory.Delete(diagnosticRoot, true);
    return loaded ? 0 : 20;
}

if (args is ["--diagnose-add-pad-ui", string uiAudioPath])
{
    string diagnosticRoot = Path.Combine(Path.GetTempPath(), $"GrassiBoard-pad-ui-{Guid.NewGuid():N}");
    Directory.CreateDirectory(diagnosticRoot);
    Exception? uiFailure = null;
    bool loaded = false;
    bool themeChanged = false;
    var thread = new Thread(() =>
    {
        try
        {
            var application = new Application();
            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/GrassiBoard;component/Themes/Tokens.xaml", UriKind.Relative)
            });
            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/GrassiBoard;component/Themes/Controls.xaml", UriKind.Relative)
            });
            application.Resources["BooleanToVisibilityConverter"] = new System.Windows.Controls.BooleanToVisibilityConverter();
            application.Resources["InverseBooleanToVisibilityConverter"] = new InverseBooleanToVisibilityConverter();
            application.DispatcherUnhandledException += (_, eventArgs) =>
            {
                uiFailure = eventArgs.Exception;
                eventArgs.Handled = true;
                application.Shutdown();
            };
            var viewModel = new MainViewModel(new SoundboardStore(Path.Combine(diagnosticRoot, "soundboard.json")));
            var window = new MainWindow(viewModel);
            window.Show();
            var darkColor = ((System.Windows.Media.SolidColorBrush)window.Background).Color;
            ThemeManager.Apply(isDark: false, persist: false);
            var lightColor = ((System.Windows.Media.SolidColorBrush)window.Background).Color;
            themeChanged = darkColor != lightColor;
            ThemeManager.Apply(isDark: true, persist: false);
            window.Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    await Task.Delay(1_500);
                    var activeViewModel = (MainViewModel)window.DataContext;
                    int existingPadCount = activeViewModel.Pads.Count;
                    await activeViewModel.AddFilesAsync([uiAudioPath]);
                    window.UpdateLayout();
                    loaded = activeViewModel.Pads.Skip(existingPadCount).Single().IsLoaded;
                }
                catch (Exception exception)
                {
                    uiFailure = exception;
                }
                finally
                {
                    window.Close();
                    application.Shutdown();
                }
            }, DispatcherPriority.ApplicationIdle);
            application.Run();
        }
        catch (Exception exception)
        {
            uiFailure = exception;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    Directory.Delete(diagnosticRoot, true);
    Console.WriteLine(uiFailure?.ToString() ?? $"Pad UI diagnostic: loaded={loaded}; themeChanged={themeChanged}");
    return uiFailure is null && loaded && themeChanged ? 0 : 21;
}

if (BuildInfo.CurrentVersion != "1.2.0" || NativeAudioEngine.ExpectedApiVersion != 11U)
{
    Console.Error.WriteLine(
        $"Managed version/native ABI contract is inconsistent. " +
        $"Expected version=1.2.0 development baseline, ABI=11; actual version={BuildInfo.CurrentVersion}, ABI={NativeAudioEngine.ExpectedApiVersion}.");
    return 1;
}

string fixture = Path.Combine(AppContext.BaseDirectory, "BuildInfo.fixture.json");
File.WriteAllText(fixture, """
    {
      "Version": "1.2.0",
      "CommitSha": "0123456789abcdef",
      "TargetArchitecture": "x64"
    }
    """);

BuildInfo info = BuildInfo.Load(fixture);
File.Delete(fixture);

if (info.Version != "1.2.0" || info.ShortCommit != "01234567" || info.TargetArchitecture != "x64")
{
    Console.Error.WriteLine("BuildInfo contract smoke test failed.");
    return 2;
}

var captures = new[]
{
    new AudioEndpointDescriptor("vb-capture", "CABLE Output (VB-Audio Virtual Cable)", "{VB}", false),
    new AudioEndpointDescriptor("amm-capture", "Microphone (AMM Virtual Audio Device)", string.Empty, false),
    new AudioEndpointDescriptor("physical-capture", "Headset Microphone (Microsoft LifeChat LX-3000)", "{LIFECHAT}", true)
};

var vbRender = new AudioEndpointDescriptor(
    "vb-render", "CABLE Input (VB-Audio Virtual Cable)", "{VB}", false);
var ammRender = new AudioEndpointDescriptor(
    "amm-render", "Speakers (AMM Virtual Audio Device)", string.Empty, false);
var physicalRender = new AudioEndpointDescriptor(
    "physical-render", "Headset Earphone (Microsoft LifeChat LX-3000)", "{LIFECHAT}", true);
var retiredRender = new AudioEndpointDescriptor(
    "retired-render", "Speakers (GrassiBoard Virtual Audio)", "{GRASSIBOARD}", false);

if (VirtualCableMatcher.FindPairedCaptureEndpoint(vbRender, captures)?.Id != "vb-capture" ||
    VirtualCableMatcher.FindPairedCaptureEndpoint(ammRender, captures)?.Id != "amm-capture" ||
    VirtualCableMatcher.FindPairedCaptureEndpoint(physicalRender, captures) is not null ||
    VirtualCableMatcher.FindPairedCaptureEndpoint(retiredRender, captures) is not null)
{
    Console.Error.WriteLine("External virtual-cable matching contract failed.");
    return 3;
}

string temporaryRoot = Path.Combine(Path.GetTempPath(), $"GrassiBoard-tests-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryRoot);
try
{
    string wavePath = Path.Combine(temporaryRoot, "pad.wav");
    WriteMonoPcmWave(wavePath);
    DecodedAudio decoded = AudioFileDecoder.Decode(wavePath);
    if (decoded.FrameCount != 480U || decoded.Samples.Length != 960 ||
        Math.Abs(decoded.Samples[0] - decoded.Samples[1]) > 0.0001F)
    {
        Console.Error.WriteLine("Offline Sound Pad decode contract failed.");
        return 4;
    }

    string storePath = Path.Combine(temporaryRoot, "soundboard.json");
    var store = new SoundboardStore(storePath);
    var pad = new SoundPadModel
    {
        Title = "Test pad",
        FilePath = wavePath,
        Volume = 0.75,
        Loop = true
    };
    store.Save([pad]);
    SoundPadModel restored = store.Load().Single();
    if (restored.Id != pad.Id || restored.Title != pad.Title ||
        Math.Abs(restored.Volume - 0.75) > 0.0001 || !restored.Loop)
    {
        Console.Error.WriteLine("Sound Pad persistence contract failed.");
        return 5;
    }

    string profilePath = Path.Combine(temporaryRoot, "profiles.json");
    Guid profileId = Guid.NewGuid();
    Guid validPresetId = Guid.NewGuid();
    File.WriteAllText(profilePath, $$"""
        {
          "SchemaVersion": 1,
          "ActiveProfileId": "{{profileId}}",
          "Profiles": [{
            "Id": "{{profileId}}",
            "Name": "Streaming",
            "Pads": [],
            "UserPresets": [
              { "Id": "{{validPresetId}}", "Name": "Radio", "Hotkey": "Ctrl+Alt+R", "State": {} },
              { "Id": "not-a-guid", "Name": "Broken", "State": {} }
            ],
            "Preferences": {}
          }]
        }
        """);
    ProfileDocument profiles = new ProfileStore(profilePath).Load();
    if (profiles.Profiles.Count != 1 || profiles.Profiles[0].UserPresets.Count != 1 ||
        profiles.Profiles[0].UserPresets[0].Name != "Radio")
    {
        Console.Error.WriteLine("Profile/preset fault-isolation contract failed.");
        return 13;
    }

    profiles.Profiles[0].Pads.Add(new SoundPadModel { Hotkey = "Ctrl+Shift+1" });
    profiles.Profiles[0].Preferences.ShowHideHotkey = "Ctrl+Alt+G";
    profiles.Profiles[0].Preferences.MediaSyncOffsetMilliseconds = -7.0;
    ProfileModel clonedProfile = profiles.Profiles[0].Clone();
    if (clonedProfile.Pads[0].Hotkey != "Ctrl+Shift+1" ||
        clonedProfile.Preferences.ShowHideHotkey != "Ctrl+Alt+G" ||
        clonedProfile.Preferences.MediaSyncOffsetMilliseconds != -7.0 ||
        !GlobalHotkeyService.TryParse("Ctrl+Alt+F9", out _, out _) ||
        GlobalHotkeyService.TryParse("Ctrl+Alt", out _, out _))
    {
        Console.Error.WriteLine("Profile clone or hotkey parsing contract failed.");
        return 14;
    }

    new ProfileStore(profilePath).Save(profiles);
    if (new ProfileStore(profilePath).Load().Profiles.Single().Preferences.MediaSyncOffsetMilliseconds != -7.0)
    {
        Console.Error.WriteLine("Media sync calibration persistence contract failed.");
        return 23;
    }

    uint baseSyncFrames = MediaDeckService.CalculateMonitorPathLatencyFrames(true, 0.0);
    uint advancedSyncFrames = MediaDeckService.CalculateMonitorPathLatencyFrames(true, -10.0);
    uint delayedSyncFrames = MediaDeckService.CalculateMonitorPathLatencyFrames(true, 10.0);
    if (baseSyncFrames - advancedSyncFrames != 480U ||
        delayedSyncFrames - baseSyncFrames != 480U ||
        MediaDeckService.CalculateMonitorPathLatencyFrames(false, 10.0) != 0U)
    {
        Console.Error.WriteLine("Live Media sync calibration direction contract failed.");
        return 24;
    }

    using (var mediaNative = new NativeAudioEngine())
    using (var mediaDeck = new MediaDeckService(mediaNative, () => false))
    {
        await mediaDeck.LoadAsync("invalid\0media.mp3");
        if (string.IsNullOrWhiteSpace(mediaDeck.Error) || mediaDeck.IsPlaying)
        {
            Console.Error.WriteLine("Media missing/invalid path safety contract failed.");
            return 16;
        }
    }

    if (new AudioDevice { Id = "monitor-id", Name = "Headset Earphone" }.ToString() != "Headset Earphone")
    {
        Console.Error.WriteLine("Audio device display-name contract failed.");
        return 17;
    }

    if (new ProfileModel { Name = "Studio" }.ToString() != "Studio" ||
        new UserPresetModel { Name = "Radio voice" }.ToString() != "Radio voice")
    {
        Console.Error.WriteLine("Profile or user-preset display-name contract failed.");
        return 21;
    }

    AudioDevice failedInput = new() { Id = "failed", Name = "USB Microphone", IsDefault = true };
    AudioDevice fallbackInput = new() { Id = "fallback", Name = "Webcam Microphone" };
    AudioDevice cableInput = new() { Id = "cable", Name = "CABLE Output (VB-Audio Virtual Cable)" };
    AudioDevice? recoveryInput = DeviceRecoveryPolicy.SelectNextInput(
        [failedInput, cableInput, fallbackInput],
        failedInput.Id);
    if (recoveryInput?.Id != fallbackInput.Id ||
        DeviceRecoveryPolicy.SelectNextInput([cableInput], failedInput.Id) is not null)
    {
        Console.Error.WriteLine("Automatic microphone recovery selection contract failed.");
        return 22;
    }

    string crashPath = CrashReporter.Report(
        new InvalidOperationException("diagnostic marker"),
        "Managed smoke test",
        fatal: true,
        directoryOverride: temporaryRoot);
    string crashText = File.ReadAllText(crashPath);
    if (!crashText.Contains("diagnostic marker", StringComparison.Ordinal) ||
        !crashText.Contains("Managed smoke test", StringComparison.Ordinal) ||
        !File.Exists(Path.Combine(temporaryRoot, "latest.txt")))
    {
        Console.Error.WriteLine("Crash report contract failed.");
        return 8;
    }
}
finally
{
    Directory.Delete(temporaryRoot, true);
}

string[] meterViews =
[
    Path.Combine(Environment.CurrentDirectory, "src", "GrassiBoard.App", "MainWindow.xaml"),
    Path.Combine(Environment.CurrentDirectory, "src", "GrassiBoard.App", "Views", "BoardView.xaml")
];
foreach (string meterView in meterViews)
{
    XDocument document = XDocument.Load(meterView);
    XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    foreach (XElement progressBar in document.Descendants(presentation + "ProgressBar"))
    {
        string binding = progressBar.Attribute("Value")?.Value ?? string.Empty;
        if (binding.Contains("Meter", StringComparison.Ordinal) &&
            !binding.Contains("Mode=OneWay", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Meter binding must be OneWay: {meterView} · {binding}");
            return 6;
        }
    }
}

string appSource = Path.Combine(Environment.CurrentDirectory, "src", "GrassiBoard.App");
var staticResourceKeys = new HashSet<string>(StringComparer.Ordinal);
XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
foreach (string xamlPath in Directory.EnumerateFiles(appSource, "*.xaml", SearchOption.AllDirectories))
{
    XDocument resourceDocument = XDocument.Load(xamlPath);
    foreach (XAttribute key in resourceDocument.Descendants().Attributes(xamlNamespace + "Key"))
    {
        staticResourceKeys.Add(key.Value);
    }
}
foreach (string xamlPath in Directory.EnumerateFiles(appSource, "*.xaml", SearchOption.AllDirectories))
{
    string xaml = File.ReadAllText(xamlPath);
    foreach (System.Text.RegularExpressions.Match match in
        System.Text.RegularExpressions.Regex.Matches(xaml, @"\{StaticResource\s+([A-Za-z_][A-Za-z0-9_.-]*)\}"))
    {
        string key = match.Groups[1].Value;
        if (!staticResourceKeys.Contains(key))
        {
            Console.Error.WriteLine($"Unknown WPF StaticResource: {xamlPath} · {key}");
            return 18;
        }
    }
}

foreach (string xamlPath in Directory.EnumerateFiles(appSource, "*.xaml", SearchOption.AllDirectories))
{
    XDocument document = XDocument.Load(xamlPath);
    foreach (XAttribute attribute in document.Descendants().Attributes()
        .Where(attribute => attribute.Name.LocalName is "Margin" or "Padding"))
    {
        string value = attribute.Value;
        if (value.StartsWith('{'))
        {
            continue;
        }
        int componentCount = value.Split(',', StringSplitOptions.TrimEntries).Length;
        if (componentCount is not (1 or 2 or 4))
        {
            Console.Error.WriteLine(
                $"Invalid WPF {attribute.Name.LocalName} component count: {xamlPath} · {value}");
            return 7;
        }
    }
}

string boardXaml = File.ReadAllText(Path.Combine(appSource, "Views", "BoardView.xaml"));
string controlsXaml = File.ReadAllText(Path.Combine(appSource, "Themes", "Controls.xaml"));
string mixerXaml = File.ReadAllText(Path.Combine(appSource, "Views", "MixerView.xaml"));
string mainWindowXaml = File.ReadAllText(Path.Combine(appSource, "MainWindow.xaml"));
string mainWindowCode = File.ReadAllText(Path.Combine(appSource, "MainWindow.xaml.cs"));
string mainViewModelCode = File.ReadAllText(Path.Combine(appSource, "ViewModels", "MainViewModel.cs"));
string appProject = File.ReadAllText(Path.Combine(appSource, "GrassiBoard.App.csproj"));
string trayServiceCode = File.ReadAllText(Path.Combine(appSource, "Services", "TrayService.cs"));
string iconPath = Path.Combine(appSource, "Assets", "GrassiBoard.ico");
string[] requiredUiContracts =
[
    "GbSoundPadCardStyle",
    "AutomationProperties.Name=\"Play sound\"",
    "AutomationProperties.Name=\"Stop sound\"",
    "IsEnabled=\"{Binding IsPlaying}\"",
    "AutomationProperties.Name=\"Remove sound\""
];
if (requiredUiContracts.Any(contract => !boardXaml.Contains(contract, StringComparison.Ordinal)) ||
    boardXaml.Contains("StateLabel", StringComparison.Ordinal) ||
    !controlsXaml.Contains("<Style TargetType=\"ToolTip\">", StringComparison.Ordinal) ||
    !controlsXaml.Contains("<Style TargetType=\"Slider\"", StringComparison.Ordinal) ||
    !controlsXaml.Contains("<Style TargetType=\"CheckBox\"", StringComparison.Ordinal) ||
    !controlsXaml.Contains("x:Name=\"PART_Track\"", StringComparison.Ordinal) ||
    !controlsXaml.Contains("x:Name=\"PART_Indicator\"", StringComparison.Ordinal) ||
    !mixerXaml.Contains("MicGain", StringComparison.Ordinal) ||
    !mixerXaml.Contains("DuckingEnabled", StringComparison.Ordinal) ||
    !mixerXaml.Contains("PitchWetMix", StringComparison.Ordinal) ||
    !mainWindowXaml.Contains("IsMixerPage", StringComparison.Ordinal) ||
    !mainWindowXaml.Contains("GlassFrameThickness=\"1\"", StringComparison.Ordinal) ||
    !mainWindowXaml.Contains("Icon=\"Assets/GrassiBoard.ico\"", StringComparison.Ordinal) ||
    !mainWindowXaml.Contains("Source=\"Assets/GrassiBoard.png\"", StringComparison.Ordinal) ||
    !appProject.Contains("<ApplicationIcon>Assets\\GrassiBoard.ico</ApplicationIcon>", StringComparison.Ordinal) ||
    !trayServiceCode.Contains("Icon.ExtractAssociatedIcon", StringComparison.Ordinal) ||
    !File.Exists(iconPath) || new FileInfo(iconPath).Length < 10_000L ||
    !mainWindowCode.Contains("WmGetMinMaxInfo", StringComparison.Ordinal) ||
    !mainWindowCode.Contains("MonitorFromWindow", StringComparison.Ordinal) ||
    !mainWindowCode.Contains("WorkArea", StringComparison.Ordinal) ||
    !mainViewModelCode.Contains("StopAllAsync", StringComparison.Ordinal) ||
    !mainViewModelCode.Contains("await Task.Run(_engine.Stop)", StringComparison.Ordinal))
{
    Console.Error.WriteLine("UI refresh contract failed.");
    return 9;
}

string remoteServerSource = File.ReadAllText(Path.Combine(appSource, "Services", "Remote", "RemoteServerService.cs"));
string remoteCommandDispatcherSource = File.ReadAllText(Path.Combine(appSource, "Services", "Remote", "RemoteCommandDispatcher.cs"));
string remoteProtocolSource = File.ReadAllText(Path.Combine(appSource, "Services", "Remote", "RemoteProtocol.cs"));
string remoteSettingsSource = File.ReadAllText(Path.Combine(appSource, "Services", "Remote", "RemoteSettingsStore.cs"));
string remoteMdnsSource = File.ReadAllText(Path.Combine(appSource, "Services", "Remote", "RemoteMdnsService.cs"));
string remoteMonitorSpikeSource = File.ReadAllText(Path.Combine(appSource, "Services", "Remote", "RemoteMonitorWebRtcSpikeService.cs"));
string remotePhoneMicSpikeSource = File.ReadAllText(Path.Combine(appSource, "Services", "Remote", "RemotePhoneMicWebRtcSpikeService.cs"));
string remotePhoneMicBridgeSource = File.ReadAllText(Path.Combine(appSource, "Services", "Remote", "RemotePhoneMicPcmBridge.cs"));
string nativeAudioEngineSource = File.ReadAllText(Path.Combine(appSource, "Services", "NativeAudioEngine.cs"));
string mediaDeckServiceSource = File.ReadAllText(Path.Combine(appSource, "Services", "MediaDeckService.cs"));
string nativeHeaderSource = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "src", "GrassiBoard.AudioEngine", "include", "grassiboard", "audio_engine.h"));
string nativeEngineSource = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "src", "GrassiBoard.AudioEngine", "src", "audio_engine.cpp"));
string nativeWasapiSource = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "src", "GrassiBoard.AudioEngine", "src", "wasapi_engine.cpp"));
string nativeMonitorTapSource = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "src", "GrassiBoard.AudioEngine", "src", "monitor_tap_buffer.cpp"));
string cmakePresetsSource = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "CMakePresets.json"));
string localBuildScriptSource = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "tools", "Build-LocalRemoteTest.ps1"));
string remoteWebRoot = Path.Combine(Environment.CurrentDirectory, "src", "GrassiBoard.RemoteWeb");
string remoteWebConnectionSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "composables", "useRemoteConnection.ts"));
string remoteMonitorWebSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "composables", "useRemoteMonitorSpike.ts"));
string remotePhoneMicWebSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "composables", "useRemotePhoneMicSpike.ts"));
string remotePhoneMicPageSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "pages", "remote-mic.vue"));
string remoteMonitorPageSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "pages", "monitor.vue"));
string remoteMonitorAudioHostSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "components", "RemoteMonitorAudioHost.vue"));
string remoteQrScannerSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "components", "QrScannerModal.vue"));
string remotePwaPluginSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "plugins", "pwa.client.ts"));
string remoteMaterialPluginSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "plugins", "material.client.ts"));
string remoteSnackbarSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "components", "ui", "GbSnackbar.vue"));
string remoteFabSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "components", "ui", "GbFab.vue"));
string remoteLayoutSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "layouts", "default.vue"));
string remoteIconsPluginSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "plugins", "icons.client.ts"));
string remoteIconSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "components", "ui", "GbIcon.vue"));
string remoteCssSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "assets", "main.css"));
string remotePackageSource = File.ReadAllText(Path.Combine(remoteWebRoot, "package.json"));
string remoteNuxtConfigSource = File.ReadAllText(Path.Combine(remoteWebRoot, "nuxt.config.ts"));
string remoteManifestSource = File.ReadAllText(Path.Combine(remoteWebRoot, "public", "manifest.webmanifest"));
string remoteServiceWorkerSource = File.ReadAllText(Path.Combine(remoteWebRoot, "public", "sw.js"));
string installerServiceSource = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "src", "GrassiBoard.Installer", "InstallationService.cs"));
string installerWindowXaml = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "src", "GrassiBoard.Installer", "MainWindow.xaml"));
if (remoteServerSource.Contains("NativeAudioEngine", StringComparison.Ordinal) ||
    remoteServerSource.Contains("DllImport", StringComparison.Ordinal) ||
    remoteProtocolSource.Contains("FilePath", StringComparison.Ordinal) ||
    !appProject.Contains("Microsoft.AspNetCore.App", StringComparison.Ordinal) ||
    !appProject.Contains("QRCoder", StringComparison.Ordinal) ||
    !appProject.Contains("GrassiBoard.RemoteWeb", StringComparison.Ordinal) ||
    !remoteWebConnectionSource.Contains("createMessageId", StringComparison.Ordinal) ||
    !remoteWebConnectionSource.Contains("getRandomValues", StringComparison.Ordinal) ||
    !remoteWebConnectionSource.Contains("pairFromQr", StringComparison.Ordinal) ||
    !remoteServerSource.Contains("UseHttps", StringComparison.Ordinal) ||
    !remoteServerSource.Contains("/api/remote/ca.cer", StringComparison.Ordinal) ||
    !remoteServerSource.Contains("RemoteMdnsService", StringComparison.Ordinal) ||
    !remoteMdnsSource.Contains("BuildLegacyUnicastAnswer", StringComparison.Ordinal) ||
    !remoteMdnsSource.Contains("packet.RemoteEndPoint.Port != MdnsPort", StringComparison.Ordinal) ||
    !remoteMdnsSource.Contains("query.QuestionCount", StringComparison.Ordinal) ||
    !remoteServerSource.Contains("if (!context.Request.IsHttps)", StringComparison.Ordinal) ||
    !remoteServerSource.Contains("Status426UpgradeRequired", StringComparison.Ordinal) ||
    !remoteSettingsSource.Contains("SecurePort { get; set; } = 47919", StringComparison.Ordinal) ||
    !remoteQrScannerSource.Contains("getUserMedia", StringComparison.Ordinal) ||
    !remoteQrScannerSource.Contains("BarcodeDetector", StringComparison.Ordinal) ||
    !remotePwaPluginSource.Contains("serviceWorker.register('/sw.js'", StringComparison.Ordinal) ||
    !remotePwaPluginSource.Contains("updateViaCache: 'none'", StringComparison.Ordinal) ||
    !remoteMaterialPluginSource.Contains("export default defineNuxtPlugin", StringComparison.Ordinal) ||
    !remoteMaterialPluginSource.Contains("@material/web/fab/fab.js", StringComparison.Ordinal) ||
    !remoteNuxtConfigSource.Contains("pathPrefix: false", StringComparison.Ordinal) ||
    !remoteSnackbarSource.Contains("gb-snackbar", StringComparison.Ordinal) ||
    !remoteFabSource.Contains("<md-fab", StringComparison.Ordinal) ||
    !remoteLayoutSource.Contains("floating-session-actions", StringComparison.Ordinal) ||
    !remoteLayoutSource.Contains("engine.stop", StringComparison.Ordinal) ||
    !remoteWebConnectionSource.Contains("showSnackbar", StringComparison.Ordinal) ||
    !remoteWebConnectionSource.Contains("engine_not_running' ? 'warning'", StringComparison.Ordinal) ||
    !remoteIconSource.Contains("material-symbols-rounded", StringComparison.Ordinal) ||
    !remoteIconSource.Contains("fontVariationSettings", StringComparison.Ordinal) ||
    !remoteIconsPluginSource.Contains("@fontsource-variable/material-symbols-rounded/full.css", StringComparison.Ordinal) ||
    !remotePackageSource.Contains("@fontsource-variable/material-symbols-rounded", StringComparison.Ordinal) ||
    !remoteCssSource.Contains("user-select: none !important", StringComparison.Ordinal) ||
    !remoteCommandDispatcherSource.Contains("engine_not_running", StringComparison.Ordinal) ||
    !appProject.Contains("EnableRemoteMonitorSpike", StringComparison.Ordinal) ||
    !appProject.Contains("<PackageReference Include=\"SIPSorcery\" Version=\"10.0.13\"", StringComparison.Ordinal) ||
    !appProject.Contains("<PackageReference Include=\"Concentus\" Version=\"2.2.2\"", StringComparison.Ordinal) ||
    appProject.Contains("<PackageReference Include=\"SpawnDev.SIPSorcery\"", StringComparison.Ordinal) ||
    !appProject.Contains("REMOTE_MONITOR_SPIKE", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("#if REMOTE_MONITOR_SPIKE", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("AudioSourcesEnum.SineWave", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("WasapiLoopbackCapture", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("WatchDefaultDeviceAsync", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("capture switched automatically", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("FrameMilliseconds = 20", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("AudioFormat negotiatedFormat = _negotiatedFormat.Value", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("_encoder.EncodeAudio(pcm, negotiatedFormat)", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("OpusCodecFactory.CreateEncoder(sampleRate, channels)", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("LoopbackOpusBitrate = 128000", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("encoder.UseVBR = true", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("encoder.UseConstrainedVBR = false", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("encoder.UseDTX = false", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("encoder.Complexity = 10", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("AudioCodecsEnum.OPUS", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("new AudioEncoder(includeOpus: true)", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("soundboard-tap", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("monitor-mix", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("PumpMonitorMixAsync", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("_mixWindowsGain = 0.90F", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("_mixSoundboardGain = 0.70F", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("_mixMediaGain = 0.70F", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("_mixVoiceGain = 0.10F", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("_mixVoiceEnabled", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("ReadVoiceMonitorTap", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("voiceEnabled", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("_mixMasterGain = 0.85F", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("MixLoopbackPrebufferFrames = 2", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("pendingSoundboardFrames", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("buffer.BufferedBytes >= frameBytes", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("MixLimiterCeiling = 0.98F", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("MixMediaPrebufferFrames = 2", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("IsMediaDuplicateSuppressedByWindowsLoopback", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("mediaDuplicateSuppressed", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("_mediaDeck.ClearRemoteMonitorTap()", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("desiredLimiterGain", StringComparison.Ordinal) ||
    !mediaDeckServiceSource.Contains("RemoteMonitorTapCapacityFrames = BlockFrames * 24", StringComparison.Ordinal) ||
    !mediaDeckServiceSource.Contains("RemoteMonitorTapBuffer", StringComparison.Ordinal) ||
    !mediaDeckServiceSource.Contains("SetRemoteMonitorTapEnabled", StringComparison.Ordinal) ||
    !mediaDeckServiceSource.Contains("TryReadRemoteMonitorTap", StringComparison.Ordinal) ||
    !mainViewModelCode.Contains("new RemoteMonitorWebRtcSpikeService(_engine, _mediaDeck)", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("SetMonitorTapEnabled(true)", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("ReadMonitorTap", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("PumpNativeSoundboardTapAsync", StringComparison.Ordinal) ||
    !nativeAudioEngineSource.Contains("ExpectedApiVersion = 11U", StringComparison.Ordinal) ||
    !nativeAudioEngineSource.Contains("gb_monitor_tap_read", StringComparison.Ordinal) ||
    !nativeAudioEngineSource.Contains("gb_voice_monitor_tap_read", StringComparison.Ordinal) ||
    !nativeHeaderSource.Contains("gb_set_input_source_mode", StringComparison.Ordinal) ||
    !nativeHeaderSource.Contains("gb_remote_input_push", StringComparison.Ordinal) ||
    !nativeHeaderSource.Contains("gb_get_remote_input_statistics", StringComparison.Ordinal) ||
    !nativeHeaderSource.Contains("gb_monitor_tap_set_enabled", StringComparison.Ordinal) ||
    !nativeHeaderSource.Contains("gb_voice_monitor_tap_set_enabled", StringComparison.Ordinal) ||
    !nativeHeaderSource.Contains("gb_monitor_tap_get_statistics", StringComparison.Ordinal) ||
    !nativeEngineSource.Contains("constexpr std::uint32_t kApiVersion = 11", StringComparison.Ordinal) ||
    !nativeWasapiSource.Contains("remote_input_.Read", StringComparison.Ordinal) ||
    !nativeWasapiSource.Contains("GB_INPUT_SOURCE_REMOTE", StringComparison.Ordinal) ||
    !nativeWasapiSource.Contains("input_source_mode_.store(GB_INPUT_SOURCE_WINDOWS", StringComparison.Ordinal) ||
    !nativeWasapiSource.Contains("active_input_source_mode_.load", StringComparison.Ordinal) ||
    !nativeWasapiSource.Contains("active_input_source_mode_.store", StringComparison.Ordinal) ||
    !nativeWasapiSource.Contains("switching runs on the realtime worker and must stay allocation-free", StringComparison.Ordinal) ||
    !nativeWasapiSource.Contains("monitor_tap_.Push", StringComparison.Ordinal) ||
    !nativeWasapiSource.Contains("voice_monitor_tap_.Push", StringComparison.Ordinal) ||
    !nativeWasapiSource.Contains("post Pitch/Formant", StringComparison.Ordinal) ||
    !nativeWasapiSource.Contains("pre Program", StringComparison.Ordinal) ||
    !nativeMonitorTapSource.Contains("std::memory_order_release", StringComparison.Ordinal) ||
    !cmakePresetsSource.Contains("windows-x64-remote-monitor-spike", StringComparison.Ordinal) ||
    !cmakePresetsSource.Contains("GRASSIBOARD_REMOTE_MONITOR_TAP", StringComparison.Ordinal) ||
    !localBuildScriptSource.Contains("windows-x64-remote-monitor-spike", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("new RTCPeerConnection()", StringComparison.Ordinal) ||
    !remoteServerSource.Contains("mic.spike.offer", StringComparison.Ordinal) ||
    !remoteServerSource.Contains("mic.spike.ice", StringComparison.Ordinal) ||
    !remoteServerSource.Contains("mic.spike.stop", StringComparison.Ordinal) ||
    !remoteServerSource.Contains("remotePhoneMicSpikeAvailable", StringComparison.Ordinal) ||
    !remoteServerSource.Contains("RebindClientAsync(client.ClientId", StringComparison.Ordinal) ||
    !remotePhoneMicSpikeSource.Contains("MediaStreamStatusEnum.RecvOnly", StringComparison.Ordinal) ||
    !remotePhoneMicSpikeSource.Contains("OnRtpPacketReceived", StringComparison.Ordinal) ||
    !remotePhoneMicSpikeSource.Contains("DecodeAudio", StringComparison.Ordinal) ||
    !remotePhoneMicSpikeSource.Contains("new AudioEncoder(includeOpus: true)", StringComparison.Ordinal) ||
    !remotePhoneMicSpikeSource.Contains("HandleRouteAsync", StringComparison.Ordinal) ||
    !remotePhoneMicSpikeSource.Contains("OpusDecodedPcmChannels = 1", StringComparison.Ordinal) ||
    !remotePhoneMicSpikeSource.Contains("decoded PCM is mono", StringComparison.Ordinal) ||
    !remotePhoneMicSpikeSource.Contains("_bridge.PushDecoded(pcm, decodedPcmChannels, sampleRate)", StringComparison.Ordinal) ||
    !remotePhoneMicSpikeSource.Contains("channels = format is null ? 0 : OpusDecodedPcmChannels", StringComparison.Ordinal) ||
    !remotePhoneMicSpikeSource.Contains("RemotePhoneMicPcmBridge", StringComparison.Ordinal) ||
    !remotePhoneMicSpikeSource.Contains("routeRequested = bridge.RouteRequested", StringComparison.Ordinal) ||
    !remotePhoneMicSpikeSource.Contains("routedToAudioEngine = bridge.Routed", StringComparison.Ordinal) ||
    !remotePhoneMicSpikeSource.Contains("nativeAbi = 11", StringComparison.Ordinal) ||
    !remotePhoneMicSpikeSource.Contains("jitterFillFrames", StringComparison.Ordinal) ||
    !remotePhoneMicBridgeSource.Contains("OutputSampleRate = 48_000", StringComparison.Ordinal) ||
    !remotePhoneMicBridgeSource.Contains("OutputBlockFrames = 480", StringComparison.Ordinal) ||
    !remotePhoneMicBridgeSource.Contains("TargetJitterFrames = 1_440", StringComparison.Ordinal) ||
    !remotePhoneMicBridgeSource.Contains("NativeTargetFillFrames = 1_440", StringComparison.Ordinal) ||
    !remotePhoneMicBridgeSource.Contains("ManagedReserveFrames = 960", StringComparison.Ordinal) ||
    !remotePhoneMicBridgeSource.Contains("StartupJitterFrames = TargetJitterFrames + NativePrebufferFrames", StringComparison.Ordinal) ||
    !remotePhoneMicBridgeSource.Contains("MaxNativeRefillBlocksPerTick = 6", StringComparison.Ordinal) ||
    !remotePhoneMicBridgeSource.Contains("while (refill.FillFrames < NativeTargetFillFrames", StringComparison.Ordinal) ||
    !remotePhoneMicBridgeSource.Contains("fill < ManagedReserveFrames + consumeFrames", StringComparison.Ordinal) ||
    !remotePhoneMicBridgeSource.Contains("NativePrebufferFrames = 1_440", StringComparison.Ordinal) ||
    !remotePhoneMicBridgeSource.Contains("statistics.Running == 0U", StringComparison.Ordinal) ||
    !remotePhoneMicBridgeSource.Contains("SetInputSourceMode(RemoteInputSourceMode.Remote)", StringComparison.Ordinal) ||
    !remotePhoneMicBridgeSource.Contains("RequestedSourceMode", StringComparison.Ordinal) ||
    !remotePhoneMicBridgeSource.Contains("ActiveSourceMode", StringComparison.Ordinal) ||
    !remotePhoneMicBridgeSource.Contains("NativePrebufferFrames", StringComparison.Ordinal) ||
    !remotePhoneMicBridgeSource.Contains("PushRemoteInput", StringComparison.Ordinal) ||
    !remoteServerSource.Contains("monitor.spike.offer", StringComparison.Ordinal) ||
    !remoteServerSource.Contains("monitor.spike.ice", StringComparison.Ordinal) ||
    !remoteServerSource.Contains("monitor.spike.mix.set", StringComparison.Ordinal) ||
    !remoteServerSource.Contains("HandleMixSettingsAsync", StringComparison.Ordinal) ||
    !remoteServerSource.Contains("monitor.spike.stop", StringComparison.Ordinal) ||
    !remoteServerSource.Contains("remoteMonitorSpikeAvailable", StringComparison.Ordinal) ||
    !remoteWebConnectionSource.Contains("subscribeMessage", StringComparison.Ordinal) ||
    !remotePhoneMicWebSource.Contains("scheduleRecovery", StringComparison.Ordinal) ||
    !remotePhoneMicWebSource.Contains("pageshow", StringComparison.Ordinal) ||
    !remotePhoneMicWebSource.Contains("iceConnectionState === 'disconnected'", StringComparison.Ordinal) ||
    !remotePhoneMicWebSource.Contains("mic.spike.route.set", StringComparison.Ordinal) ||
    !remotePhoneMicWebSource.Contains("getUserMedia", StringComparison.Ordinal) ||
    !remotePhoneMicWebSource.Contains("new RTCPeerConnection({ iceServers: [] })", StringComparison.Ordinal) ||
    !remotePhoneMicWebSource.Contains("addTrack(track, stream)", StringComparison.Ordinal) ||
    !remotePhoneMicWebSource.Contains("mic.spike.offer", StringComparison.Ordinal) ||
    !remotePhoneMicWebSource.Contains("echoCancellation: true", StringComparison.Ordinal) ||
    !remotePhoneMicWebSource.Contains("echoCancellation: false", StringComparison.Ordinal) ||
    !remotePhoneMicPageSource.Contains("Route Phone Mic", StringComparison.Ordinal) ||
    !remotePhoneMicPageSource.Contains("Enable Phone Mic", StringComparison.Ordinal) ||
    !remotePhoneMicPageSource.Contains("RTP packets", StringComparison.Ordinal) ||
    !remotePhoneMicPageSource.Contains("Decoded frames", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("new RTCPeerConnection({ iceServers: [] })", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("addTransceiver('audio', { direction: 'recvonly' })", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("monitor.spike.offer", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("monitor-mix", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("windows-loopback", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("soundboard-tap", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("monitor.spike.mix.set", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("mixWindowsGainPercent", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("mixMediaGainPercent", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("mixVoiceGainPercent", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("mixVoiceEnabled", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("voiceEnabled", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("mediaDuplicateSuppressed", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("waitForIceGatheringComplete", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("a=candidate:", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("Start monitor", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("monitor-quick-grid", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("Tap or drag horizontally", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("touch-action: pan-y", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("Voice Lv", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("role=\"slider\"", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("navigator.vibrate(7)", StringComparison.Ordinal) ||
    remoteMonitorPageSource.Contains("monitor-glance", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("Advanced diagnostics", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("Isolated source test", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("Windows / Space", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("label=\"Media\"", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("Via Windows", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("My Voice", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("My Voice level", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("Monitor Master", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("Phone-only mix", StringComparison.Ordinal) ||
    remoteMonitorPageSource.Contains("Gate 4C", StringComparison.Ordinal) ||
    remoteMonitorPageSource.Contains("Technology spike", StringComparison.OrdinalIgnoreCase) ||
    !remoteMonitorPageSource.Contains("monitor-fold", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("Headphones recommended for My Voice", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("Mute monitor audio", StringComparison.Ordinal) ||
    !remoteMonitorAudioHostSource.Contains("monitor-audio--hidden", StringComparison.Ordinal) ||
    !remoteMonitorAudioHostSource.Contains("navigator.mediaSession", StringComparison.Ordinal) ||
    !remoteMonitorAudioHostSource.Contains("visibilityState === 'visible'", StringComparison.Ordinal) ||
    !remoteMonitorAudioHostSource.Contains("resumeIfDesired", StringComparison.Ordinal) ||
    !remoteMonitorAudioHostSource.Contains("bind('stop'", StringComparison.Ordinal) ||
    remoteMonitorAudioHostSource.Contains("bind('stop', () => monitor.stop()", StringComparison.Ordinal) ||
    !remoteMonitorAudioHostSource.Contains("monitor.setMonitorMuted(true)", StringComparison.Ordinal) ||
    !remoteMonitorAudioHostSource.Contains("monitor.active.value ? 'playing' : 'none'", StringComparison.Ordinal) ||
    !remoteMonitorAudioHostSource.Contains("addEventListener('focus'", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("grassiboard.monitor.desired-source", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("peerAttached", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("localPeerIsUsable", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("scheduleAutoResume", StringComparison.Ordinal) ||
    !remoteMonitorWebSource.Contains("getStats()", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("Connection details", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("Opus target", StringComparison.Ordinal) ||
    !remoteLayoutSource.Contains("<RemoteMonitorAudioHost", StringComparison.Ordinal) ||
    remoteMonitorPageSource.Contains("onBeforeUnmount(() => monitor.dispose())", StringComparison.Ordinal) ||
    !remoteServerSource.Contains("RebindClientAsync(client.ClientId", StringComparison.Ordinal) ||
    remoteServerSource.Contains("Remote WebSocket disconnected", StringComparison.Ordinal) ||
    !remoteMonitorSpikeSource.Contains("RebindClientAsync", StringComparison.Ordinal) ||
    (!remoteMonitorSpikeSource.Contains("stable paired client id", StringComparison.Ordinal) &&
     !remoteServerSource.Contains("stable paired client id", StringComparison.Ordinal)) ||
    !remoteMonitorPageSource.Contains("Capture format", StringComparison.Ordinal) ||
    !remoteMonitorPageSource.Contains("Test tone", StringComparison.Ordinal) ||
    !remoteLayoutSource.Contains("remoteMonitorSpikeAvailable", StringComparison.Ordinal) ||
    !localBuildScriptSource.Contains("RemoteMonitorSpike", StringComparison.Ordinal) ||
    !localBuildScriptSource.Contains("GrassiBoard v1.3 Gate 2 development path is ENABLED", StringComparison.Ordinal) ||
    !localBuildScriptSource.Contains("windows-x64-release", StringComparison.Ordinal) ||
    !localBuildScriptSource.Contains("--no-incremental", StringComparison.Ordinal) ||
    !remoteManifestSource.Contains("\"name\": \"GrassiMote\"", StringComparison.Ordinal) ||
    !remoteManifestSource.Contains("\"display\": \"standalone\"", StringComparison.Ordinal) ||
    !remoteServiceWorkerSource.Contains("grassimote-shell-v22", StringComparison.Ordinal) ||
    !remoteServiceWorkerSource.Contains("caches.match('/offline.html')", StringComparison.Ordinal) ||
    !remoteServiceWorkerSource.Contains("url.pathname.startsWith('/api/')", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Remote isolation/publish source contract failed.");
    return 34;
}
if (!installerServiceSource.Contains("ProductVersion = \"1.2.0\"", StringComparison.Ordinal) ||
    !installerWindowXaml.Contains("Ready to install GrassiBoard 1.2.0", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Installer candidate version contract failed.");
    return 35;
}

MixerSettings defaultMixer = MixerSettings.CreateDefault();
if (System.Runtime.InteropServices.Marshal.SizeOf<MixerSettings>() != 60 ||
    defaultMixer.StructSize != 60U ||
    defaultMixer.LimiterEnabled != 1U ||
    defaultMixer.ClippingProtectionEnabled != 1U ||
    Math.Abs(defaultMixer.PitchWetMix - 1.0F) > 0.0001F)
{
    Console.Error.WriteLine("Managed mixer ABI layout failed.");
    return 11;
}

int statisticsSize = System.Runtime.InteropServices.Marshal.SizeOf<AudioStatistics>();
if (statisticsSize != 144)
{
    Console.Error.WriteLine($"Managed statistics ABI layout failed: {statisticsSize} bytes.");
    return 15;
}

int remoteInputStatisticsSize = System.Runtime.InteropServices.Marshal.SizeOf<RemoteInputStatistics>();
if (remoteInputStatisticsSize != 56)
{
    Console.Error.WriteLine($"Managed Remote Input ABI-11 statistics layout failed: {remoteInputStatisticsSize} bytes.");
    return 38;
}

if (MainViewModel.ToMeter(float.NegativeInfinity) != 0.0 ||
    MainViewModel.ToMeter(float.NaN) != 0.0 ||
    MainViewModel.ToMeter(0.0F) != 0.0 ||
    Math.Abs(MainViewModel.ToMeter(0.001F) - 0.0) > 0.001 ||
    Math.Abs(MainViewModel.ToMeter(1.0F) - 100.0) > 0.001 ||
    MainViewModel.ToMeter(0.1F) is < 66.0 or > 67.0)
{
    Console.Error.WriteLine("dBFS meter mapping contract failed.");
    return 10;
}

string remoteRoot = Path.Combine(Path.GetTempPath(), $"GrassiBoard-remote-{Guid.NewGuid():N}");
Directory.CreateDirectory(remoteRoot);
try
{
    string remoteSettingsPath = Path.Combine(remoteRoot, "remote-settings.json");
    var remoteStore = new RemoteSettingsStore(remoteSettingsPath);
    RemoteSettingsDocument remoteSettings = remoteStore.Load();
    DateTimeOffset remoteNow = new(2026, 8, 11, 9, 0, 0, TimeSpan.Zero);
    var pairing = new RemotePairingService(remoteStore, remoteSettings, () => remoteNow);
    RemotePairingInfo pairingInfo = pairing.CreatePairing("http://192.168.1.20:47918/");
    string pairSecret = Uri.UnescapeDataString(new Uri(pairingInfo.Url).Query.TrimStart('?').Split('=', 2)[1]);
    if (!pairing.TryPair(new RemotePairRequest(pairSecret, null, "Smoke Phone"), out RemotePairResponse? pairResponse) ||
        pairResponse is null || pairing.ValidateClientToken(pairResponse.ClientToken)?.Name != "Smoke Phone")
    {
        Console.Error.WriteLine("Remote pairing/token contract failed.");
        return 25;
    }
    if (!pairing.Revoke(Guid.Parse(pairResponse.ClientId)) || pairing.ValidateClientToken(pairResponse.ClientToken) is not null)
    {
        Console.Error.WriteLine("Remote token revoke contract failed.");
        return 26;
    }

    RemotePairingInfo expiring = pairing.CreatePairing("http://192.168.1.20:47918/");
    remoteNow = remoteNow.AddMinutes(6);
    if (pairing.IsPairingActive || pairing.TryPair(new RemotePairRequest(null, expiring.Code, "Late Phone"), out _))
    {
        Console.Error.WriteLine("Remote pairing expiration contract failed.");
        return 27;
    }

    using (var tls = new RemoteTlsService(Path.Combine(remoteRoot, "remote-tls")))
    {
        RemoteTlsMaterial material = tls.GetOrCreate(IPAddress.Parse("192.168.1.20"));
#pragma warning disable SYSLIB0057 // .NET 8 pinned SDK: DER loading constructor is intentionally used in this smoke test.
        using var rootCertificate = new X509Certificate2(material.RootCertificateDer);
#pragma warning restore SYSLIB0057
        string serverDnsName = material.ServerCertificate.GetNameInfo(X509NameType.DnsName, forIssuer: false);
        if (!material.ServerCertificate.HasPrivateKey ||
            rootCertificate.HasPrivateKey ||
            !string.Equals(serverDnsName, RemoteTlsService.StableHostName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(material.ServerCertificate.Issuer, rootCertificate.Subject, StringComparison.Ordinal))
        {
            Console.Error.WriteLine("GrassiMote local TLS certificate contract failed.");
            return 36;
        }
    }

    string envelopeJson = """{"protocolVersion":1,"type":"voice.pitch.set","messageId":"smoke","payload":{"value":2.5}}""";
    RemoteIncomingEnvelope? protocolEnvelope = JsonSerializer.Deserialize<RemoteIncomingEnvelope>(envelopeJson, RemoteProtocol.JsonOptions);
    if (protocolEnvelope is null || protocolEnvelope.ProtocolVersion != 1 || protocolEnvelope.Type != "voice.pitch.set" ||
        protocolEnvelope.Payload.GetProperty("value").GetDouble() != 2.5)
    {
        Console.Error.WriteLine("Remote protocol serialization contract failed.");
        return 28;
    }

    string soundboardPath = Path.Combine(remoteRoot, "soundboard.json");
    string profilePathForRemote = Path.Combine(remoteRoot, "profiles.json");
    using var remoteViewModel = new MainViewModel(
        new SoundboardStore(soundboardPath),
        new ProfileStore(profilePathForRemote),
        new RemoteSettingsStore(Path.Combine(remoteRoot, "vm-remote-settings.json")));
    var privatePad = new SoundPadModel
    {
        Title = "Private path pad",
        FilePath = Path.Combine(remoteRoot, "secret-audio.wav")
    };
    remoteViewModel.Pads.Add(privatePad);
    var remotePreset = new UserPresetModel
    {
        Name = "Remote Radio",
        State = new AudioStateSnapshot { Pitch = 4.0, Formant = -1.0 }
    };
    remoteViewModel.UserPresets.Add(remotePreset);
    var publisher = new RemoteStatePublisher();
    int invalidations = 0;
    publisher.Invalidated += _ => invalidations++;
    long revision = publisher.Invalidate();
    if (revision <= 1 || invalidations != 1)
    {
        Console.Error.WriteLine("Remote state revision contract failed.");
        return 29;
    }

    var dispatcher = new RemoteCommandDispatcher(remoteViewModel, Dispatcher.CurrentDispatcher);
    RemoteCommandResult stoppedEnginePad = await dispatcher.ExecuteAsync(new RemoteIncomingEnvelope
    {
        ProtocolVersion = RemoteProtocol.Version,
        Type = "pad.play",
        MessageId = Guid.NewGuid().ToString("N"),
        Payload = JsonSerializer.SerializeToElement(new { padId = privatePad.Id }, RemoteProtocol.JsonOptions)
    });
    if (stoppedEnginePad.Success || stoppedEnginePad.ErrorCode != "engine_not_running" || privatePad.HasError)
    {
        Console.Error.WriteLine("Remote stopped-engine Sound Pad regression contract failed.");
        return 37;
    }

    RemoteStateSnapshot snapshot = await dispatcher.CreateSnapshotAsync(revision);
    string snapshotJson = JsonSerializer.Serialize(snapshot, RemoteProtocol.JsonOptions);
    if (snapshot.Revision != revision || snapshot.Pads.Single().Title != "Private path pad" ||
        snapshot.Presets.Single().Name != "Remote Radio" ||
        snapshotJson.Contains(privatePad.FilePath, StringComparison.OrdinalIgnoreCase) ||
        snapshotJson.Contains("filePath", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("Remote authoritative snapshot/privacy contract failed.");
        return 30;
    }

    RemoteCommandResult invalidPitch = await dispatcher.ExecuteAsync(new RemoteIncomingEnvelope
    {
        ProtocolVersion = RemoteProtocol.Version,
        Type = "voice.pitch.set",
        MessageId = Guid.NewGuid().ToString("N"),
        Payload = JsonSerializer.SerializeToElement(new { value = 99.0 }, RemoteProtocol.JsonOptions)
    });
    if (invalidPitch.Success || invalidPitch.ErrorCode != "invalid_range")
    {
        Console.Error.WriteLine("Remote server-side slider validation contract failed.");
        return 31;
    }

    RemoteCommandResult applyPreset;
    try
    {
        applyPreset = await dispatcher.ExecuteAsync(new RemoteIncomingEnvelope
        {
            ProtocolVersion = RemoteProtocol.Version,
            Type = "preset.apply",
            MessageId = Guid.NewGuid().ToString("N"),
            Payload = JsonSerializer.SerializeToElement(new { presetId = remotePreset.Id }, RemoteProtocol.JsonOptions)
        }).WaitAsync(TimeSpan.FromSeconds(5));
    }
    catch (TimeoutException)
    {
        Console.Error.WriteLine("Remote preset command routing contract timed out.");
        return 32;
    }
    if (!applyPreset.Success || Math.Abs(remoteViewModel.Pitch - 4.0) > 0.001 || Math.Abs(remoteViewModel.Formant + 1.0) > 0.001)
    {
        Console.Error.WriteLine("Remote preset command routing contract failed.");
        return 32;
    }

    RemoteStateSnapshot reconnectSnapshot = await dispatcher.CreateSnapshotAsync(publisher.Invalidate());
    if (reconnectSnapshot.Revision <= snapshot.Revision || Math.Abs(reconnectSnapshot.Voice.Pitch - 4.0) > 0.001)
    {
        Console.Error.WriteLine("Remote reconnect snapshot contract failed.");
        return 33;
    }
}
finally
{
    Directory.Delete(remoteRoot, true);
}

string presetRoot = Path.Combine(Path.GetTempPath(), $"GrassiBoard-preset-{Guid.NewGuid():N}");
Directory.CreateDirectory(presetRoot);
try
{
    using var presetViewModel = new MainViewModel(
        new SoundboardStore(Path.Combine(presetRoot, "soundboard.json")));
    presetViewModel.ApplyPreset(2);
    if (presetViewModel.MicGain != 3.0 ||
        presetViewModel.SoundboardGain != -2.0 ||
        !presetViewModel.NoiseGateEnabled ||
        !presetViewModel.CompressorEnabled ||
        !presetViewModel.DuckingEnabled ||
        presetViewModel.DuckingAmount != 6.0 ||
        !presetViewModel.LimiterEnabled ||
        !presetViewModel.ClippingProtectionEnabled ||
        presetViewModel.PitchWetMix != 100.0)
    {
        Console.Error.WriteLine("Broadcast preset contract failed.");
        return 12;
    }
}
finally
{
    Directory.Delete(presetRoot, true);
}

Console.WriteLine("Managed app, decode, persistence, binding, and XAML layout smoke tests passed.");
return 0;

static void WriteMonoPcmWave(string path)
{
    const int sampleRate = 48_000;
    const short channels = 1;
    const short bitsPerSample = 16;
    const int frameCount = 480;
    int dataBytes = frameCount * channels * bitsPerSample / 8;
    using FileStream stream = File.Create(path);
    using var writer = new BinaryWriter(stream);
    writer.Write(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
    writer.Write(36 + dataBytes);
    writer.Write(new byte[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
    writer.Write(new byte[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
    writer.Write(16);
    writer.Write((short)1);
    writer.Write(channels);
    writer.Write(sampleRate);
    writer.Write(sampleRate * channels * bitsPerSample / 8);
    writer.Write((short)(channels * bitsPerSample / 8));
    writer.Write(bitsPerSample);
    writer.Write(new byte[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
    writer.Write(dataBytes);
    for (int frame = 0; frame < frameCount; ++frame)
    {
        writer.Write((short)(Math.Sin(frame * 2.0 * Math.PI * 440.0 / sampleRate) * 4000.0));
    }
}
