using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GrassiBoard.Services;
using GrassiBoard.Services.Looper;

internal static class LooperGate3Smoke
{
    [ModuleInitializer]
    internal static void Run()
    {
#if REMOTE_MONITOR_SPIKE
        if (NativeAudioEngine.ExpectedApiVersion != 11U)
        {
            throw new InvalidOperationException("Gate 3 requires managed native ABI 11.");
        }
#endif
        if (Marshal.SizeOf<LooperRecordNativeState>() != 40)
        {
            throw new InvalidOperationException("Gate 3 managed/native record-state ABI layout must remain 40 bytes.");
        }
        if (LooperRecordService.SampleRate != 48_000 ||
            LooperRecordService.Channels != 2 ||
            LooperRecordService.MaxCaptureFrames != 28_800_000L)
        {
            throw new InvalidOperationException("Gate 3 first-Master capture safety contract changed unexpectedly.");
        }

        string root = Environment.CurrentDirectory;
        string app = Path.Combine(root, "src", "GrassiBoard.App");
        string native = Path.Combine(root, "src", "GrassiBoard.AudioEngine");
        string xaml = File.ReadAllText(Path.Combine(app, "Views", "Looper", "LooperView.xaml"));
        string codeBehind = File.ReadAllText(Path.Combine(app, "Views", "Looper", "LooperView.xaml.cs"));
        string recordService = File.ReadAllText(Path.Combine(app, "Services", "Looper", "LooperRecordService.cs"));
        string nativeHeader = File.ReadAllText(Path.Combine(native, "include", "grassiboard", "audio_engine.h"));
        string nativeWorker = File.ReadAllText(Path.Combine(native, "src", "wasapi_engine.cpp"));

        string[] requiredRecordApi =
        [
            "gb_looper_record_start",
            "gb_looper_record_stop",
            "gb_looper_record_read",
            "gb_looper_record_get_state"
        ];
        if (requiredRecordApi.Any(api => !nativeHeader.Contains(api, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Gate 3 dedicated Looper Record Tap ABI source contract failed.");
        }

        if (!xaml.Contains("Record First Loop", StringComparison.Ordinal) ||
            !xaml.Contains("OnRecordFirstLoopClick", StringComparison.Ordinal) ||
            !xaml.Contains("Gate 3 processed microphone capture", StringComparison.Ordinal) ||
            !codeBehind.Contains("LooperRecordService", StringComparison.Ordinal) ||
            !codeBehind.Contains("WriteTakeWaveAsync", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Gate 3 first microphone Master UI/trim handoff contract failed.");
        }

        int recordWrite = nativeWorker.IndexOf("looper_record_tap_.Push(recorded, recorded)", StringComparison.Ordinal);
        int muteMarker = nativeWorker.IndexOf("Program Mic Mute happens after the dedicated Looper Record Tap", StringComparison.Ordinal);
        if (recordWrite < 0 || muteMarker < 0 || recordWrite >= muteMarker)
        {
            throw new InvalidOperationException("Gate 3 Record Tap must remain before Program Mic Mute.");
        }
        if (!nativeWorker.Contains("looper_record_source_changed_.store(true", StringComparison.Ordinal) ||
            !recordService.Contains("The Take was discarded instead of combining two inputs", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Gate 3 microphone-source handoff must fail safely during an active Take.");
        }
        if (!recordService.Contains("_drainedFrames", StringComparison.Ordinal) ||
            !recordService.Contains("audioState.Running == 0U", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Gate 3 cumulative capture diagnostics or engine-stop discard contract is missing.");
        }
        if (codeBehind.Contains("SetVoiceMonitorTapEnabled", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Gate 3 must not enable delayed live microphone monitoring while recording.");
        }
    }
}
