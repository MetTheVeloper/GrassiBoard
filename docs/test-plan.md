# v0.11.0 combined test plan

The user has manually accepted v0.9.0. That build is the regression baseline. v0.11.0 is not complete until CI is green, a downloadable package exists, and the user explicitly approves this matrix on the Windows 10 / Microsoft LifeChat / VB-CABLE setup.

## Automated gates

- Build native x64 Release with `/W4 /WX`; require product `0.11.0`, native ABI 7, and matching 144-byte managed/native statistics layout.
- Retain all accepted Pitch/Formant mode, benchmark, device pairing, Soundboard, Mixer/Dynamics, lifecycle, UI/XAML, package-isolation, and persistence tests.
- Test the bounded Media SPSC ring: inactive silence, FIFO, capacity, clear, and finite-sample protection; test its exported ABI and statistics.
- Test managed Profile migration/round-trip, malformed item isolation, full Profile cloning, hotkey parsing, Pad decode/persistence, crash reporting, and UI contracts.
- Publish self-contained Windows x64 portable/symbol/test packages; include no experimental driver/certificate or external cable installer.

## Manual setup

1. Extract the complete v0.11.0 portable ZIP into a new folder and run `GrassiBoard.exe`.
2. In Routing select the physical microphone and working VB-CABLE playback/input endpoint. Select the paired cable recording endpoint in Voice Recorder/Telegram/OBS.
3. Start at low headphone volume. Media monitoring routes only Media to the selected headphone endpoint; it must never monitor the microphone.

## Regression

1. Confirm microphone routing, Pitch, Fine Pitch, Formant/preservation, quality switching, Mixer/Dynamics, Pads, meters, themes, custom window behavior, and Start/Stop.
2. Navigate every page while live; require no engine restart/interruption.
3. Use Stop All during multiple/looped Pads and again while idle; require safe engine restart and no deleted configuration.

## Profiles and presets

1. Create, duplicate, rename, apply, and delete Profiles. Restart and verify selected devices, Pads, hotkeys, user presets, Voice/Mixer values, and preferences.
2. Save the current Voice + Mixer state as a named preset. Apply, update, duplicate, rename, delete, and restart.
3. Apply contrasting presets while speaking and while a Pad/Media plays. Require an immediate but smooth ~200 ms change, no click/pop, no stream restart, and no Pad/Media interruption.
4. Temporarily corrupt one preset entry in a backup test JSON only; valid siblings must still load.

## Global hotkeys and Tray

1. Assign Pad and user-preset hotkeys. Trigger them with another application focused and while GrassiBoard is hidden in Tray.
2. Test Mic Mute, Stop All, Voice FX, Show/Hide, Media Play/Pause/Stop/±10 globally.
3. Configure Push-to-Talk: hold must open the mic; release must mute; the engine must remain running.
4. Assign the same gesture twice and a Windows-reserved/unavailable gesture; Settings must report failure rather than silently ignore it.
5. Test minimize/restore, Tray Show/Mute/Stop All/Exit, Start Minimized, and optional Start with Windows. Disable Start with Windows afterward if not wanted.

## Local Media Deck

1. Load a long MP3, then a WAV and any locally available M4A/MP4 audio track. Unsupported codecs must fail visibly without crashing.
2. Test Play, Pause, Resume, Stop-to-zero, timeline drag, -10, +10, current/total time, volume, activity meter, and hotkeys.
3. Test Monitor/Send combinations ON/ON, ON/OFF, OFF/ON, OFF/OFF.
4. With Monitor ON, require Media in the USB headphones and no microphone echo. With Send ON, require Media at the target app microphone.
5. Speak over Media and trigger Pads/presets. Require microphone + Pad + Media in the cable mix; only microphone character changes with Pitch/Formant.
6. Move the remembered file, restart, verify a safe missing-file state, then locate another file or Clear. Require no autoplay at startup.
7. Run Media 30–60 minutes while occasionally speaking/triggering Pads. Inspect Media fill/underruns and U/O/D for growth, crackling, memory growth, UI blocking, or desynchronization.

## Latency and combined load

1. Record perceived latency and Diagnostics in Balanced before changing mode.
2. Compare Low, Balanced, and High with the same microphone/headset. Record capture/render frames, Pitch ms, ring fill, estimated total, U/O/D, and Media underruns.
3. While Media plays, speak, trigger Pads, and apply presets repeatedly. Stability wins; do not accept a lower number with crackling/dropouts/artifacts.

## Required report

```text
Version:
Commit:
Windows version:
USB headset:

PRESETS
Save / survives restart / Apply / smooth transition:
Rename / Update / Duplicate / Delete / preset hotkey:

PROFILES
Create / Duplicate / Rename / Apply / Delete / survives restart:
Devices / Pads / presets / hotkeys / preferences restored:

HOTKEYS
Pad / Mute / Stop All / Voice FX / Push-to-Talk / Show-Hide:
Conflicts reported / survive restart:

TRAY
Minimize / Restore / menu / Start minimized / Start with Windows / clean Exit:

MEDIA DECK
Long audio Load / Play / Pause / Resume / Stop / Seek / ±10 / Timeline:
Volume / Meter / hotkeys:
Monitor ON-OFF / Send ON-OFF / all four combinations:
Speak over media / Media in headphones / Mic NOT in headphones:
Missing file safety / no autoplay / 30–60 min stability:

LATENCY
Low / Balanced / High perceived and reported latency:
Capture / Render / Pitch / Ring / Media fill:
Crackling / Dropouts / Underruns / Overruns / CPU issues:

REGRESSION
Voice / Mixer / Soundboard / VB-CABLE / themes / custom window / meters:
Stop All stops Pads + Media + engine and engine restarts:

OTHER
UI issues / crashes / logs / screenshots:
```

Stop after delivering v0.11.0. Do not begin Stability/Packaging until the user explicitly accepts this build.
