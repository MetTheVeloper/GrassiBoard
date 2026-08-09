# Test plan

## Automated v0.8.x checks

- Build native x64 Release with `/W4 /WX` and validate ABI version 5 / current v0.8.x engine version.
- Retain all accepted Pitch, Formant, quality-switching, benchmark, device-pairing, and DSP regression tests.
- Test the isolated Soundboard mixer: clip loading, one-shot completion, simultaneous mix, Loop, per-pad stop, and Stop All.
- Test managed WAV decode/resample contract and Sound Pad JSON round-trip.
- Publish a self-contained portable app with the native DLL, NAudio runtime dependencies, BuildInfo, documentation, changelog, and third-party notices.
- Verify no experimental SYS/INF/CAT/certificate or third-party virtual-cable installer enters the portable package.

## Manual v0.8.x acceptance

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
4. Play multiple Pads and use global Stop All; require the microphone to remain live.
5. Speak while a Pad plays; require both signals in the target application.
6. Enable Voice FX and change Pitch; require the microphone to change while Pad audio stays unpitched.
7. Toggle Mute Mic; require Pad audio to continue.
8. Edit volume/title/source behavior, remove a Pad, and confirm the source file is not deleted.
9. Close and reopen GrassiBoard; require remaining Pads to restore.
10. Temporarily move one referenced file; require a safe missing-file state and successful repair through Edit.

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
Pad persistence works: Yes/No

Microphone + Soundboard mixed correctly: Yes/No
Soundboard incorrectly pitch-shifted: Yes/No

Crackling/dropouts:
Perceived latency:
UI issues:
Crash/freeze:
Other notes:
```

Do not begin v0.9.0 until the user explicitly accepts the current v0.8.x build.
