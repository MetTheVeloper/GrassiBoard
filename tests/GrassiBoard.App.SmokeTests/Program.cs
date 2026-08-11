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

if (BuildInfo.CurrentVersion != "1.1.0" || NativeAudioEngine.ExpectedApiVersion != 8U)
{
    Console.Error.WriteLine("Managed version contract is inconsistent.");
    return 1;
}

string fixture = Path.Combine(AppContext.BaseDirectory, "BuildInfo.fixture.json");
File.WriteAllText(fixture, """
    {
      "Version": "1.1.0",
      "CommitSha": "0123456789abcdef",
      "TargetArchitecture": "x64"
    }
    """);

BuildInfo info = BuildInfo.Load(fixture);
File.Delete(fixture);

if (info.Version != "1.1.0" || info.ShortCommit != "01234567" || info.TargetArchitecture != "x64")
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
string remoteProtocolSource = File.ReadAllText(Path.Combine(appSource, "Services", "Remote", "RemoteProtocol.cs"));
string remoteSettingsSource = File.ReadAllText(Path.Combine(appSource, "Services", "Remote", "RemoteSettingsStore.cs"));
string remoteMdnsSource = File.ReadAllText(Path.Combine(appSource, "Services", "Remote", "RemoteMdnsService.cs"));
string remoteWebRoot = Path.Combine(Environment.CurrentDirectory, "src", "GrassiBoard.RemoteWeb");
string remoteWebConnectionSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "composables", "useRemoteConnection.ts"));
string remoteQrScannerSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "components", "QrScannerModal.vue"));
string remotePwaPluginSource = File.ReadAllText(Path.Combine(remoteWebRoot, "app", "plugins", "pwa.client.ts"));
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
    !remoteManifestSource.Contains("\"name\": \"GrassiMote\"", StringComparison.Ordinal) ||
    !remoteManifestSource.Contains("\"display\": \"standalone\"", StringComparison.Ordinal) ||
    !remoteServiceWorkerSource.Contains("grassimote-shell-v1", StringComparison.Ordinal) ||
    !remoteServiceWorkerSource.Contains("url.pathname.startsWith('/api/')", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Remote isolation/publish source contract failed.");
    return 34;
}
if (!installerServiceSource.Contains("ProductVersion = \"1.1.0\"", StringComparison.Ordinal) ||
    !installerWindowXaml.Contains("Ready to install GrassiBoard 1.1.0", StringComparison.Ordinal))
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
