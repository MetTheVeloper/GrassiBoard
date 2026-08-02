using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using GrassiBoard.Shared;

namespace GrassiBoard;

public partial class MainWindow : Window
{
    private const uint ExpectedNativeApiVersion = 4;
    private readonly DispatcherTimer _meterTimer;
    private nint _engine;
    private bool _running;
    private bool _busy;
    private bool _closing;
    private IReadOnlyList<AudioDevice> _captureDevices = [];
    private AudioDevice? _targetCaptureEndpoint;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        _meterTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, OnMeterTick, Dispatcher);
        _meterTimer.Stop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildInfo build = BuildInfo.Load(Path.Combine(AppContext.BaseDirectory, "BuildInfo.json"));
        VersionText.Text = $"v{build.Version}";
        CommitText.Text = build.ShortCommit;

        try
        {
            uint apiVersion = NativeMethods.GetApiVersion();
            string nativeVersion = Marshal.PtrToStringUTF8(NativeMethods.GetVersion()) ?? "unknown";
            if (apiVersion != ExpectedNativeApiVersion)
            {
                SetNativeStatus($"ABI mismatch · expected {ExpectedNativeApiVersion}, got {apiVersion}", false);
                StartStopButton.IsEnabled = false;
                return;
            }

            NativeResult result = NativeMethods.EngineCreate(ExpectedNativeApiVersion, out _engine);
            if (result != NativeResult.Ok || _engine == nint.Zero)
            {
                SetNativeStatus($"Engine creation failed · {result}", false);
                StartStopButton.IsEnabled = false;
                return;
            }

            SetNativeStatus($"Ready · API {apiVersion} · v{nativeVersion}", true);
            ApplyPitchSettings();
            RefreshDevices();
            _meterTimer.Start();
        }
        catch (Exception exception) when (exception is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            SetNativeStatus($"Native load failed · {exception.GetType().Name}", false);
            StartStopButton.IsEnabled = false;
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshDevices();
    }

    private async void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _engine == nint.Zero)
        {
            return;
        }

        _busy = true;
        UpdateControlState();
        try
        {
            if (!_running)
            {
                if (InputDeviceCombo.SelectedItem is not AudioDevice input ||
                    CableOutputCombo.SelectedItem is not AudioDevice cableOutput)
                {
                    SetNativeStatus("Select an input microphone and a virtual cable input", false);
                    return;
                }

                SetNativeStatus("Starting WASAPI streams…", true);
                NativeResult result = await Task.Run(() => NativeMethods.EngineStart(_engine, input.Id, cableOutput.Id));
                if (_closing)
                {
                    return;
                }
                if (result != NativeResult.Ok)
                {
                    SetNativeStatus($"Start failed · {result} · {ReadLastError()}", false);
                    return;
                }

                _running = true;
                string destination = _targetCaptureEndpoint is null
                    ? cableOutput.Name
                    : $"target mic: {_targetCaptureEndpoint.Name}";
                SetNativeStatus($"Running · 48 kHz · {SelectedQualityName()} · {destination}", true);
            }
            else
            {
                NativeResult result = await Task.Run(() => NativeMethods.EngineStop(_engine));
                if (_closing)
                {
                    return;
                }
                _running = false;
                SetNativeStatus(result == NativeResult.Ok ? "Stopped · ready" : $"Stop failed · {result}", result == NativeResult.Ok);
                ResetMeters();
            }
        }
        finally
        {
            _busy = false;
            if (!_closing)
            {
                UpdateControlState();
            }
        }
    }

    private void PitchSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PitchValueText is null)
        {
            return;
        }
        PitchValueText.Text = $"{e.NewValue:+0;-0;0} semitones";
        if (_engine != nint.Zero)
        {
            NativeMethods.SetPitchSemitones(_engine, (float)e.NewValue);
        }
    }

    private void FinePitchSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FinePitchValueText is null)
        {
            return;
        }
        FinePitchValueText.Text = $"{e.NewValue:+0;-0;0} cents";
        if (_engine != nint.Zero)
        {
            NativeMethods.SetPitchCents(_engine, (float)e.NewValue);
        }
    }

    private void PitchBypassCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_engine != nint.Zero)
        {
            NativeMethods.SetPitchBypass(_engine, PitchBypassCheck.IsChecked == true ? 1U : 0U);
        }
    }

    private void FormantSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FormantValueText is null)
        {
            return;
        }
        FormantValueText.Text = $"{e.NewValue:+0.0;-0.0;0} semitones";
        if (_engine != nint.Zero)
        {
            NativeMethods.SetFormantSemitones(_engine, (float)e.NewValue);
        }
    }

    private void FormantPreservationCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_engine != nint.Zero)
        {
            NativeMethods.SetFormantPreservation(
                _engine, FormantPreservationCheck.IsChecked == true ? 1U : 0U);
        }
    }

    private void PitchQualityCombo_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_engine != nint.Zero && PitchQualityCombo.SelectedIndex >= 0)
        {
            NativeMethods.SetPitchQuality(_engine, (uint)PitchQualityCombo.SelectedIndex);
            if (_running)
            {
                SetNativeStatus($"Running · 48 kHz · {SelectedQualityName()}", true);
            }
        }
    }

    private string SelectedQualityName()
    {
        return PitchQualityCombo.SelectedIndex switch
        {
            0 => "Low latency",
            2 => "High quality",
            _ => "Balanced"
        };
    }

    private void ApplyPitchSettings()
    {
        NativeMethods.SetPitchSemitones(_engine, (float)PitchSlider.Value);
        NativeMethods.SetPitchCents(_engine, (float)FinePitchSlider.Value);
        NativeMethods.SetPitchBypass(_engine, PitchBypassCheck.IsChecked == true ? 1U : 0U);
        NativeMethods.SetFormantSemitones(_engine, (float)FormantSlider.Value);
        NativeMethods.SetFormantPreservation(
            _engine, FormantPreservationCheck.IsChecked == true ? 1U : 0U);
        NativeMethods.SetPitchQuality(_engine, (uint)Math.Max(PitchQualityCombo.SelectedIndex, 0));
    }

    private void RefreshDevices()
    {
        try
        {
            IReadOnlyList<AudioDevice> inputs = ReadDevices(input: true);
            IReadOnlyList<AudioDevice> outputs = ReadDevices(input: false);
            _captureDevices = inputs;
            InputDeviceCombo.ItemsSource = inputs;
            CableOutputCombo.ItemsSource = outputs;

            InputDeviceCombo.SelectedItem =
                inputs.FirstOrDefault(device => device.IsDefault && !IsExternalVirtualEndpoint(device)) ??
                inputs.FirstOrDefault(device => !IsExternalVirtualEndpoint(device)) ??
                inputs.FirstOrDefault();

            CableOutputCombo.SelectedItem = outputs.FirstOrDefault(output => FindPairedCaptureEndpoint(output) is not null) ??
                outputs.FirstOrDefault(device => device.IsDefault) ??
                outputs.FirstOrDefault();

            bool ready = inputs.Count > 0 && outputs.Count > 0;
            if (!ready)
            {
                SetNativeStatus("No active microphone or render output was found", false);
            }
            StartStopButton.IsEnabled = ready;
            UpdateCableRouteStatus();
        }
        catch (Exception exception)
        {
            SetNativeStatus($"Device enumeration failed · {exception.Message}", false);
            StartStopButton.IsEnabled = false;
        }
        UpdateControlState();
    }

    private void CableOutputCombo_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CableRouteText is not null)
        {
            UpdateCableRouteStatus();
        }
    }

    private void UpdateCableRouteStatus()
    {
        _targetCaptureEndpoint = CableOutputCombo.SelectedItem is AudioDevice output
            ? FindPairedCaptureEndpoint(output)
            : null;

        if (_targetCaptureEndpoint is not null && CableOutputCombo.SelectedItem is AudioDevice cableInput)
        {
            CableRouteText.Text = $"Cable ready · {cableInput.Name}";
            CableRouteHintText.Text =
                $"In Discord, OBS, Voice Recorder, or another target app, select: {_targetCaptureEndpoint.Name}";
            CableRouteText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#75E6B5"));
            return;
        }

        CableRouteText.Text = "No paired external virtual-cable microphone was detected for this output.";
        CableRouteHintText.Text =
            "Install an external cable or select its playback/input endpoint. GrassiBoard's retired test driver is ignored.";
        CableRouteText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4D899"));
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

    private static IReadOnlyList<AudioDevice> ReadDevices(bool input)
    {
        uint required;
        NativeResult result = input
            ? NativeMethods.EnumerateInputDevices(nint.Zero, 0, out required)
            : NativeMethods.EnumerateOutputDevices(nint.Zero, 0, out required);
        if (result != NativeResult.Ok || required <= 1)
        {
            return Array.Empty<AudioDevice>();
        }

        nint buffer = Marshal.AllocHGlobal(checked((int)required));
        try
        {
            uint written;
            result = input
                ? NativeMethods.EnumerateInputDevices(buffer, required, out written)
                : NativeMethods.EnumerateOutputDevices(buffer, required, out written);
            if (result != NativeResult.Ok || written == 0)
            {
                throw new InvalidOperationException(result.ToString());
            }

            string json = Marshal.PtrToStringUTF8(buffer, checked((int)written - 1)) ?? "[]";
            return JsonSerializer.Deserialize<List<AudioDevice>>(json, JsonOptions) ?? [];
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void OnMeterTick(object? sender, EventArgs e)
    {
        if (_engine == nint.Zero)
        {
            return;
        }

        NativeResult result = NativeMethods.GetAudioStatistics(_engine, out AudioStatistics statistics);
        if (result != NativeResult.Ok)
        {
            return;
        }

        InputMeter.Value = Math.Clamp(statistics.InputPeak * 100.0, 0.0, 100.0);
        OutputMeter.Value = Math.Clamp(statistics.OutputPeak * 100.0, 0.0, 100.0);
        InputDbText.Text = FormatDb(statistics.InputPeak);
        OutputDbText.Text = FormatDb(statistics.OutputPeak);
        BufferText.Text = $"Capture {statistics.CaptureBufferFrames} · Render {statistics.RenderBufferFrames}";
        RingFillText.Text = $"{statistics.RingBufferFillFrames} frames";
        double latencyMilliseconds = statistics.PitchLatencySamples * 1000.0 / statistics.SampleRate;
        PitchLatencyText.Text = $"{statistics.PitchLatencySamples} · {latencyMilliseconds:0.0} ms";
        DropoutText.Text = $"U {statistics.UnderrunCount} · O {statistics.OverrunCount} · D {statistics.DiscontinuityCount}";

        if (_running && statistics.Running == 0)
        {
            _running = false;
            SetNativeStatus($"Audio stream stopped · {ReadLastError()}", false);
            UpdateControlState();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _closing = true;
        _meterTimer.Stop();
        if (_engine != nint.Zero)
        {
            NativeMethods.EngineStop(_engine);
            NativeMethods.EngineDestroy(_engine);
            _engine = nint.Zero;
        }
    }

    private string ReadLastError()
    {
        NativeResult result = NativeMethods.GetLastError(_engine, nint.Zero, 0, out uint required);
        if (result != NativeResult.Ok || required <= 1)
        {
            return "no detail";
        }

        nint buffer = Marshal.AllocHGlobal(checked((int)required));
        try
        {
            result = NativeMethods.GetLastError(_engine, buffer, required, out uint written);
            return result == NativeResult.Ok
                ? Marshal.PtrToStringUTF8(buffer, checked((int)written - 1)) ?? "no detail"
                : result.ToString();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void UpdateControlState()
    {
        bool selectionEnabled = !_running && !_busy;
        InputDeviceCombo.IsEnabled = selectionEnabled;
        CableOutputCombo.IsEnabled = selectionEnabled;
        RefreshButton.IsEnabled = selectionEnabled;
        StartStopButton.IsEnabled = _engine != nint.Zero && !_busy &&
            (_running || (InputDeviceCombo.SelectedItem is not null && CableOutputCombo.SelectedItem is not null));
        StartStopButton.Content = _busy ? "Please wait…" : _running ? "Stop engine" : "Start engine";
        StartStopButton.Background = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(_running ? "#FF7A90" : "#75E6B5"));
    }

    private void ResetMeters()
    {
        InputMeter.Value = 0;
        OutputMeter.Value = 0;
        InputDbText.Text = "−∞ dBFS";
        OutputDbText.Text = "−∞ dBFS";
    }

    private static string FormatDb(float linear)
    {
        return linear <= 0.000001F ? "−∞ dBFS" : $"{20.0 * Math.Log10(linear):0.0} dBFS";
    }

    private void SetNativeStatus(string text, bool healthy)
    {
        NativeStatusText.Text = text;
        NativeStatusDot.Fill = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(healthy ? "#75E6B5" : "#FF7A90"));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record AudioDevice
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string ContainerId { get; init; } = string.Empty;
        public bool IsDefault { get; init; }

        public AudioEndpointDescriptor ToDescriptor() => new(Id, Name, ContainerId, IsDefault);
    }

    private enum NativeResult
    {
        Ok = 0,
        InvalidArgument = 1,
        OutOfMemory = 2,
        Com = 3,
        DeviceNotFound = 4,
        AudioClient = 5,
        AlreadyRunning = 6,
        NotRunning = 7,
        BufferTooSmall = 8,
        Internal = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioStatistics
    {
        public uint StructSize;
        public uint Running;
        public uint SampleRate;
        public uint CaptureBufferFrames;
        public uint RenderBufferFrames;
        public uint RingBufferFillFrames;
        public uint PitchLatencySamples;
        public ulong CapturedFrames;
        public ulong RenderedFrames;
        public ulong UnderrunCount;
        public ulong OverrunCount;
        public ulong DiscontinuityCount;
        public float InputPeak;
        public float InputRms;
        public float OutputPeak;
        public float OutputRms;
    }

    private static partial class NativeMethods
    {
        private const string LibraryName = "GrassiBoard.AudioEngine.dll";

        [LibraryImport(LibraryName, EntryPoint = "gb_get_api_version")]
        internal static partial uint GetApiVersion();

        [LibraryImport(LibraryName, EntryPoint = "gb_get_version")]
        internal static partial nint GetVersion();

        [LibraryImport(LibraryName, EntryPoint = "gb_enumerate_input_devices")]
        internal static partial NativeResult EnumerateInputDevices(nint buffer, uint capacity, out uint required);

        [LibraryImport(LibraryName, EntryPoint = "gb_enumerate_output_devices")]
        internal static partial NativeResult EnumerateOutputDevices(nint buffer, uint capacity, out uint required);

        [LibraryImport(LibraryName, EntryPoint = "gb_engine_create")]
        internal static partial NativeResult EngineCreate(uint apiVersion, out nint engine);

        [LibraryImport(LibraryName, EntryPoint = "gb_engine_destroy")]
        internal static partial void EngineDestroy(nint engine);

        [LibraryImport(LibraryName, EntryPoint = "gb_engine_start", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial NativeResult EngineStart(nint engine, string inputDeviceId, string monitorDeviceId);

        [LibraryImport(LibraryName, EntryPoint = "gb_engine_stop")]
        internal static partial NativeResult EngineStop(nint engine);

        [LibraryImport(LibraryName, EntryPoint = "gb_set_pitch_semitones")]
        internal static partial NativeResult SetPitchSemitones(nint engine, float semitones);

        [LibraryImport(LibraryName, EntryPoint = "gb_set_pitch_cents")]
        internal static partial NativeResult SetPitchCents(nint engine, float cents);

        [LibraryImport(LibraryName, EntryPoint = "gb_set_pitch_bypass")]
        internal static partial NativeResult SetPitchBypass(nint engine, uint bypass);

        [LibraryImport(LibraryName, EntryPoint = "gb_set_formant_semitones")]
        internal static partial NativeResult SetFormantSemitones(nint engine, float semitones);

        [LibraryImport(LibraryName, EntryPoint = "gb_set_formant_preservation")]
        internal static partial NativeResult SetFormantPreservation(nint engine, uint preserve);

        [LibraryImport(LibraryName, EntryPoint = "gb_set_pitch_quality")]
        internal static partial NativeResult SetPitchQuality(nint engine, uint qualityMode);

        [LibraryImport(LibraryName, EntryPoint = "gb_get_audio_statistics")]
        internal static partial NativeResult GetAudioStatistics(nint engine, out AudioStatistics statistics);

        [LibraryImport(LibraryName, EntryPoint = "gb_get_last_error")]
        internal static partial NativeResult GetLastError(nint engine, nint buffer, uint capacity, out uint required);
    }
}
