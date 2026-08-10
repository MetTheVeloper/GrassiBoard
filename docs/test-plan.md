# v1.0.0 final release-candidate test plan

The user has accepted v0.11.2 as the functional regression baseline. v1.0.0 remains a prerelease until CI is green and the Windows 10 / Microsoft LifeChat / external-cable installation and long-duration matrix below is explicitly approved.

## Automated release gate

- Build native x64 Release with `/W4 /WX`; require product `1.0.0`, native ABI 8, and the matching 144-byte managed/native statistics layout.
- Pass native DSP, Mixer, Pad, Media ring/alignment, lifecycle, and version tests.
- Pass managed persistence, selector-label, device-recovery policy, XAML/resource, icon, decode, and packaging smoke tests.
- Publish and verify portable, symbols, tests, and exactly one self-contained Setup EXE containing the portable payload.
- Keep the tagged GitHub release marked prerelease until this manual matrix passes.

## A. UI regression

1. Open Board, Mixer, Routing, and Settings in Light and Dark themes.
2. Confirm Profile, user-preset, input/output, and monitor selectors show human-readable names only.
3. Confirm text and icons on every green action button are white in both themes.
4. Confirm Media Deck shows legible `-10s`, Stop, and `+10s` controls with no square/tofu glyphs.
5. Seek while paused and while playing; the timeline must stay at the chosen position.

## B. Media vocal synchronization

1. Select headphones as Media Monitor, enable Monitor and Send, and start the engine.
2. Play a beat and record the GrassiBoard virtual microphone in a target app.
3. Sing or clap on the monitored beat in High quality, Balanced, and Low latency.
4. Confirm the recorded voice and beat remain acceptably aligned in each mode. Settings must show a non-zero Media Vocal Sync that follows the active route/quality.
5. Repeat Play/Pause/seek/±10 and a live quality change; no stale Media or progressive drift is allowed.
6. Confirm the physical microphone is never audible in the independent Media monitor.

## C. Microphone disappearance and recovery

1. Start on microphone A with distinctive Voice FX, Mixer values, Pads, Media settings, and user Mute state.
2. Disconnect/disable microphone A while the engine is live and microphone B is available.
3. Confirm GrassiBoard stays open, switches automatically to B, resumes the virtual route, and preserves every Voice/Mixer/Pad/Media setting.
4. Disconnect every physical microphone. Confirm the process does not crash and the virtual microphone receives no microphone signal.
5. Reconnect one physical microphone. Confirm automatic retry restores the route without changing the user's stored Mute choice.
6. Confirm a virtual cable is never selected as the recovery microphone.

## D. Installer and uninstall

1. On a machine with a compatible cable, run Setup and verify the poster, destination picker, optional Desktop shortcut, progress, Finish, and Open GrassiBoard states.
2. Confirm Start Menu/Desktop shortcuts, the application icon, launch, and Windows Apps & features entry.
3. Repeat without a compatible cable. Installation must complete and the final screen must show the official VB-CABLE download link and instruction; it must not install a driver.
4. Install over the same destination and confirm an update succeeds without deleting unknown/user files.
5. Uninstall through Apps & features. Confirm manifest-owned program files and shortcuts are removed while `%APPDATA%\GrassiBoard`, `%LOCALAPPDATA%\GrassiBoard`, original audio files, and the external cable remain.

## E. Long-duration acceptance

1. Run microphone processing plus looping/overlapping Pads and streamed Media for at least two hours.
2. Exercise theme/page changes, Tray, global hotkeys, seek, quality and preset changes, Mute, and Stop All.
3. Record dropout counters, Media underruns, memory growth, CPU behavior, and any `%LOCALAPPDATA%\GrassiBoard\CrashReports\latest.txt`.
4. Restart and verify Profiles, Pads, presets, hotkeys, routing, Media preferences, and app behavior persist without autoplay.

## Acceptance report

```text
Windows version:
Installer path/custom path:
Cable present/missing flow:
UI labels/colors/icons:
Media sync High/Balanced/Low:
Mic A -> Mic B recovery:
No-microphone mute/reconnect recovery:
Two-hour stability/dropouts:
Uninstall/preserved settings:
Result: PASS / FAIL
Crash report or diagnostics (if any):
```
