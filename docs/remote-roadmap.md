# GrassiBoard Remote — v1.1 → v1.3 Master Development Roadmap

> **Project:** GrassiBoard  
> **Baseline reviewed:** v1.0.1 stable  
> **Roadmap scope:** v1.1 Remote Control → v1.2 Remote Monitor → v1.3 Full-Duplex Remote Audio  
> **Primary target:** Windows 10/11 x64 desktop + Android phone browser on the same LAN/Wi-Fi  
> **Development model:** Codex edits/builds/tests the source; the user does **not** edit code and only performs real-device manual testing and reports results.

---

# 0. Why this document exists

This file is the authoritative implementation roadmap for turning GrassiBoard into a remote live-audio console.

The final experience should allow the user to leave the Windows computer in another room and operate a live session from an Android phone:

```text
Android phone
├─ GrassiBoard Web Remote
│  ├─ Sound Pads
│  ├─ Voice FX / Presets
│  ├─ Compact Mixer
│  ├─ Media Deck
│  ├─ Engine / Mic controls
│  └─ Remote Monitor controls
│
├─ Remote monitor audio ← GrassiBoard / Windows
│
└─ Phone microphone → GrassiBoard → Voice DSP → VB-CABLE

Windows PC
├─ GrassiBoard
├─ Chrome / X Spaces / Discord / Telegram / recorder
└─ VB-CABLE remains the final virtual microphone solution
```

The remote feature is intentionally split into three versions so that a failure in realtime networking or remote audio never destabilizes the already accepted v1.0.1 microphone/DSP/Soundboard route.

---

# 1. Existing v1.0.1 baseline — preserve it

The current repository has a stable v1.0.1 baseline. Before changing anything, Codex must read at minimum:

```text
docs/current-status.md
docs/architecture.md
docs/audio-pipeline.md
docs/ui-architecture.md
docs/hotkeys-and-tray.md
docs/media-deck.md
docs/profiles-and-presets.md
docs/test-plan.md
src/GrassiBoard.App/ViewModels/MainViewModel.cs
src/GrassiBoard.App/Services/NativeAudioEngine.cs
src/GrassiBoard.App/Services/MediaDeckService.cs
src/GrassiBoard.App/Services/GlobalHotkeyService.cs
src/GrassiBoard.App/Services/ProfileStore.cs
src/GrassiBoard.AudioEngine/include/grassiboard/audio_engine.h
src/GrassiBoard.AudioEngine/src/wasapi_engine.cpp
src/GrassiBoard.AudioEngine/src/audio_engine.cpp
.github/workflows/build.yml
```

Current important repository facts:

```text
Desktop UI: WPF
Target framework: net8.0-windows
Native engine: C++ / WASAPI
Architecture: x64
Internal processing: 48 kHz, 32-bit float
Native ABI baseline: 8
Virtual microphone output: external VB-CABLE route
Profiles / user Voice+Mixer presets: implemented
Global hotkeys / Tray: implemented
Soundboard: implemented and accepted
Local Media Deck: implemented and accepted
Media headphone monitor: independent from microphone monitoring
GitHub Actions: self-contained win-x64 app + installer + tests
```

The v1.0.1 audio route is a regression baseline. Do not replace VB-CABLE. Do not revive the abandoned custom virtual driver as part of this roadmap.

---

# 2. Non-negotiable project rules

## 2.1 User role

The user does **not** modify source code.

Codex is responsible for:

- source edits;
- project files;
- dependency changes;
- migrations;
- tests;
- CI changes;
- documentation;
- packaging changes;
- version changes;
- fixing regressions.

The user is responsible for:

- downloading the GitHub Actions artifact/installer;
- installing/running it;
- allowing required local-network/firewall access when instructed;
- pairing the Android phone;
- performing real Windows/Android/X Spaces/Discord tests;
- reporting exact behavior, screenshots, logs, and subjective latency;
- explicitly approving or rejecting a stage.

Never ask the user to manually edit C#, C++, XAML, JSON, project files, registry values, certificates, or config files when the application/installer/script can perform the operation.

---

## 2.2 Manual acceptance is authoritative

CI success does **not** equal user acceptance.

A version is accepted only when the user explicitly says it works and gives final approval.

Examples of explicit approval:

```text
Approved
تایید شد
همه چیز درست کار می‌کند، برو مرحله بعد
این نسخه نهایی این مرحله است
```

Do not infer acceptance from silence, a successful build, or a partial test.

---

## 2.3 Patch-version policy

If a major remote stage fails manual testing, remain on that stage and produce patch versions.

Example:

```text
v1.1.0  first Remote Control test build
v1.1.1  first reported fix
v1.1.2  second reported fix
...
USER APPROVES v1.1.x
v1.2.0  only now begin Remote Monitor
```

Same rule for v1.2 and v1.3.

Do not begin the next stage automatically.

---

## 2.4 Status file is mandatory

The repository must contain:

```text
docs/remote-development-status.md
```

This file is the authoritative state handoff between sessions, chats, Codex runs, and future ChatGPT Projects.

After every implementation iteration, update its **current work / awaiting test / known issues** fields.

After and only after explicit user approval, mark the corresponding release as **USER ACCEPTED** and record:

- accepted version;
- date;
- commit SHA if available;
- GitHub Actions run if available;
- important test notes;
- regressions checked;
- next permitted stage.

---

## 2.5 Commit-message rule

After the user explicitly approves a release stage, provide the user with a suitable exact commit message.

Suggested semantic shape:

```text
feat(remote): complete v1.1 realtime web control
feat(remote-monitor): complete v1.2 LAN monitor audio
feat(remote-audio): complete v1.3 full-duplex phone audio
```

If implementation commits already exist and only the acceptance/status record remains to be committed, use a documentation-focused message instead:

```text
docs(remote): mark v1.1.x as user accepted
```

The assistant/Codex should choose the final wording based on what is actually uncommitted at that time.

---

# 3. Overall target architecture

The long-term architecture is:

```text
                         SAME LAN / WI-FI

 ┌─────────────────────────────────────────────────────────────┐
 │                    ANDROID PHONE                            │
 │                                                             │
 │  Web Remote UI                                              │
 │  ├─ Pads / Presets / Voice / Mixer / Media                 │
 │  ├─ Engine / Mute / Stop All                               │
 │  ├─ Realtime state                                         │
 │  ├─ WebRTC Remote Monitor      ← v1.2                      │
 │  └─ WebRTC Phone Mic           → v1.3                      │
 └──────────────┬───────────────────────────────▲──────────────┘
                │ control/state                 │ monitor audio
                │ WebSocket                     │ WebRTC
                │                               │
                ▼                               │
 ┌─────────────────────────────────────────────────────────────┐
 │                    GRASSIBOARD WINDOWS                      │
 │                                                             │
 │  Embedded Remote Server                                    │
 │      ↓                                                      │
 │  Remote command/state layer                                │
 │      ↓                                                      │
 │  Existing application/audio services                       │
 │                                                             │
 │  Input source                                              │
 │  ├─ Physical WASAPI microphone                             │
 │  └─ Remote phone microphone (v1.3)                         │
 │          ↓                                                  │
 │      Voice DSP                                             │
 │          ↓                                                  │
 │       Mixer ← Soundboard ← Remote pad commands             │
 │          ↑                                                  │
 │       Media Deck                                            │
 │          ↓                                                  │
 │      Program Mix → VB-CABLE → target application           │
 │                                                             │
 │  External/System loopback ─┐                                │
 │  Soundboard tap ───────────┼→ Remote Monitor Mix → WebRTC  │
 │  Media tap ────────────────┤                                │
 │  Optional processed Mic ───┘                                │
 └─────────────────────────────────────────────────────────────┘
```

The critical design principle is separation between:

```text
PROGRAM MIX
→ what X Spaces / Discord / Telegram / recorder hears

REMOTE MONITOR MIX
→ what the user hears on the phone
```

These are independent buses.

---

# 4. Architecture preparation — do this without a rewrite

`MainViewModel.cs` is currently large and owns many commands/state bindings. Do **not** perform a risky full MVVM rewrite as part of the Remote feature.

Instead, incrementally create a reusable command/state boundary so Remote, desktop UI, Hotkeys, Tray, and future MIDI/Stream Deck controllers can call the same application operations.

Recommended conceptual API:

```text
IGrassiBoardController / AppCommandService

EngineStartAsync()
EngineStopAsync()
StopAllAsync()

SetMicrophoneMute(bool)
ToggleMicrophoneMute()
SetVoiceFxEnabled(bool)
SetPitch(double)
SetFinePitch(double)
SetFormant(double)
ApplyUserPresetAsync(Guid)

PlayPadAsync(Guid)
StopPad(Guid)

SetMicGain(double)
SetSoundboardGain(double)
SetMasterGain(double)

MediaPlayPause()
MediaStop()
MediaSeek(double)
MediaSkip(double)
SetMediaVolume(double)
SetMediaMonitor(bool)
SetMediaSend(bool)
```

Desktop commands, global hotkeys, Tray commands, and Remote commands should converge on this layer.

Do not duplicate audio-engine logic in the web server.

---

# 5. Server-authoritative state model

The Windows application is always the source of truth.

The phone must **not** own a separate persistent copy of Pads, Presets, Voice settings, Mixer state, or Media state.

Example:

```text
Windows: user adds Pad "Airhorn"
→ state revision increments
→ PadAdded / state delta pushed to phone
→ Airhorn appears immediately
```

Example in the opposite direction:

```text
Phone: Pitch = +4
→ Remote command
→ Windows validates and applies Pitch
→ Windows state becomes +4
→ desktop slider updates
→ authoritative +4 event/snapshot returns to phone
```

Never implement optimistic phone-only state that can drift away from Windows.

---

# 6. Remote protocol contract

Create a versioned protocol independent from WPF models and native ABI.

Do not serialize `MainViewModel`, `ProfileModel`, or arbitrary internal objects directly.

Recommended envelope:

```json
{
  "protocolVersion": 1,
  "type": "command|event|snapshot|ack|error",
  "messageId": "uuid",
  "revision": 1234,
  "payload": {}
}
```

Core rules:

- every client command has a unique `messageId`;
- server validates all parameters;
- server sends ACK or structured error;
- server owns monotonically increasing state revision;
- reconnect always begins with a full state snapshot;
- deltas/events follow after the snapshot;
- unknown protocol versions fail gracefully;
- protocol DTOs contain no arbitrary file-system operations;
- do not expose full local file paths to the phone unless strictly necessary.

Possible message families:

```text
connection.hello
connection.auth
state.snapshot

engine.start
engine.stop
engine.changed

mic.mute.set
mic.mute.changed

voice.fx.set
voice.pitch.set
voice.finePitch.set
voice.formant.set
voice.changed

preset.apply
preset.applied

pad.play
pad.stop
pad.added
pad.updated
pad.removed
pad.playbackChanged

mixer.gain.set
mixer.changed

media.playPause
media.stop
media.seek
media.changed

meter.update
error
ack
```

---

# 7. Update frequency / realtime UI rules

Do not send audio-rate data over the control connection.

Suggested categories:

```text
Immediate event:
- Pad added/removed/edited
- Pad play/stop
- Preset applied
- Voice FX toggle
- Pitch/Formant change
- Mute
- Engine state
- Media play/pause/stop

Throttled realtime telemetry:
- meters: ~10–20 Hz
- Media position: ~4–10 Hz
- network stats: ~1 Hz
```

If the phone changes a slider continuously, coalesce updates when necessary rather than queueing hundreds of stale values.

---

# 8. Web client technology

Preferred implementation:

```text
Vue 3
Vite
TypeScript
Static SPA only
No Nuxt
No Node.js runtime on the user's PC
```

The Node toolchain exists only in CI/build time.

Recommended repository location:

```text
src/GrassiBoard.RemoteWeb/
```

Build output should be static assets copied/embedded into the Windows app package and served by the embedded server.

Pin package versions and commit the lockfile.

GitHub Actions must build the web client from a clean runner before publishing the WPF app.

The installer/portable package must contain the compiled web assets; the user must not install Node/npm.

---

# 9. Embedded Windows web server

Preferred host:

```text
ASP.NET Core / Kestrel hosted inside GrassiBoard.App
```

The WPF application remains the main process.

Recommended managed components:

```text
Services/Remote/
├─ RemoteServerService.cs
├─ RemoteCommandDispatcher.cs
├─ RemoteStatePublisher.cs
├─ RemoteClientRegistry.cs
├─ RemotePairingService.cs
├─ RemoteSettingsStore.cs
├─ RemoteProtocol.cs
└─ RemoteDiagnostics.cs
```

Names may change if a cleaner existing convention is available.

Do not let Kestrel callbacks directly manipulate WPF controls.

UI-bound changes must cross the correct dispatcher/control layer.

---

# 10. Local-network-only principle

The first three Remote versions are designed for the **same trusted LAN/Wi-Fi**, not Internet remote access.

No cloud account is required.

No external signaling server should be required for basic LAN use.

No remote port forwarding should be configured automatically.

Remote Server should be:

```text
OFF by default on first install
explicitly enabled by the user
bound only for LAN use
clearly show listening IP/port
warn/refuse unsafe behavior on untrusted/public network profiles where practical
```

Do not silently expose a control server to the Internet.

---

# 11. Pairing and security model

Remote control must not be unauthenticated even on a LAN.

## First-time pairing

Desktop Settings should show:

```text
REMOTE CONTROL

Status: ON
Address: 192.168.x.x:<port>

[ QR CODE ]

Pairing code: 123456
Expires in: 01:42

Paired devices
• Mehdi's Phone   Connected
  [ Revoke ]
```

Preferred flow:

1. User enables Remote Control.
2. GrassiBoard creates a cryptographically random one-time pairing secret.
3. QR contains the local URL plus one-time pairing data.
4. Phone opens the web client.
5. Client exchanges one-time secret for a long-lived random client credential.
6. One-time pairing secret expires quickly and cannot be reused.
7. Future reconnect uses the paired client credential.
8. Desktop can revoke any device.

Do not store reusable secrets in logs.

Prefer storing server-side client-token hashes rather than plaintext tokens.

Use fixed-time comparisons where applicable.

Pairing/security settings are global application data, not duplicated into every Profile.

---

# 12. Firewall / unreachable-device behavior

Listening on the LAN may be blocked by Windows Firewall.

The app must detect and explain this instead of silently showing a QR that cannot work.

Preferred UX:

```text
Remote server is running, but this PC may not be reachable from your phone.
Make sure both devices are on the same Wi-Fi and allow GrassiBoard on Private networks.
```

Do not require the user to hand-author firewall rules.

If an elevation-required helper is eventually needed, provide it through the installer/app with explicit consent.

---

# 13. v1.1.0 — GrassiBoard Remote Control

## Goal

Create a polished realtime web controller that mirrors a deliberately limited subset of the active Windows GrassiBoard state.

**No phone microphone streaming.**  
**No remote monitor audio.**  
**No WebRTC requirement yet.**

The goal is to validate server architecture, pairing, realtime state synchronization, and mobile UX before touching audio networking.

---

# 14. v1.1 Remote UI scope

The phone is a controller, not a full clone of the desktop application.

Required mobile sections:

```text
Board
Voice
Mixer
Media
Connection / Remote status
```

Do not expose full desktop Routing, Diagnostics, installer controls, Profile deletion, or advanced maintenance UI.

---

# 15. v1.1 — Board page

The phone must show the same active Profile Sound Pads as Windows.

For each pad expose at least:

```text
Title
Ready / missing / error state
Playing state
Loop/active indication where useful
Tap to play
Stop action where needed
```

Required synchronization:

```text
Windows Add Pad    → phone receives it immediately
Windows Delete Pad → phone removes it immediately
Windows Edit Pad   → phone updates title/state immediately
Windows Play Pad   → phone shows playing state
Phone Play Pad     → Windows plays existing Pad
Phone Stop Pad     → Windows stops existing Pad
```

The phone does not upload or edit audio files in v1.1.

Pad file-management remains desktop-only.

---

# 16. v1.1 — Voice page

Expose the live controls useful during a session:

```text
Voice FX ON/OFF
Pitch
Fine Pitch
Formant
Preserve vocal character if appropriate
User Presets
Reset Voice if useful
```

Preset application must use the existing smooth transition behavior.

Do not create a second preset implementation for Remote.

Desktop and phone must always converge on the same values.

---

# 17. v1.1 — Compact Mixer

Expose only live-friendly mixer controls.

Recommended initial controls:

```text
Mic Gain
Soundboard Gain
Master Gain
Mic Mute
```

Optionally expose Media volume after Media controls are stable.

Do not initially expose every Compressor/Gate/Limiter attack/threshold parameter on the phone.

The desktop remains the advanced editor.

---

# 18. v1.1 — Media Deck

Expose:

```text
Loaded filename/title
Play/Pause
Stop
-10 sec
+10 sec
Position / duration
Timeline seek
Media volume
Monitor enabled
Send to Virtual Mic enabled
```

The phone does not browse Windows files in v1.1.

Loading/replacing a local media file remains a desktop action.

If Windows loads a new file, the remote UI updates immediately.

---

# 19. v1.1 — Engine lifecycle is required

Because existing **Stop All** stops the audio engine, the Remote must not leave the user stranded in another room.

Expose:

```text
Engine state: LIVE / READY / OFFLINE / ERROR
Start Engine
Stop Engine if appropriate
Stop All
```

If the user presses Remote Stop All and the engine stops, they must be able to start it again from the phone.

Do not require a trip back to the laptop.

---

# 20. v1.1 — Remote top status

Recommended compact status:

```text
● LIVE
PC: GrassiBoard
Connection: Good
Mic: -18 dB
Master: -9 dB

[MUTE MIC] [STOP ALL]
```

Stop All must be visually distinct and difficult to hit accidentally, while remaining usable as an emergency action.

A short confirm/hold behavior is acceptable if it does not make emergency stop frustrating.

---

# 21. v1.1 — Responsive phone UX

Portrait mode:

```text
Board
Voice
Mixer
Media
```

Landscape mode should behave like a compact hardware deck:

```text
┌───────────────────────────────────────────┐
│ [PAD] [PAD] [PAD] [PAD] [PAD]            │
│ [PAD] [PAD] [PAD] [PAD] [PAD]            │
│                                           │
│ Mic ●      Preset: Deep Radio      ● LIVE │
└───────────────────────────────────────────┘
```

Requirements:

- large touch targets;
- no hover-only interactions;
- readable in low light;
- dark visual language matching GrassiBoard;
- no desktop-sized forms squeezed onto mobile;
- no accidental browser horizontal scrolling;
- important controls remain reachable with one hand.

---

# 22. v1.1 — Haptic feedback

Use browser vibration/haptic support as progressive enhancement when available.

Examples:

```text
Pad tap      → short click
Preset apply → soft tick
Mute         → short distinct feedback
Stop All     → stronger feedback
```

The Remote must remain fully functional if vibration is unavailable or denied.

---

# 23. v1.1 — reconnect behavior

Phone browser backgrounding, Wi-Fi roaming, screen lock, and temporary network loss are normal.

Implement automatic reconnect with backoff.

On reconnect:

```text
1. authenticate paired client
2. discard stale local assumptions
3. request/receive full authoritative snapshot
4. restore current UI
5. resume event stream
```

Never replay stale pad or destructive commands after reconnect.

---

# 24. v1.1 — settings persistence

Global remote settings should include at least:

```text
Remote enabled
Port
Paired clients
Pairing/security metadata
Optional device display names
```

Do not put reusable pairing tokens inside Profile JSON.

The active Profile determines the remotely visible Pads/Presets/Voice/Mixer state.

---

# 25. v1.1 — tests that Codex can automate

Add automated tests for:

```text
Protocol serialization/deserialization
Protocol version rejection
Command validation
Unauthorized command rejection
Pairing expiration
Token revoke
State snapshot construction
Pad delta propagation
Preset command routing
Slider clamping
Reconnect snapshot
Remote static assets included in publish
No local file paths leaked unintentionally
```

Add source/smoke tests to ensure remote server code cannot call the audio callback directly.

---

# 26. v1.1 — manual test matrix

The user should test on real Windows + Android:

```text
A. Pairing
[ ] Enable Remote
[ ] QR opens on Android
[ ] Pair succeeds
[ ] Reload browser reconnects
[ ] Revoke device prevents reconnect

B. Soundboard
[ ] Existing Pads appear
[ ] Tap Pad plays immediately on Windows output path
[ ] Windows Add Pad appears on phone without refresh
[ ] Windows Edit Pad updates phone
[ ] Windows Delete Pad removes phone item
[ ] Playing state stays synchronized

C. Voice
[ ] Voice FX toggle
[ ] Pitch
[ ] Fine Pitch
[ ] Formant
[ ] Preset apply
[ ] Desktop values update immediately
[ ] Target app hears changes live

D. Mixer
[ ] Mic Gain
[ ] Soundboard Gain
[ ] Master Gain
[ ] Mute

E. Media
[ ] Play/Pause
[ ] Stop
[ ] ±10 sec
[ ] Timeline
[ ] Volume
[ ] Monitor toggle
[ ] Send toggle

F. Engine
[ ] Engine status accurate
[ ] Stop All works
[ ] Start Engine from phone works afterward

G. Resilience
[ ] Turn phone Wi-Fi off/on
[ ] Lock/unlock phone
[ ] Background/foreground browser
[ ] No duplicate command fires after reconnect

H. Regression
[ ] Normal desktop Soundboard still works
[ ] Hotkeys still work
[ ] Tray still works
[ ] Voice processing unchanged
[ ] Media Deck unchanged
[ ] VB-CABLE target app route unchanged
```

---

# 27. v1.1 acceptance criteria

v1.1 is complete only when:

```text
[ ] Remote can be enabled/disabled safely
[ ] Pairing works
[ ] Unpaired clients cannot control GrassiBoard
[ ] Paired device reconnects
[ ] Pads are live-synchronized both directions
[ ] Voice controls are live-synchronized
[ ] User presets apply from phone
[ ] Compact Mixer works
[ ] Media transport works
[ ] Mic Mute works
[ ] Stop All works
[ ] Engine can be restarted remotely
[ ] Mobile portrait/landscape UI is usable
[ ] No audio regression is introduced
[ ] GitHub Actions is green
[ ] Installer/portable package contains Remote assets
[ ] User explicitly approves the real-device test
```

After approval:

- update `docs/remote-development-status.md`;
- update `docs/current-status.md` and `CHANGELOG.md`;
- provide the user with the appropriate commit message;
- only then unlock v1.2 work.

---

# 28. v1.2.0 — Remote Monitor

## Goal

Make the phone an audio-monitor endpoint.

The user should be able to be physically away from the PC and hear the live session on Android while continuing to control GrassiBoard.

**v1.2 is audio from PC → phone only.**

The phone microphone can still use an external solution such as AudioRelay during this stage.

---

# 29. v1.2 — core concept: separate Remote Monitor Bus

Do not simply send the Program/Master mix to the phone.

The Program mix contains the processed microphone; hearing your own voice after network latency is distracting and can create feedback.

Create an independent monitor mix:

```text
PROGRAM MIX → VB-CABLE → audience

REMOTE MONITOR MIX → WebRTC → phone → user
```

Default Remote Monitor sources:

```text
External / Windows / Chrome audio    ON
Soundboard                           ON
Media Deck                           ON
Processed Microphone                 OFF
```

Each source should have independent remote-monitor gain where practical.

---

# 30. v1.2 — target listening scenario

```text
Laptop in another room:
Chrome runs X Space
Chrome microphone = CABLE Output

User in bedroom:
Android phone opens GrassiBoard Remote
User hears X Space from phone/earbuds
User taps Pads/Presets on phone
Pad audio reaches Space
Remote Monitor also lets user hear the Pad
```

This is the primary acceptance scenario.

---

# 31. v1.2 — external application/system audio capture

Support two conceptual capture modes.

## Mode A — process-specific capture (best case)

When the operating system supports it, capture a selected process tree such as Chrome.

Example:

```text
Remote Monitor External Source:
Chrome.exe
```

Do not assume this is available on all supported Windows 10 systems.

Runtime capability detection is mandatory.

If unsupported, hide/disable it with a clear explanation and use Mode B.

## Mode B — selected Windows output loopback (required fallback)

Capture the system mix playing on a selected render endpoint using WASAPI loopback.

Example:

```text
Remote Monitor External Source:
Headphones (USB Audio Device)
```

This is the primary compatibility route for ordinary Windows 10 machines.

The UI should describe this honestly as system/output audio, not “Chrome only.”

---

# 32. v1.2 — implementation preference for system loopback

The project already uses NAudio 2.3.0 for managed media monitoring.

Prefer using an isolated managed loopback worker where reliable, rather than rewriting the accepted native WASAPI microphone engine.

Requirements:

- capture worker is independent from WPF UI thread;
- no blocking work in native realtime callback;
- convert/resample to the Remote Monitor internal format outside realtime render callback;
- count dropouts/overflow;
- survive endpoint changes gracefully.

---

# 33. v1.2 — internal GrassiBoard monitor tap

Soundboard and Media do not necessarily play to the same Windows endpoint as Chrome, so system loopback alone is insufficient.

Create a realtime-safe internal monitor tap for GrassiBoard sources.

Recommended native concept:

```text
Soundboard branch ──────┐
Media branch ───────────┼→ Internal Remote Monitor Mix → SPSC ring
Optional processed Mic ─┘
```

Managed `RemoteMonitorService` can then mix:

```text
External/System loopback
+
Internal Remote Monitor ring
→ WebRTC audio sender
```

Any native ABI change must be versioned.

Suggested ABI progression if needed:

```text
v1.0.1 baseline: ABI 8
v1.1: keep ABI 8 if no native change is needed
v1.2: ABI 9 if Remote Monitor tap APIs are added
v1.3: ABI 10 if Remote Mic input APIs are added
```

This is guidance, not a requirement to bump unnecessarily.

---

# 34. v1.2 — avoid duplicate Media monitoring

Potential problem:

```text
Media Deck → physical headphone monitor
physical headphone endpoint → System Loopback
+
Media Deck → direct Remote Monitor tap
=
Media heard twice on phone
```

The implementation must prevent this.

At minimum detect:

```text
Captured system endpoint == Media local monitor endpoint
AND Media local monitor enabled
```

Then choose only one Media contribution for Remote Monitor.

Preferred UI/state explanation:

```text
Media: included through Windows Output
```

or automatically exclude the duplicate direct source.

Never knowingly send doubled/comb-filtered Media to the phone.

---

# 35. v1.2 — Remote Monitor source mixer

Phone UI should expose a simple monitor mixer:

```text
REMOTE MONITOR

Windows / Space   90%
Soundboard        70%
Media             70%
My Voice           0%   ← default

Monitor Master    85%
```

This monitor mix must not modify the Program mix sent to VB-CABLE.

Changing `Soundboard 70%` here changes only what the user hears remotely.

---

# 36. v1.2 — microphone monitoring rule

Processed microphone contribution is OFF by default.

If enabled, make it visually obvious:

```text
My Voice Monitor: 10%
```

Warn that self-monitoring over Wi-Fi has noticeable latency.

Do not silently enable it when Remote Monitor starts.

---

# 37. v1.2 — WebRTC downlink

Use WebRTC for realtime monitor audio.

Control/state remains on the existing authenticated WebSocket connection.

Use the WebSocket connection for WebRTC signaling as well:

```text
SDP offer/answer
ICE candidates
session start/stop
```

No separate cloud signaling service is required for same-LAN operation.

Default topology:

```text
Windows GrassiBoard WebRTC peer
→ audio track
→ Android browser RTCPeerConnection
```

Preferred audio format:

```text
48 kHz
Opus
stereo where music/Media benefits
```

Do not send raw PCM over the control WebSocket.

---

# 38. v1.2 — WebRTC implementation spike is mandatory

Before committing a WebRTC library to the product, Codex must perform a small isolated spike and document:

```text
Library/package name
Exact pinned version
Active maintenance status
Windows x64 support
Browser interoperability
Opus support
ICE/SDP support
License compatibility
Native/runtime dependencies
Self-contained publish behavior
GitHub Actions build behavior
Installer size impact
```

Do not use deprecated Microsoft MixedReality-WebRTC merely because old examples exist.

Do not choose a library with unacceptable licensing terms without explicitly stopping and reporting the issue.

Do not merge the spike until a local synthetic audio track can be received by Android Chrome through the same-LAN architecture.

---

# 39. v1.2 — ICE/network scope

v1.2 is LAN-first.

Do not require external STUN/TURN servers for normal same-Wi-Fi operation.

Prefer host/local candidates when the chosen WebRTC stack permits.

Do not silently send network metadata to third-party infrastructure.

Internet remote access is outside this roadmap.

---

# 40. v1.2 — browser playback UX

Mobile browsers may require a user gesture before audible playback.

Remote UI should provide an explicit action such as:

```text
[ Start Remote Monitor ]
```

After start:

```text
🎧 Monitor: Connected
Latency: -- ms
Packets lost: --
```

If the browser suspends playback while backgrounded/screen locked, show an accurate reconnect/resume state rather than pretending it is still audible.

---

# 41. v1.2 — earbuds / echo guidance

For the X Spaces use case, recommend wired or Bluetooth earbuds/headphones connected to the phone.

Reason:

```text
Space audio → phone speaker → phone microphone → GrassiBoard → Space
```

can cause acoustic echo when an external phone-mic bridge such as AudioRelay is also in use.

Remote Monitor itself should remain usable with the phone speaker, but the UI/docs should clearly recommend headphones for simultaneous phone-mic use.

---

# 42. v1.2 — diagnostics

Add Remote Monitor diagnostics, preferably in desktop Settings/Diagnostics:

```text
Remote monitor state
Capture source type
Capture endpoint/process
Capture sample rate
Internal monitor ring fill
Monitor underruns/overruns
WebRTC connection state
ICE state
Audio packets sent
Packet loss if available
RTT if available
Estimated/jitter statistics if available
Remote clients listening
```

Do not flood ordinary Board UI with technical counters.

---

# 43. v1.2 — latency policy

The purpose is live conversational monitoring, but network/browser jitter buffering means remote monitor latency will never behave like a direct wired audio monitor.

Measure rather than promise.

Target on good LAN/Wi-Fi:

```text
subjectively usable for live conversation
no multi-second buffering
no periodic stutter
stable enough for long X Spaces sessions
```

Record measured/observed latency in `docs/remote-development-status.md`.

Do not sacrifice audio continuity just to display a smaller latency number.

---

# 44. v1.2 — manual test matrix

```text
A. Basic monitor
[ ] Start Remote Monitor on Android
[ ] Windows audio heard on phone
[ ] Stop monitor works
[ ] reconnect works

B. X Spaces / Chrome scenario
[ ] X Space runs in Windows Chrome
[ ] Chrome uses CABLE Output as microphone
[ ] Space participants heard on Android
[ ] Sound Pad triggered on Android reaches Space
[ ] Same Sound Pad is heard by user on Android
[ ] Media Deck reaches Space and Android as configured

C. Source modes
[ ] System Output loopback works
[ ] Process capture shown only when OS supports it
[ ] Unsupported process capture has clear fallback

D. Monitor mix
[ ] Windows gain changes only phone monitor
[ ] Soundboard gain changes only phone monitor
[ ] Media gain changes only phone monitor
[ ] My Voice default is OFF
[ ] My Voice can be enabled deliberately
[ ] Program/VB-CABLE mix is not altered

E. Duplicate prevention
[ ] Media is not doubled when local monitor shares loopback endpoint

F. Resilience
[ ] phone screen lock/unlock
[ ] Wi-Fi off/on
[ ] Chrome playback stop/start
[ ] Windows endpoint change
[ ] Remote Control remains usable if monitor audio fails

G. Long-duration
[ ] 30–60 minute monitor session
[ ] no runaway memory
[ ] no growing audio delay
[ ] no regular stutter
[ ] no GrassiBoard engine dropout
```

---

# 45. v1.2 acceptance criteria

```text
[ ] v1.1 Remote remains fully working
[ ] Remote Monitor is an independent bus
[ ] System/output loopback works on target Windows 10
[ ] Process-specific capture is runtime-gated, not assumed
[ ] Soundboard can be heard remotely
[ ] Media can be heard remotely
[ ] No duplicate Media path
[ ] Mic monitor is OFF by default
[ ] Monitor gains do not affect audience/program mix
[ ] WebRTC reconnects gracefully
[ ] Control still works if WebRTC audio fails
[ ] Long monitor session is stable
[ ] Existing microphone DSP/VB-CABLE route has no regression
[ ] GitHub Actions/installer are green
[ ] User explicitly approves the real-device test
```

After approval, update status/current-status/changelog and provide the stage commit message before starting v1.3.

---

# 46. Phone microphone before v1.3 — supported external bridge

Until integrated Remote Mic is complete, maintain compatibility with an external Android-to-Windows microphone bridge such as AudioRelay.

Conceptually:

```text
Phone Mic
→ external bridge
→ Windows virtual recording endpoint
→ GrassiBoard Input Microphone
→ Voice DSP
→ Mixer
→ VB-CABLE
```

GrassiBoard does not need special audio-engine logic for this route; it sees the bridge as another Windows input device.

Document this workflow, but do not make v1.1/v1.2 depend on one vendor.

---

# 47. v1.3.0 — Full-Duplex Remote Audio

## Goal

Make the phone both:

```text
REMOTE CONTROL
+
REMOTE MONITOR
+
REMOTE MICROPHONE
```

At this point the full scenario becomes:

```text
PHONE
├─ Mic → WebRTC → GrassiBoard
├─ Monitor ← WebRTC ← GrassiBoard
└─ Controls ↔ WebSocket ↔ GrassiBoard

GrassiBoard
→ Voice FX / Pitch / Formant / Mixer
→ VB-CABLE
→ X Spaces / Discord / Telegram / Recorder
```

No custom Windows virtual audio driver is required.

---

# 48. Critical v1.3 secure-context gate

Browser microphone capture uses `navigator.mediaDevices.getUserMedia()` and must be treated as a secure-context feature.

A plain LAN page such as:

```text
http://192.168.x.x:<port>
```

must **not** be assumed to provide browser microphone access.

Before implementing the integrated browser microphone, Codex must complete and document a secure-origin onboarding spike.

Acceptable outcomes:

## Preferred Path A — clean HTTPS LAN onboarding

Provide an HTTPS origin that Android Chrome accepts as trustworthy without recurring scary warnings.

Requirements:

- certificate lifecycle handled by GrassiBoard tooling;
- user guided through one-time onboarding if trust installation is unavoidable;
- no private key committed to repository;
- no public cloud dependency for daily LAN use;
- paired client remains authenticated;
- certificate rotation/recovery documented.

## Fallback Path B — minimal Android companion shell

If trustworthy HTTPS onboarding is too fragile, do **not** bypass browser security.

Instead build a minimal Android companion shell that:

- reuses the same web UI/control concepts;
- handles microphone permission natively;
- participates in WebRTC/full-duplex audio;
- remains only a thin Remote client, not a second GrassiBoard audio engine.

The browser-based v1.1/v1.2 Remote must remain available either way.

Stop and report the spike result before choosing A or B.

---

# 49. v1.3 — phone microphone capture UX

When integrated mic is available:

```text
🎤 PHONE MICROPHONE

[ Enable Phone Mic ]

Input: Phone microphone
Echo cancellation: On
Noise suppression: Optional
Auto gain: Off by default

Status: Connected
```

Do not request microphone permission automatically on page load.

Use an explicit user gesture.

If permission is denied, Remote Control and Remote Monitor must continue working.

---

# 50. v1.3 — capture modes

Provide at least two conceptual microphone modes if browser/client capabilities permit:

## Communication mode

Suitable for phone speaker use:

```text
Echo cancellation: ON
Noise suppression: ON or user-selectable
Auto gain: OFF by default
```

## Clean / headset mode

Suitable when earbuds/headset isolate playback from microphone:

```text
Echo cancellation: optional/off
Noise suppression: optional/off
Auto gain: OFF
```

The purpose is to avoid browser DSP fighting GrassiBoard Voice FX while still having an echo-safe option.

Do not promise perfect AEC.

---

# 51. v1.3 — Remote Mic is a GrassiBoard input source, not a Windows device

Do **not** create another virtual microphone driver.

The phone should appear as an input source **inside GrassiBoard**, for example:

```text
INPUT MICROPHONE

Microphone (USB Headset)
Webcam Microphone
────────────────────────
Remote — Mehdi's Phone
```

This synthetic Remote source does not need to appear in Windows Sound Settings.

Only GrassiBoard needs to consume it.

---

# 52. v1.3 — input-source abstraction

Introduce a controlled abstraction around the current capture source.

Conceptually:

```text
IMicrophoneSource
├─ WasapiMicrophoneSource
└─ RemoteWebRtcMicrophoneSource
```

Do not rewrite the entire native engine solely to make the abstraction aesthetically perfect.

The final downstream DSP path remains the same:

```text
Selected input source
→ 48 kHz mono float
→ Pitch / Formant
→ Mixer dynamics
→ VB-CABLE
```

---

# 53. v1.3 — native Remote Mic ring

Preferred implementation if the chosen WebRTC stack receives/decodes audio in managed code:

```text
WebRTC decoded phone audio
→ managed jitter/resample stage
→ versioned native API
→ preallocated SPSC Remote Mic ring
→ existing Voice DSP pipeline
```

Possible ABI concept:

```text
gb_set_input_source_mode(...)
gb_remote_input_push(...)
gb_remote_input_reset(...)
gb_get_remote_input_statistics(...)
```

Exact API names are implementation details.

Hard realtime rules:

- no socket work in native render callback;
- no Opus decoding in render callback;
- no allocation in audio callback;
- no blocking locks;
- silence safely on starvation;
- count underrun/overflow;
- reset cleanly on reconnect.

---

# 54. v1.3 — jitter, clock drift, and resampling

Network microphone audio cannot be treated as a perfectly clocked local WASAPI device.

Provide a bounded jitter/ring strategy.

Handle:

```text
packet timing variation
packet loss
phone/PC clock drift
sample-rate mismatch
temporary Wi-Fi interruption
reconnect
```

The target consumed by the existing Voice DSP remains 48 kHz mono float.

Do not let jitter-buffer growth create ever-increasing microphone delay.

---

# 55. v1.3 — disconnect/fallback behavior

If the phone microphone disconnects:

```text
Remote Control remains alive if possible
Remote Monitor remains alive if possible
Soundboard remains usable
Media remains usable
VB-CABLE render remains alive
Mic branch fades/mutes safely
```

Do not stop the entire engine merely because the remote microphone drops.

Optionally allow the user to define a physical-mic fallback.

Example:

```text
Remote Mic lost
→ switch to USB Headset Mic
```

Automatic fallback must be explicit/configurable; never surprise the user by broadcasting from another microphone.

---

# 56. v1.3 — reconnect behavior

On phone mic reconnect:

1. re-authenticate existing paired device;
2. renegotiate/recover WebRTC as required;
3. refill minimum jitter target;
4. fade microphone in smoothly;
5. resume the same Voice/Mixer state;
6. do not reset Pitch/Formant/Preset;
7. do not replay Pads/Media commands.

---

# 57. v1.3 — Full Duplex and echo prevention

Default safe behavior:

```text
Remote Monitor My Voice contribution = OFF
Phone mic echoCancellation = ON in Communication mode
Earbuds/headphones strongly recommended
```

The remote monitor audio is the likely acoustic reference heard by the phone user.

Do not route the phone microphone back into the phone monitor by default.

If user deliberately enables self-monitoring, clearly show it and keep the gain conservative.

---

# 58. v1.3 — target X Spaces scenario

Final intended workflow:

```text
WINDOWS PC — another room

Chrome
├─ X Space
├─ microphone = CABLE Output
└─ audio output = normal Windows output

GrassiBoard
├─ input = Remote — Android Phone
├─ Voice FX / Preset active
├─ Pads active
├─ Media optional
├─ Program → VB-CABLE
└─ Remote Monitor → phone

ANDROID — user in bedroom

GrassiBoard Remote
├─ hears Space
├─ talks with phone/earbud mic
├─ triggers Pads
├─ changes Pitch/Formant
├─ applies Presets
├─ controls Media
├─ mutes mic
└─ can Start Engine after Stop All
```

No laptop interaction should be required during a healthy session.

---

# 59. v1.3 — phone audio status UI

Recommended:

```text
REMOTE AUDIO

🎤 Phone Mic        Connected
🎧 Monitor          Connected
Network             Good
RTT                 24 ms
Packet loss         0.2%

[MUTE PHONE MIC]
[STOP MONITOR]
```

Do not expose fake precision if the selected WebRTC stack does not provide a trustworthy metric.

---

# 60. v1.3 — manual test matrix

```text
A. Permission/security
[ ] Secure onboarding succeeds
[ ] Mic permission requested only after user action
[ ] Permission denial does not break Remote Control
[ ] paired authentication still required

B. Remote input
[ ] Phone appears as GrassiBoard input source
[ ] selecting it starts live audio
[ ] phone speech reaches GrassiBoard meters
[ ] Pitch applies
[ ] Formant applies
[ ] presets apply
[ ] Mixer applies
[ ] VB-CABLE target app hears processed phone mic

C. Full duplex
[ ] phone hears X Space
[ ] phone mic talks into X Space
[ ] Pads reach X Space and are heard on phone
[ ] Media reaches X Space and is heard on phone
[ ] no obvious feedback loop with earbuds

D. Voice changes
[ ] change Pitch from phone while talking
[ ] apply preset from phone while talking
[ ] transition is smooth
[ ] Windows UI remains synchronized

E. Disconnect
[ ] Wi-Fi interruption does not crash engine
[ ] mic branch becomes safe/silent
[ ] Soundboard/Media continue
[ ] reconnect restores mic
[ ] no stale audio replay

F. Long duration
[ ] 30–60 minute full-duplex session
[ ] no growing latency
[ ] no runaway memory
[ ] no increasing jitter-buffer delay
[ ] no regular dropouts

G. Target applications
[ ] X Spaces / Chrome
[ ] Discord if available
[ ] Telegram/recording if useful
[ ] existing physical microphone still works after switching back
```

---

# 61. v1.3 acceptance criteria

```text
[ ] v1.1 Remote Control remains stable
[ ] v1.2 Remote Monitor remains stable
[ ] secure mic onboarding is repeatable
[ ] integrated phone mic reaches GrassiBoard without custom Windows driver
[ ] phone mic uses existing Voice DSP path
[ ] phone mic reaches VB-CABLE target applications
[ ] Full Duplex works
[ ] Mic is not self-monitored by default
[ ] disconnect fails safely
[ ] reconnect works
[ ] physical microphone route still works
[ ] no new kernel driver is introduced
[ ] long-duration test is stable
[ ] GitHub Actions/installer are green
[ ] user explicitly approves real Windows + Android use
```

After approval, mark v1.3 as USER ACCEPTED in the status file and provide the final stage commit message.

---

# 62. Remote UI information architecture — final target

The full v1.3 phone UI should remain intentionally compact.

Suggested navigation:

```text
BOARD
VOICE
MIXER
MEDIA
REMOTE AUDIO
```

## Board

```text
Pads
Pad playing state
Stop Pad
Stop All
Engine status
Mic Mute
```

## Voice

```text
Voice FX
Pitch
Fine Pitch
Formant
User Presets
```

## Mixer

```text
Mic Gain
Soundboard Gain
Master Gain
possibly Media Gain
```

## Media

```text
Play/Pause
Stop
Seek
±10
Volume
Monitor
Send
```

## Remote Audio

```text
Remote Monitor source mix
Monitor master volume
Phone mic enable/mute
Connection/network stats
```

Do not turn the phone UI into the complete desktop Settings page.

---

# 63. Multi-client policy

Design the protocol so multiple paired web clients do not corrupt state.

Initial product behavior may allow multiple control clients, but only one active Remote Mic session should be required for v1.3.

Recommended:

```text
Control clients: multiple allowed
Monitor listeners: one or limited number initially
Remote Mic talker: one active source at a time
```

If a second phone tries to become Remote Mic:

```text
Remote microphone already active on Mehdi's Phone.
[Take Over]
```

Takeover must be explicit.

---

# 64. Security boundaries

Remote client commands must be allowlisted.

Do not expose endpoints for:

```text
arbitrary shell commands
arbitrary file reads
arbitrary file writes
registry editing
installer execution
driver installation
process launching
raw diagnostics file download
```

Remote exists to control GrassiBoard's approved live functions.

Validate numeric ranges on the server even if the web UI already clamps them.

Examples:

```text
Pitch range
Fine Pitch range
Formant range
Gain ranges
Media seek bounds
Pad IDs must exist in active profile
Preset IDs must exist
```

---

# 65. Privacy

Do not send more local information than the phone UI needs.

Remote snapshots should prefer:

```text
Pad title
Pad ID
Pad status
Media display name
Preset display name
Audio state
```

over:

```text
C:\Users\...\private\full\file\paths
```

Remote logs must not include pairing credentials or raw microphone audio dumps unless the user explicitly enables a diagnostic recording.

---

# 66. No audio in control protocol

Keep architecture separation strict:

```text
WebSocket / control transport
→ commands, state, events, signaling

WebRTC
→ realtime audio
```

Do not stream PCM chunks through JSON/Base64 WebSocket messages as the production Remote Monitor or Remote Mic solution.

---

# 67. Audio thread rules remain absolute

Remote features must not weaken the v1.0.1 realtime rules.

The native realtime callback must never:

```text
perform network IO
perform HTTP/WebSocket work
perform WebRTC signaling
encode/decode Opus
allocate dynamic memory
write logs
access WPF
wait on blocking locks
perform disk IO
```

Use preallocated rings and worker threads at subsystem boundaries.

---

# 68. Dependency policy

Any new dependency must be:

- actively maintained enough for the use case;
- pinned;
- license-reviewed;
- added to `LICENSES.md` / notices as required;
- restored by clean GitHub Actions;
- included in self-contained publish when required;
- tested for Windows 10 x64;
- justified in documentation.

For WebRTC in particular, do not select a dependency before the v1.2 technology spike.

---

# 69. GitHub Actions changes

v1.1 likely adds a web build step.

Expected conceptual order:

```text
Checkout
Setup pinned .NET SDK
Setup pinned Node LTS for RemoteWeb build
npm ci
npm run build
Generate build information
Configure/build native engine
Native tests
Managed tests
Remote protocol/web tests
Publish self-contained WPF app
Verify RemoteWeb assets
Package portable
Build installer
Verify installer
Upload artifacts
```

Keep action commits pinned according to the existing repository policy.

If v1.2/v1.3 add native/runtime WebRTC DLLs, portable-package verification must explicitly require them.

---

# 70. Installer requirements

The installer must install everything required for Remote UI operation.

The user must not separately install:

```text
Node.js
npm
ASP.NET hosting bundle
WebRTC developer SDK
codec development tools
```

unless a third-party runtime is truly unavoidable and explicitly approved.

Any firewall/certificate onboarding must be explained through UI/setup, not a hidden README-only prerequisite.

VB-CABLE remains an external dependency as in the current accepted product.

---

# 71. Documentation to add/update

Create/update as development progresses:

```text
docs/remote-roadmap.md                 ← this document
docs/remote-development-status.md      ← authoritative progress tracker
docs/remote-control.md                  ← v1.1 user/architecture docs
docs/remote-monitor.md                  ← v1.2 docs
docs/remote-audio.md                    ← v1.3 docs
docs/audio-pipeline.md                  ← add Remote Monitor / Remote Mic routes
docs/architecture.md                    ← command/state/server boundaries
docs/current-status.md                  ← current accepted release
README.md                               ← user-facing feature/setup summary
CHANGELOG.md                            ← actual shipped features only
LICENSES.md / THIRD-PARTY-NOTICES.txt   ← new dependencies
```

Do not claim an unaccepted feature as stable in README/current-status.

---

# 72. Source-control boundaries

The old experimental custom driver source may remain in the repository for history, but Remote development must not depend on it.

Do not modify `src/GrassiBoard.Driver` merely to make Remote work.

The stable route remains:

```text
GrassiBoard Program Mix
→ external VB-CABLE render endpoint
→ CABLE recording endpoint
→ target app microphone
```

---

# 73. Performance budget

Remote Control should add negligible load when no client is connected.

Remote Monitor/Remote Mic worker threads may use CPU/network, but must not starve the audio callback.

Track at least:

```text
CPU with Remote disabled
CPU with Remote Control connected
CPU with Remote Monitor connected
CPU with Full Duplex connected
Memory growth over 60 min
Control message rate
WebRTC bitrate
Audio underruns/overruns
```

A disconnected Remote must not materially change core audio latency.

---

# 74. Failure isolation

A Remote subsystem crash/failure must not crash the audio engine.

Examples:

```text
Web server fails → desktop GrassiBoard still works
Phone disconnects → desktop GrassiBoard still works
WebSocket fails → audio engine still works
WebRTC monitor fails → Program mix still works
Remote Mic fails → mic branch goes safe; Soundboard/Media/program render survive
```

Treat Remote as an optional peripheral subsystem around the stable core.

---

# 75. Recovery controls

Desktop Settings should eventually provide:

```text
Restart Remote Server
Regenerate Pairing Code
Revoke Device
Revoke All Devices
Reset Remote Settings
Restart Remote Monitor
```

Do not require restarting the entire GrassiBoard engine to recover only the Remote web server.

---

# 76. Remote settings UX on Windows

Suggested Settings group:

```text
REMOTE CONTROL

Remote Control                [ ON ]
Network                       Home Wi-Fi
Address                       192.168.1.20:xxxxx
Connected Clients             1

[ Show Pairing QR ]
[ Restart Remote Server ]

PAIRED DEVICES
Mehdi's Phone      ● Connected     [ Revoke ]

REMOTE MONITOR (v1.2)
External Audio Source          Windows Output / Chrome
Output Endpoint                Headphones (...)

REMOTE AUDIO (v1.3)
Remote Microphone              Connected / Off
```

Keep advanced network diagnostics in an expander.

---

# 77. Profile behavior

When the user switches Profiles on Windows:

```text
active Pads change
active User Presets change
Voice/Mixer state changes
Media preferences may change
```

Remote clients must receive a fresh authoritative snapshot or sufficient deltas.

Do not keep showing Pad IDs from the previous Profile.

Pairing identity itself remains global and should survive Profile changes.

---

# 78. Future extensibility — do not implement yet

The command/state boundary intentionally prepares for future controllers:

```text
MIDI
Stream Deck
OSC
Numpad/macropad
Desktop mini overlay
```

Do not add these during v1.1–v1.3 unless the user explicitly expands scope.

The Remote roadmap should not become an excuse for unrelated feature creep.

---

# 79. Definition of final “monster mode” success

The full roadmap is successful when this scenario works reliably:

```text
1. Windows laptop is in another room.
2. GrassiBoard and Chrome/X Space are running.
3. Chrome microphone is CABLE Output.
4. User is in bedroom with Android phone and earbuds.
5. User opens paired GrassiBoard Remote.
6. User hears the Space on the phone.
7. User speaks using the phone/earbud microphone.
8. GrassiBoard applies Pitch/Formant/Preset live.
9. User triggers Sound Pads from phone.
10. User controls Media Deck from phone.
11. User changes Voice/Mixer controls from phone.
12. Audience hears the correct Program Mix.
13. User hears the independent Remote Monitor Mix.
14. User's microphone is not unintentionally echoed back.
15. User can Mute, Stop All, and Start Engine from the phone.
16. Temporary Wi-Fi loss recovers without corrupting GrassiBoard state.
17. The original physical-mic desktop workflow still works unchanged.
```

---

# 80. Development gates summary

```text
BASELINE
v1.0.1 USER ACCEPTED
        │
        ▼
v1.1.x REMOTE CONTROL
Web UI + pairing + realtime sync
        │
        ├─ user rejects → patch v1.1.x only
        │
        └─ USER ACCEPTS
                │
                ▼
v1.2.x REMOTE MONITOR
System/process audio + independent monitor bus + WebRTC downlink
        │
        ├─ user rejects → patch v1.2.x only
        │
        └─ USER ACCEPTS
                │
                ▼
v1.3.x FULL REMOTE AUDIO
Secure mic onboarding + WebRTC phone mic + full duplex
        │
        ├─ user rejects → patch v1.3.x only
        │
        └─ USER ACCEPTS
                │
                ▼
REMOTE ROADMAP COMPLETE
```

---

# 81. Mandatory Codex handoff behavior

At the start of every new Remote-development task:

1. read `docs/remote-development-status.md`;
2. read the relevant section of this roadmap;
3. read `docs/current-status.md`;
4. inspect the current code before editing;
5. do not assume previous chat claims are newer than repository status;
6. implement only the currently permitted stage;
7. run/update automated tests;
8. keep GitHub Actions green;
9. produce a test artifact;
10. give the user a concise manual test checklist;
11. wait for the user's result;
12. do not mark acceptance until the user explicitly approves;
13. after approval, update status files and provide an exact commit message.

---

# 82. Official technical references for implementation

These links are reference material, not a substitute for testing on the user's Windows 10 + Android devices.

- ASP.NET Core WebSockets: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets
- ASP.NET Core Kestrel: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel
- WASAPI loopback recording: https://learn.microsoft.com/en-us/windows/win32/coreaudio/loopback-recording
- Application/process loopback sample: https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/
- `AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS`: https://learn.microsoft.com/en-us/windows/win32/api/audioclientactivationparams/ns-audioclientactivationparams-audioclient_process_loopback_params
- W3C WebRTC: https://www.w3.org/TR/webrtc/
- W3C Media Capture / `getUserMedia`: https://www.w3.org/TR/mediacapture-streams/

Important verified constraints for this roadmap:

- standard WASAPI loopback can capture the mix played on a render endpoint;
- process-specific loopback must be runtime-gated because Microsoft's API requires a newer Windows build than many ordinary Windows 10 installations;
- WebRTC supports sending/receiving realtime media tracks;
- browser microphone capture via `getUserMedia()` is a secure-context-sensitive capability, which is why v1.3 contains an explicit HTTPS/companion-client technology gate.

---

# 83. Final instruction

Do not try to implement v1.1, v1.2, and v1.3 in one giant change.

The point of this roadmap is to create the monster **without killing the stable app that already works**.

Each stage must be independently useful:

```text
v1.1 → phone becomes the control surface
v1.2 → phone becomes the listening/monitor surface
v1.3 → phone becomes the full live audio console
```

Stop after each stage, give the user a real build, wait for real-device testing, fix that stage until accepted, record acceptance, then continue.
