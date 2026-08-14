# GrassiBoard v1.3 — Remote Phone Microphone

## Gate 1: Android capture → WebRTC → Windows receive

Authoritative baseline: tag `v1.2.0`, commit `c3cf4da1a65a7f97314c265ab581dc9694d1b631`, native ABI **9**.

```text
Android getUserMedia
→ MediaStream audio track
→ WebRTC / Opus / host ICE
→ existing authenticated GrassiMote WSS signaling
→ RemotePhoneMicWebRtcSpikeService
→ SIPSorcery RTP receive
→ managed Opus decode
→ PCM counters + RMS/Peak diagnostics
→ STOP
```

Gate 1 deliberately does **not** feed the native Audio Engine. It does not change the physical Windows microphone, Pitch/Formant/Voice FX, Mic Gain/Mixer, Remote Monitor, Program Mix, or VB-CABLE.

### Security

The accepted v1.2 trusted HTTPS origin, local CA, pairing credential, authenticated WSS channel, and LAN-only host ICE policy are reused. No second signaling server, STUN/TURN service, or cloud dependency is added.

Microphone permission is requested only after **Enable Phone Mic**.

- **Communication:** requests echo cancellation ON, noise suppression ON, AGC OFF, mono.
- **Clean / headset:** requests echo cancellation OFF, noise suppression OFF, AGC OFF, mono.

### Lifecycle

A live Android microphone track is retained across transient WSS/foreground recovery where the browser allows it. If Android ends the track, GrassiMote never silently calls `getUserMedia()` again; the user must explicitly enable it again. Phone-mic failure does not stop Remote Control, Remote Monitor, the Audio Engine, Soundboard, Media, or VB-CABLE.

## Manual Gate 1 test

Build/run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Build-LocalRemoteTest.ps1 -Run -RunSmokeTests
```

Then on the already-paired Android device open:

`https://<PC-LAN-IP>:47919/remote-mic`

Pass criteria:

1. Ordinary Board/Voice/Mixer/Media controls still work.
2. Accepted Remote Monitor Mix still works.
3. Select **Communication**, tap **Enable Phone Mic**, grant permission, and speak for 10–20 seconds.
4. Track becomes `live`.
5. Peer becomes `connected`.
6. ICE becomes `connected` or `completed`.
7. Codec becomes `OPUS`.
8. RTP packets, Decoded frames, and Decoded samples continuously increase.
9. Decode errors stay zero or do not continuously grow.
10. RMS/Peak responds to speech.
11. Stop and repeat once with **Clean / headset**.
12. Background/foreground once while the mic track is live.
13. Wi-Fi off/on once; Control/Monitor must remain isolated and recover independently.
14. Final regression: Windows physical mic, Pitch/Formant, Soundboard, Media, Remote Monitor, Program/VB-CABLE all remain unchanged.

## Gate 1 — PASS / USER ACCEPTED 2026-08-13

Real Android validation passed for both Communication and Clean/headset capture: Opus/RTP decode remained error-free, RMS/Peak followed speech, Stop behaved correctly, Wi-Fi recovery preserved the session, and the accepted lifecycle hotfix remained connected through more than two minutes of background/minimize. v1.2 Remote Control, Media, Soundboard, Remote Monitor, physical Windows Mic, Program and VB-CABLE remained isolated and functional.

## Gate 2 — ABI 10 Remote Phone Mic input

Status after Hotfix 38: **IMPLEMENTED / WINDOWS BUILD + REAL-DEVICE AUDIO TEST PENDING**.

```text
Android Opus decode
→ managed stereo-to-mono normalization
→ bounded 30 ms managed jitter buffer
→ tiny source-clock drift correction
→ 48 kHz mono float
→ ABI 10 native Remote Input SPSC ring (250 ms hard bound)
→ explicit Windows Mic / Phone Mic source selector
→ existing Pitch/Formant / Voice FX
→ existing Mic Gain + dynamics + mixer
→ Program
→ VB-CABLE
```

Windows Mic remains the default. Phone Mic routing requires an explicit **Route Phone Mic** command after the WebRTC peer is connected and both managed/native prebuffers are ready. **Return to Windows Mic**, Phone Mic Stop, session close, engine stop/restart, or route mismatch fail safely back to Windows Mic. A recreated WebRTC session never silently re-arms the Phone Mic route.

The native render worker performs no networking or codec work. Remote transport pushes pre-normalized PCM from a managed non-realtime worker into a bounded SPSC ring; the render path reads the selected source and then reuses the existing Voice DSP and Program mixer.
