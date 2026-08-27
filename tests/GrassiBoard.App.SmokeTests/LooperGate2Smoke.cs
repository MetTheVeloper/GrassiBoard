using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GrassiBoard.Services;
using GrassiBoard.Services.Looper;

internal static class LooperGate2Smoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        if (Marshal.SizeOf<LooperNativeState>() != 48)
        {
            throw new InvalidOperationException("Gate 2 managed/native Looper state ABI layout must remain 48 bytes.");
        }
        if (LooperMonitorService.MaxStereoFloatBytes != 230_400_000L)
        {
            throw new InvalidOperationException("Gate 2 ten-minute 48 kHz stereo-float safety baseline changed unexpectedly.");
        }

        string root = Environment.CurrentDirectory;
        string app = Path.Combine(root, "src", "GrassiBoard.App");
        string native = Path.Combine(root, "src", "GrassiBoard.AudioEngine");
        string looperXaml = File.ReadAllText(Path.Combine(app, "Views", "Looper", "LooperView.xaml"));
        string looperCodeBehind = File.ReadAllText(Path.Combine(app, "Views", "Looper", "LooperView.xaml.cs"));
        string looperViewModel = File.ReadAllText(Path.Combine(app, "ViewModels", "LooperViewModel.cs"));
        string waveformView = File.ReadAllText(Path.Combine(app, "Views", "Looper", "WaveformView.cs"));
        string nativeHeader = File.ReadAllText(Path.Combine(native, "include", "grassiboard", "audio_engine.h"));
        string nativeLooper = File.ReadAllText(Path.Combine(native, "src", "looper_engine.cpp"));
        string nativeMixer = File.ReadAllText(Path.Combine(native, "src", "mixer_processor.cpp"));

        string[] requiredXaml =
        [
            "AuditionPlayPauseCommand",
            "AuditionSeekCommand",
            "TransportPlayPauseCommand",
            "TransportStopCommand",
            "EditMasterCommand",
            "PendingPlayheadPosition",
            "MasterPlayheadPosition",
            "LooperMonitorOutputCombo",
            "OnFitSelectionClick"
        ];
        if (requiredXaml.Any(contract => !looperXaml.Contains(contract, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Gate 2 Looper transport/editor XAML contract failed.");
        }
        if (!looperViewModel.Contains("OnSelectionRestartTick", StringComparison.Ordinal) ||
            !looperViewModel.Contains("SeekPendingSelection", StringComparison.Ordinal) ||
            !looperViewModel.Contains("EditMasterAsync", StringComparison.Ordinal) ||
            !looperViewModel.Contains("EditorWaveformBuckets", StringComparison.Ordinal) ||
            !waveformView.Contains("new FrameworkPropertyMetadata(-1.0", StringComparison.Ordinal) ||
            !waveformView.Contains("ZoomToSelection", StringComparison.Ordinal) ||
            !waveformView.Contains("MaxZoom", StringComparison.Ordinal) ||
            !looperCodeBehind.Contains("SelectedMonitorOutput", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Gate 2 seek/zoom/edit/shared-monitor source contract failed.");
        }
        if (!nativeHeader.Contains("gb_looper_load_master", StringComparison.Ordinal) ||
            !nativeHeader.Contains("gb_looper_set_transport", StringComparison.Ordinal) ||
            !nativeHeader.Contains("gb_looper_seek", StringComparison.Ordinal) ||
            !nativeHeader.Contains("gb_looper_monitor_read", StringComparison.Ordinal) ||
            !nativeLooper.Contains("LooperEngine::Seek", StringComparison.Ordinal) ||
            !nativeLooper.Contains("BeginMutation", StringComparison.Ordinal) ||
            !nativeLooper.Contains("kSeamFadeFrames", StringComparison.Ordinal) ||
            !nativeMixer.Contains("looper_clock_->RenderFrame", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Gate 2 native shared-clock/seek/monitor API source contract failed.");
        }
    }
}