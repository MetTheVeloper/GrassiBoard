# Test plan

## Automated v0.9.0 checks

- Build native x64 Release with `/W4 /WX` and validate ABI version 6 / engine version 0.9.0.
- Retain all accepted Pitch, Formant, quality-switching, benchmark, device-pairing, and DSP regression tests.
- Test the isolated Soundboard mixer: clip loading, one-shot completion, simultaneous mix, Loop, per-pad stop, and Stop All.
- Test managed WAV decode/resample contract and Sound Pad JSON round-trip.
- Test unity/default Mixer behavior, gain, gate, ducking, linked limiting, and clipping protection.
- Test the fixed Mixer ABI layout, built-in preset values, safe dBFS mapping, shared meter template, global Stop All engine lifecycle call, and monitor work-area window hook.
- Publish a self-contained portable app with the native DLL, NAudio runtime dependencies, BuildInfo, documentation, changelog, and third-party notices.
- Verify no experimental SYS/INF/CAT/certificate or third-party virtual-cable installer enters the portable package.

## Manual v0.9.0 acceptance

### Regression

1. Launch GrassiBoard on the accepted Windows 10 test system.
2. Open Routing, select the physical Microsoft LifeChat microphone and known working VB-CABLE playback endpoint, then start the engine.
3. In Voice Recorder or Telegram, select the paired cable recording endpoint as the microphone.
4. Confirm normal microphone routing, Pitch, Fine Pitch, and Formant.
5. Navigate Board → Voice → Routing → Settings while audio is live; require no interruption or engine restart.

### Soundboard

1. Add one WAV and one MP3.
2. Play each separately, then simultaneously.
3. Set a Pad to Loop and stop it with its Stop button.
4. Play multiple Pads and use global Stop All; require every Pad and the engine to stop.
5. Speak while a Pad plays; require both signals in the target application.
6. Enable Voice FX and change Pitch; require the microphone to change while Pad audio stays unpitched.
7. Toggle Mute Mic; require Pad audio to continue.
8. Edit volume/title/source behavior, remove a Pad, and confirm the source file is not deleted.
9. Close and reopen GrassiBoard; require remaining Pads to restore.
10. Temporarily move one referenced file; require a safe missing-file state and successful repair through Edit.

### Mixer and mandatory fixes

1. Confirm top MIC/BOARD/MASTER and Board Quick Levels fills move and silence/invalid values render empty in both themes.
2. Test Mic, Soundboard, and Master gain independently.
3. Test Gate, Compressor, Limiter, Ducking, Clipping Protection, Pitch Wet/Dry, and every built-in preset.
4. Confirm restored border/shadow, resize/drag, maximize to the current monitor work area without covering the taskbar, and correct restore bounds.
5. Exercise global Stop All with one Pad, simultaneous Pads, Loop, and an already stopped engine.
6. After Stop All, start the engine again and verify the external-cable microphone route without restarting GrassiBoard.

### Stability

1. Leave the engine running and trigger Pads repeatedly for several minutes.
2. Observe U/O/D diagnostics before and after.
3. Report crackling, perceived delay, freezes, crashes, unexpected routing, or layout issues at 1280×800 and 1024×700.

## Required report

```text
Version:
Commit:

Application opened: Yes/No
Engine started: Yes/No

Microphone routing works: Yes/No
Pitch works: Yes/No
Formant works: Yes/No
Virtual microphone works: Yes/No

Board page works: Yes/No
Voice page works: Yes/No
Routing page works: Yes/No
Settings page works: Yes/No
Navigation interrupted audio: Yes/No

WAV pad works: Yes/No
MP3 pad works: Yes/No
Multiple pads work: Yes/No
Loop works: Yes/No
Stop All works: Yes/No
Stop All stops engine and restart works: Yes/No
Pad persistence works: Yes/No

Microphone + Soundboard mixed correctly: Yes/No
Soundboard incorrectly pitch-shifted: Yes/No

MIC/BOARD/MASTER and Quick meters move: Yes/No
Restored border/shadow: Yes/No
Maximize respects taskbar: Yes/No
Mic/Soundboard/Master Gain: Yes/No
Gate/Compressor/Limiter/Ducking/Clipping Protection: Yes/No
Pitch Wet/Dry and Presets: Yes/No

Crackling/dropouts:
Perceived latency:
UI issues:
Crash/freeze:
Other notes:
```

Do not declare v0.9.0 complete or begin v0.10.0 until the user explicitly accepts this build.
