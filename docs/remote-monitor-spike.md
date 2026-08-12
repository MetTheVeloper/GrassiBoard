# GrassiBoard v1.2 Remote Monitor — WebRTC/Opus technology spike

> **Status:** Historical v1.2 engineering/spike record. The accepted implementation is promoted by Hotfix 36 into the personal-stable v1.2 production candidate. Keep this file for diagnostics and provenance.

## Purpose

This document tracks the staged v1.2 Remote Monitor feasibility gates. The original transport question is already accepted on the real Android device:

> Can the existing authenticated GrassiMote WSS connection negotiate a same-LAN WebRTC audio session from Windows to Android Chrome and deliver Opus audio reliably? **Yes.**

The spike has since validated default Windows-output WASAPI loopback, explicit high-quality Opus tuning, the **experimental ABI-9 read-only Soundboard tap**, the clean Gate 4A Windows + Soundboard Monitor Mix, and Gate 4B direct Media + duplicate prevention on the real Android device. The current slice is **Gate 4C**: processed **My Voice** self-monitoring as an explicit opt-in source. Hotfix 36 promotes the normal v1.2 build to ABI 9 after all source/mix gates were manually accepted.

Still locked from release: any change to the accepted Program/VB-CABLE route. Gate 4C only adds an experimental processed-mic monitor tap and does not alter the audience mix.

## Temporary dependency candidate

Local spike builds conditionally reference:

```text
SIPSorcery 10.0.13
```

The package targets modern .NET and provides the WebRTC/ICE/SDP transport used by this experiment. The official SIPSorcery package includes managed Opus support through Concentus. The synthetic source uses `AudioExtrasSource` + `AudioEncoder`, with local formats restricted to Opus. No STUN/TURN server is configured for this gate; it intentionally tests host ICE candidates on the same LAN.

The first spike candidate used `SpawnDev.SIPSorcery 10.0.7`. The real-device test reached the Windows session creation path but `AudioEncoder` exposed no Opus source format, so that candidate was rejected for the audio spike before ICE/DTLS negotiation. Hotfix 18 swaps only the conditional spike dependency to official `SIPSorcery 10.0.13`; normal release builds remain unaffected.

### License gate — REVIEW COMPLETE / USER DECISION REQUIRED

The production decision remains intentionally blocked, but the reason is now precisely documented.

The tested transport is still **SIPSorcery 10.0.13**. Current upstream SIPSorcery licensing uses BSD-3-Clause plus an additional geographic field-of-use/distribution restriction. The upstream text states that outside the restricted geography the ordinary BSD-3-Clause terms apply, with no additional commercial-use restriction and no requirement to relicense derivative works.

GrassiBoard must not silently treat this as ordinary unrestricted BSD because the extra geographic condition still matters for distribution policy.

An older pre-restriction package/tag is not considered a clean workaround. In particular, the SIPSorcery **v8.0.12** license file contains a separate unresolved provenance warning around DTLS/SRTP code and states that downstream users should take the safest course of assuming an AGPL-3.0 claim or removing the affected files. Removing those files would make DTLS/SRTP unusable, which defeats the WebRTC requirement.

Decision gate:

1. **Accept the tested SIPSorcery 10.0.13 licensing for the intended GrassiBoard distribution scope.**
   - Keep the exact transport already validated on Windows + Android.
   - Add the required upstream notice to GrassiBoard third-party notices.
   - Promote Remote Monitor/ABI 9 into a v1.2 release-candidate build.
   - Run CI/package verification and the long-duration soak test.

2. **Reject that licensing and replace the transport.**
   - Keep all accepted monitor mixing/source code.
   - Replace only the WebRTC transport boundary.
   - Re-run transport, quality, reconnect, packaging and long-session gates before v1.2 acceptance.

Until the user explicitly chooses one of those paths, `EnableRemoteMonitorSpike=true` remains the only supported build path and release CI must stay locked.


## Signaling

The spike reuses the existing authenticated GrassiMote WebSocket. No second signaling server is added.

Messages:

```text
monitor.spike.offer   phone → Windows
monitor.spike.answer  Windows → phone
monitor.spike.ice     Windows → phone (server trickle; Android host ICE is bundled into the offer SDP)
monitor.spike.state   Windows → phone
monitor.spike.stop    phone → Windows
```

Normal Remote protocol commands and authoritative state continue unchanged.

## Local build

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Build-LocalRemoteTest.ps1 -RemoteMonitorSpike -Run -RunSmokeTests
```

Hotfix 36 enables the accepted v1.2 monitor compile path by default. `-RemoteMonitorSpike` is retained only as a compatibility/diagnostic alias; the normal local command now builds the ABI-9 v1.2 path and identifies itself as `1.2.0`.

## Real-device test gate

Use the same Windows + Android LAN/hotspot topology already accepted for v1.1. Test first with VPNs disabled.

Success requires:

1. GrassiMote connects normally over secure LAN IP/WSS.
2. A **Monitor** destination appears only in a spike-enabled build.
3. Tap **Start Remote Monitor** (explicit user gesture). No Windows-side playback or audio setup is required; the synthetic sine source is generated inside the spike service and starts automatically after WebRTC reaches `connected`.
4. Android first gathers its same-LAN host ICE candidate into the SDP offer, then status progresses from negotiating to connected/streaming.
5. Android receives an audio track and audibly plays a stable synthetic tone.
6. Stop terminates playback promptly.
7. Board/Voice/Mixer/Media controls continue working normally.
8. The original transport gate used native ABI 8 and made no Program/VB-CABLE changes. The later Soundboard gate uses conditional ABI 9 only in the dedicated spike native build.

If Android blocks autoplay, the Monitor screen exposes a manual **Play test tone** action; that does not count as a transport failure.

## After a successful spike

Do **not** immediately merge this exact implementation. Record the result and decide the dependency/license gate first. Then proceed to the next v1.2 engineering slice:

1. isolated managed Windows render-endpoint loopback worker;
2. independent Remote Monitor source/mix model;
3. real PCM → Opus/WebRTC feed;
4. duplicate-source prevention for Media/system loopback;
5. measured end-to-end latency and long-session stability;
6. production packaging/CI decision.

### Opus encoder opt-in

The official SIPSorcery `AudioEncoder` does not enable Opus in its default format list. The spike therefore constructs it with `includeOpus: true` before restricting the synthetic source to `AudioCodecsEnum.OPUS`. This is required for the sine-wave transport test and remains isolated behind the `REMOTE_MONITOR_SPIKE` build flag.

## Real-device transport result (2026-08-12)

The synthetic gate passed on the real Windows + Android topology:

```text
Peer connected
ICE connected
Track received
Synthetic Opus tone audible on Android
```

This closes the transport feasibility question. Hotfix 21 advances the spike to the next isolated gate: **default Windows render-endpoint loopback → 20 ms PCM frames → Opus → WebRTC → Android**. The synthetic sine remains selectable as a fallback diagnostic source.

### Windows output loopback gate

The new `windows-loopback` source uses NAudio `WasapiLoopbackCapture` against the current default multimedia render endpoint. Capture is requested at the negotiated Opus clock rate using 16-bit PCM and packetized into 20 ms frames before calling `AudioEncoder.EncodeAudio` and `RTCPeerConnection.SendAudio`.

This is still **not** the final Remote Monitor mix. In particular it does not yet provide independent source gains, duplicate-source prevention, processed-mic monitoring, or direct taps from the native Soundboard/Media buses.

Test procedure:

1. Build with `-RemoteMonitorSpike` as before.
2. Open **Monitor** and leave **Windows output** selected.
3. Tap **Start Windows Monitor**.
4. Once Peer/ICE are connected, play a YouTube/video/music source on the Windows default output device.
5. The same Windows output should be audible on Android.
6. Stop Windows playback and verify Android becomes silent; restart playback and verify it resumes.
7. Use **Test tone** to confirm transport independently if loopback capture fails.

If the default Windows playback endpoint is not the device carrying the desired audio, this gate should be reported rather than changing the system device; endpoint selection is a later v1.2 slice.


### Hotfix 22 — nullable negotiated format

`AudioFormat` is a value type in the SIPSorcery media abstractions. The spike stores the negotiated format as `AudioFormat?` until SDP negotiation completes. The Windows loopback path must unwrap the nullable value after its null guard before reading `ClockRate`, `ChannelCount`, or `RtpClockRate`, and before passing the format to `AudioEncoder.EncodeAudio`.


## Gate 2 follow-up: resilient default-output capture

After real-device validation confirmed that Windows WASAPI loopback audio reaches Android over the established WebRTC/Opus transport, the spike now keeps the peer connection alive when the Windows default playback endpoint changes.

- The active default render endpoint is checked approximately every 750 ms while Windows-output monitoring is active.
- If the endpoint ID changes, only the WASAPI loopback capture source is replaced; the existing WebRTC peer, ICE/DTLS session, RTP sender, and Opus track stay alive.
- GrassiMote receives the updated device name and a short status detail when capture moves to the new endpoint.
- The monitor UI exposes the negotiated capture sample rate, channel count, and 20 ms packetization interval for quality diagnostics.
- The browser audio element remains an implementation detail. GrassiMote exposes a full-width **Mute monitor audio / Unmute monitor audio** action instead of native HTML audio transport controls; muting does not pause or renegotiate the stream.

### Quality note

The spike does not explicitly request a reduced Opus bitrate. Its current capture path negotiates Opus through SIPSorcery, uses 20 ms frames, and converts the loopback feed to 16-bit PCM at the negotiated sample rate/channel count before encoding. Perceived quality differences therefore need a separate quality-tuning gate rather than being attributed to an intentional low-bitrate mode in GrassiBoard.

## Gate 2 follow-up: background persistence + receive diagnostics

The accepted Windows-output gate showed `48 kHz · 2 ch · 20 ms`, which is already the intended full-band stereo capture shape for this LAN monitor. Before changing encoder knobs blindly, the spike now exposes receiver-side WebRTC statistics (effective inbound bitrate, jitter, packet loss, codec) so later Opus tuning can be based on measured transport behavior rather than capture-format guesses.

The Android/PWA lifetime model is also hardened:

- the hidden audio element is mounted at the app-shell level instead of inside `/monitor`, so changing GrassiMote pages does not stop monitor playback;
- monitor intent is persisted locally and foreground/resume events retry negotiation if Android discarded the page/peer;
- Media Session metadata and play/pause handlers are registered so Android/Chrome treat the stream as ongoing media playback and expose system media controls;
- a transient Remote control WebSocket disconnect no longer tears down the WebRTC media session on Windows; monitor sessions are keyed by the stable paired client id and the signaling event sink is rebound when WSS reconnects;
- explicit **Stop monitor**, disabling Remote, or stopping the Remote server still closes the media session.

This is best-effort PWA background media behavior, not a promise that Android can never kill the browser process. If the OS actually discards the PWA process, no web code remains alive; the persisted monitor intent is used to rebuild the WebRTC session automatically when GrassiMote returns and Remote WSS reconnects.

## Gate 2 follow-up: explicit Opus quality profile + Android task resilience

The real-device receiver measurements for the accepted Windows-loopback path were stable enough to tune quality without changing capture shape:

```text
Capture       48 kHz · 2 ch · 20 ms
Inbound       ~100–104 kbps typical, occasional ~64 kbps
Jitter        ~22–24 ms
Packets lost  0
Codec         Opus
```

Because capture is already 48 kHz stereo and the LAN showed zero packet loss, the spike keeps the 20 ms real-time framing and moves only the Windows-loopback encoder to an explicit Concentus profile:

- `OpusApplication.OPUS_APPLICATION_AUDIO` (the Concentus factory default);
- 128 kbps target for stereo, 96 kbps for mono;
- VBR enabled, constrained VBR disabled;
- DTX disabled;
- complexity 10;
- 16-bit input-depth hint.

SIPSorcery remains responsible for WebRTC/SDP/RTP. Direct Concentus encoding is used only for the Windows-loopback PCM payload so the quality target is deterministic during this spike. The existing SIPSorcery encoder remains as a defensive fallback and continues to serve the synthetic-tone path.

### Installed-PWA task/background behavior

The first background pass exposed a GrassiMote lifecycle bug: the Media Session `stop` action was mapped directly to `monitor.stop()`. A system/task media stop could therefore clear the persisted monitor intent and explicitly tear down the Windows session when the installed PWA was minimized.

This gate changes the contract:

- only the in-app **Stop monitor** action tears down the WebRTC monitor session;
- Media Session pause/stop actions are local listening mute operations and never signal Windows to stop;
- the desired monitor source remains persisted across task/background transitions;
- foreground/focus/pageshow/resume paths rebuild the browser peer automatically if the local peer/track disappeared;
- a server-side `connected` state is not treated as a usable local monitor unless the browser still owns a live peer/audio track;
- the service worker cache advances to `grassimote-shell-v11`.

Android/Chrome still owns the final background process policy. A browser page may be frozen or discarded, so this remains a best-effort PWA gate: if Android suspends playback while the screen is locked, GrassiMote must at minimum preserve intent/session state and recover automatically on foreground. Guaranteed uninterrupted audio while the web process is fully suspended would require a native Android foreground-media component and is outside this web-only spike.

## Gate 3 — ABI-9 native Soundboard source tap

The transport gate and Windows-output loopback gate are accepted on the real Android device. The next isolated slice validates an **independent internal GrassiBoard source** without yet building the complete Remote Monitor mixer.

Spike builds now add a native ABI-9-only Soundboard tap behind `GRASSIBOARD_REMOTE_MONITOR_TAP`. Normal/release builds remain ABI 8 until the v1.2 architecture is accepted.

Native source shape:

```text
SoundboardMixer raw stereo branch
(post per-pad volume, pre Program Soundboard gain/master)
        ↓
allocation-free SPSC stereo float ring
        ↓
gb_monitor_tap_read (managed worker only)
        ↓
20 ms / 48 kHz frames
        ↓
128 kbps Opus VBR
        ↓
existing WebRTC peer
        ↓
GrassiMote
```

The realtime render callback only performs a bounded ring `Push`. It does not allocate, block, encode, call managed code, or modify the Program sample. When the ring is full the newest tap frame is dropped and an overrun counter increments; Program/VB-CABLE rendering continues unchanged.

New experimental ABI-9 exports:

```text
gb_monitor_tap_set_enabled
gb_monitor_tap_clear
gb_monitor_tap_read
gb_monitor_tap_get_statistics
```

The local spike build uses a separate CMake preset/output directory:

```text
windows-x64-remote-monitor-spike
out/build/windows-x64-remote-monitor-spike
```

This prevents the experimental ABI-9 DLL from replacing the accepted normal ABI-8 build cache. `Build-LocalRemoteTest.ps1 -RemoteMonitorSpike` intentionally rebuilds this native target instead of reusing the installed ABI-8 DLL.

### Soundboard gate test

1. Build with `-RemoteMonitorSpike -Run -RunSmokeTests`.
2. Start the GrassiBoard audio engine.
3. In GrassiMote → Monitor choose **Soundboard tap**.
4. Tap **Start Soundboard Tap** and wait for Peer/ICE/Track connected.
5. Trigger several Pads, including a looping Pad if available.
6. The Pad audio must be heard on Android even though the monitor source is not Windows loopback.
7. Change the normal **Soundboard Gain** and **Master Gain** on the Program mixer. The direct monitor tap is intentionally pre-Program gain, so those controls must not change the tap level.
8. Verify the audience/VB-CABLE Program route still receives Soundboard exactly as before.
9. Stop the Soundboard tap and verify normal Board/Voice/Mixer/Media operation is unchanged.

This gate was manually accepted on **2026-08-12**. Pads triggered from both Windows and GrassiMote were heard clearly on Android. Program Soundboard Gain and Master did not affect the direct tap, and Program/VB-CABLE behavior remained unchanged.

This acceptance unlocks Gate 4A below.


## Gate 4A — real independent Windows + Soundboard Monitor Mix

Gate 4A is the first multi-source Remote Monitor bus. It deliberately combines only the two source paths already accepted independently:

```text
Default Windows output / Space ──┐
                                 ├─ monitor-only gains ─ Monitor Master ─ 128 kbps Opus ─ WebRTC ─ Android
ABI-9 raw Soundboard tap ─────────┘
```

The Program bus remains separate:

```text
Mic / Soundboard / Media
        ↓
Program mixer / Master
        ↓
VB-CABLE
        ↓
target application
```

### Implementation rules

- Windows loopback and the ABI-9 Soundboard SPSC ring feed a single managed 20 ms mixer worker.
- The loopback callback only buffers PCM while `monitor-mix` is active; it does not encode/send independently.
- The worker performs one Opus encode/send per mixed 20 ms frame.
- Windows loopback buffering is bounded so temporary worker stalls cannot grow into seconds of accumulated monitor latency.
- Soundboard still enters from the accepted pre-Program-gain tap.
- Monitor gains are validated server-side in the range `0.0..1.0`.
- Default monitor-only values:
  - Windows / Space: **90%**
  - Soundboard: **70%**
  - Monitor Master: **85%**
- Live GrassiMote changes use `monitor.spike.mix.set`.
- These controls must never change the Program Soundboard gain, Program Master, or VB-CABLE output.
- Existing isolated `windows-loopback`, `soundboard-tap`, and `synthetic-sine` sources remain available for diagnostics.
- Service-worker shell cache advances to `grassimote-shell-v13` so the installed PWA receives the Gate 4A UI.

### Gate 4A real-device test

1. Build/run with:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\Build-LocalRemoteTest.ps1 -RemoteMonitorSpike -Run -RunSmokeTests
   ```

2. Start the GrassiBoard engine.
3. In GrassiMote → **Monitor**, stop any preserved older monitor session.
4. Choose **Monitor mix** and tap **Start Monitor Mix**.
5. Wait for `Streaming`, Peer connected, ICE connected, and Track received.
6. Play ordinary audio on the Windows default output. It must be audible on Android.
7. Trigger Sound Pads from both Windows and GrassiMote. They must be audible on Android at the same time as Windows audio.
8. Move **Windows / Space** to 0%. Windows audio must disappear from the phone while Soundboard remains audible.
9. Restore Windows / Space, then move **Soundboard** to 0%. Pads must disappear from the phone while Windows audio remains audible.
10. Move **Monitor Master** to 0%. The entire phone monitor must become silent; restore it and both sources must return.
11. While changing those three monitor controls, verify the audience/Program/VB-CABLE route does not change.
12. Change the normal Program **Soundboard Gain** and **Master Gain** and verify they still do not alter the monitor-only source levels.
13. Play Windows audio and several Pads simultaneously for a few minutes. Listen for clicks, periodic stutter, doubled Soundboard, obvious drift, or growing delay.
14. Stop Monitor Mix and start the isolated Windows output/Soundboard diagnostics once each to confirm Gate 4A did not regress the already accepted source paths.
15. Minimize/restore or switch GrassiMote routes once. The existing session-preservation/recovery behavior must remain intact.

### Gate 4A acceptance criteria — USER ACCEPTED 2026-08-12

```text
[x] Windows output and Soundboard are audible simultaneously
[x] Windows monitor gain affects only Windows contribution on the phone
[x] Soundboard monitor gain affects only Soundboard contribution on the phone
[x] Monitor Master affects the phone monitor only
[x] Program/VB-CABLE remains unchanged
[x] Program Soundboard/Master gains remain independent from monitor-only gains
[x] no doubled Soundboard
[x] no obvious clicks/stutter/growing delay during a short mixed-source run
[x] isolated Windows-output gate still works
[x] isolated Soundboard-tap gate still works
[x] PWA reconnect/recovery is not regressed
```

The first mixed run exposed mild mix-only scratch/micro-stutter even though isolated Windows output and Soundboard tap remained clean. The stability follow-up changed only the managed monitor mixer: complete native Soundboard frames became the cadence, Windows loopback is consumed only as complete 20 ms frames after a 40 ms reservoir, buffering is bounded, and the monitor-only sum uses an immediate-attack/slow-release peak limiter.

The user repeated the focused tests and explicitly confirmed the artifact was completely gone and the result was clean. Final observed receive metrics were approximately **128–133 kbps**, **7–11 ms jitter**, and **0 packet loss**. No growing delay was reported. Gate 4A is accepted.

## Gate 4B — direct Media contribution + duplicate prevention

Gate 4B extends only the accepted phone monitor bus:

```text
default Windows output ───────────────┐
ABI-9 raw Soundboard tap ─────────────┼→ monitor-only mix → limiter → Opus → phone
Media Deck direct 48 kHz stereo tap ──┘
```

Media is tapped from the existing managed Media Deck decode worker **after Media Deck volume** and before local endpoint/Program routing. The tap is a bounded in-memory stereo-float ring and is enabled only while Monitor Mix is active. It adds no socket/codec work to the native realtime callback and does not alter Program/VB-CABLE.

Monitor-only defaults:

- Windows / Space: **90%**
- Soundboard: **70%**
- Media: **70%**
- Monitor Master: **85%**

### Duplicate prevention

If Media local monitoring is enabled and its selected monitor endpoint matches the Windows endpoint currently captured by WASAPI loopback, Media is already present inside the Windows / Space branch. The direct Media tap is therefore suppressed and its backlog is cleared. GrassiMote reports **Included through Windows / Space** so a doubled/comb-filtered Media signal is never intentionally produced.

If local Media monitoring is disabled or points to a different endpoint, the direct Media tap contributes instead and the dedicated Media monitor-only gain is active. Switching between these modes must not replay stale Media.

### Gate 4B real-device test

1. Start Monitor Mix with Windows / Space, Soundboard, Media, and Monitor Master at their defaults.
2. Load a recognizable Media Deck track and start playback.
3. With **Media local monitor OFF**, verify Media is still heard on the phone through the direct Media contribution.
4. Move **Media → 0%** and verify only Media disappears from the phone; Windows / Space and Soundboard remain. Restore Media.
5. Confirm ordinary Media transport/volume remains functional and Program/VB-CABLE behavior is unchanged.
6. Enable Media local monitoring and select the **same Windows output endpoint** currently captured by Remote Monitor. GrassiMote Monitor should report **Included through Windows / Space**.
7. Listen for doubling, echo, comb-filtering, phasey sound, or a sudden level jump. None is acceptable.
8. While duplicate suppression is active, Media follows the Windows / Space branch on the phone; the dedicated Media level is retained for when direct mode returns.
9. Change Media local monitor to a **different output endpoint** if available, or turn local Media monitor OFF. The Monitor page should return to direct Media mode without replaying stale audio.
10. Seek, pause/resume, stop, and restart Media; no stale chunk or delayed replay should appear on the phone.
11. Run Windows audio + Soundboard + Media simultaneously for several minutes and listen for the previously fixed crackle/stutter, growing delay, or clipping.
12. Record receive bitrate, jitter, and packet loss.
13. Briefly retest isolated Windows output and Soundboard tap sources for regression.

### Gate 4B acceptance — USER ACCEPTED 2026-08-12

The user explicitly approved proceeding after the real-device routing behavior matched the design: direct Media is independently controllable when local Media monitoring is OFF; same-endpoint local Media monitoring is detected and folded into Windows / Space without a second direct copy; the Media slider is intentionally bypassed while Media is embedded in Windows / Space; Windows / Space and Monitor Master controls remove that audio exactly as expected. Program/VB-CABLE remained logically isolated.

## Gate 4C — processed My Voice opt-in

Gate 4C extends the accepted monitor-only bus with one deliberately safety-gated source:

```text
default Windows output ───────────────┐
ABI-9 raw Soundboard tap ─────────────┤
Media direct / duplicate-safe path ───┼→ monitor-only mix → limiter → Opus → phone
ABI-9 processed My Voice tap ─────────┘
```

The My Voice native tap is taken **after Pitch/Formant processing and Mic Mute, before Program Mic Gain/dynamics/Master**. The source therefore follows Voice FX and Mute but its phone level does not depend on Program Mic Gain or Program Master. The realtime callback only writes bounded stereo-float samples into a second SPSC ring; WebRTC/Opus/network work remains on the managed worker.

Monitor-only defaults now are:

- Windows / Space: **90%**
- Soundboard: **70%**
- Media: **70%**
- My Voice retained level: **10%**
- My Voice audible state: **OFF**
- Monitor Master: **85%**

An explicit GrassiMote control is required to enable My Voice. Explicitly stopping Remote Monitor resets the next manually started session to OFF; automatic recovery of an already-running session keeps the current state. Headphones/earbuds are strongly recommended because network self-monitoring has audible latency and phone-speaker playback can create acoustic feedback.

The installed PWA shell cache advances to `grassimote-shell-v15` for this Gate 4C UI/state contract.

### Gate 4C real-device test

1. Start Monitor Mix and confirm **My Voice is OFF** by default. Speak into the selected GrassiBoard physical microphone; no self-monitor should be added by the My Voice branch.
2. Use headphones/earbuds, then tap **Enable My Voice**. Speak and confirm the processed voice is now heard on the phone.
3. Set **My Voice level → 0%**; only self-monitor should disappear. Windows / Space, Soundboard, Media, and Program/VB-CABLE remain unchanged. Restore to a conservative level such as 10–20%.
4. Change **Pitch** and **Formant** while speaking. My Voice on the phone must reflect the live Voice FX changes.
5. Toggle **Mic Mute**. My Voice must become silent immediately and return when unmuted.
6. Change normal Program **Mic Gain** and **Master Gain**. The My Voice phone level should remain independent because this tap is pre Program mixer. Program/VB-CABLE must continue following its own controls.
7. Run Windows audio + Soundboard + Media + My Voice together. Listen for clipping, crackle, micro-stutter, drift, growing delay, or unexpected ducking of the phone-only bus.
8. Disable My Voice while speaking; self-monitor must stop without stopping the rest of Remote Monitor. Re-enable it and confirm clean recovery with no stale voice replay.
9. Stop Remote Monitor explicitly, then start a new Monitor Mix. **My Voice must be OFF again by default.**
10. Record receive bitrate, jitter, packet loss, and subjective self-monitor latency.

Gate 4C is complete only after explicit real-device approval. My Voice latency is expected to be audible over Wi-Fi; the acceptance question is stability, isolation, correct Voice FX/Mute behavior, and absence of feedback/artifacts—not zero-latency self-monitoring.

## Gate 4C acceptance — USER ACCEPTED 2026-08-12

The user explicitly confirmed the full My Voice real-device matrix passed: processed voice quality was clean, Pitch/Formant followed live, Mic Mute silenced the source immediately, Program gain/master remained independent, and the full Windows + Soundboard + Media + My Voice monitor bus ran without crackle, stutter, or other audible defects. Acoustic feedback occurred only when the phone speaker was allowed to re-enter the physical microphone; using phone headphones/earbuds eliminated the loop completely, matching the intended safe operating mode.

## Monitor UX production cleanup

After Gate 4C acceptance, the Monitor page is intentionally reduced from a technology-spike dashboard to a daily control surface. Normal use now prioritizes one-glance state and hides engineering detail:

```text
Monitor                         LIVE
Remote Monitor
Windows 90 · Board 70 · Media Auto · Voice Off · Master 85
[ Stop monitor ] [ mute ]

> Monitor levels
> Connection details
    > Advanced diagnostics
```

`Monitor levels` contains the existing phone-only Windows / Space, Soundboard, Media, My Voice, and Monitor Master controls. `Connection details` contains codec/bitrate/jitter/loss and peer/ICE/track state. Isolated Windows-output, Soundboard-tap, and synthetic-tone source tests remain available only under `Advanced diagnostics`. The primary page no longer exposes Gate numbers, transport prose, or a permanent spike warning.

The first explicit My Voice enable shows a one-time short headphones recommendation Snackbar. No accepted audio, routing, protocol, WebRTC, or Program/VB-CABLE behavior is changed by this UI-only cleanup.


## Monitor UX follow-up — interactive brutal-minimal tiles

After the four-source Monitor Mix and brutal-minimal page hierarchy were manually validated, the first-glance 3×2 summary becomes an active mixer surface rather than read-only status. Windows, Soundboard, direct Media, My Voice level, and Monitor Master tiles accept tap-to-set and horizontal drag while preserving vertical page scrolling through `touch-action: pan-y`. The dedicated Voice tile remains a simple explicit ON/OFF control. Gain tiles show a subtle percentage fill and optionally provide short haptic ticks only at 0/25/50/75/100.

Duplicate prevention remains honest in the compact surface: when Media is already included through Windows output, the Media tile is non-interactive, displays **Via Windows**, and visually follows the effective Windows/Space monitor level. The retained direct Media gain remains available once direct mode returns. The expandable conventional sliders and all connection/advanced diagnostics remain available unchanged. This is a RemoteWeb-only UX layer and does not alter the accepted monitor audio path.
