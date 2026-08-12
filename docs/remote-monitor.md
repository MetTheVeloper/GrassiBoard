# GrassiBoard v1.2 Remote Monitor

> **Target:** personal-stable v1.2 production candidate  
> **Transport:** same-LAN WebRTC / Opus  
> **Program route:** external VB-CABLE, unchanged

## What v1.2 adds

GrassiMote can receive an independent phone-monitor mix while continuing to control the Windows app in realtime.

```text
Windows / Space ───────┐
Soundboard ────────────┤
Media ─────────────────┼→ Remote Monitor Mix → limiter → Opus/WebRTC → Android
My Voice (opt-in) ─────┘
```

The Remote Monitor bus is separate from the Program/VB-CABLE mix.

## Accepted source behavior

- **Windows / Space:** WASAPI loopback of the active default Windows render endpoint.
- **Soundboard:** ABI-9 internal raw Soundboard monitor tap.
- **Media:** direct managed Media tap when needed.
- **Media duplicate prevention:** when Media local monitoring is already present in the captured Windows endpoint, direct Media contribution is automatically suppressed.
- **My Voice:** processed microphone tap after Pitch/Formant + Mic Mute, before Program Mic Gain/Master; OFF by default.
- **Monitor Master:** phone-monitor-only master.
- **Peak limiter:** monitor-only clipping protection.

## Accepted Android UI

The normal Monitor page is intentionally minimal:

- Start / Stop Monitor
- Monitor mute
- six direct quick tiles: Windows, Board, Media, Voice, Voice Level, Master
- tap/horizontal-drag gain control with subtle percentage fill
- duplicate-aware Media tile (`Via Windows`)
- expandable precise Monitor Levels
- collapsed Connection Details and Advanced Diagnostics

## Audio profile

- 48 kHz
- stereo monitor transport
- 20 ms framing
- Opus
- 128 kbps target
- VBR
- complexity 10
- same-LAN host ICE; no cloud signaling service required

## Acoustic feedback

My Voice is network self-monitoring. Use phone headphones/earbuds when My Voice is enabled. Phone-speaker playback can feed back into the physical microphone and create an acoustic echo loop.

## v1.2 final acceptance gate

Before marking v1.2 USER ACCEPTED:

1. Apply Hotfix 36.
2. Build the normal v1.2 path **without** `-RemoteMonitorSpike`.
3. Run native + managed smoke tests.
4. Verify Board/Voice/Mixer/Media/Monitor controls still work.
5. Run Windows + Soundboard + Media + optional My Voice for 30–60 minutes.
6. Confirm no growing delay, periodic stutter, crackle, packet-loss problem, memory runaway, or engine dropout.
7. Confirm Program/VB-CABLE remains unchanged.
8. If GitHub CI is used, confirm self-contained portable/installer artifacts build successfully.

After explicit user approval, record v1.2 as USER ACCEPTED and unlock v1.3 Remote Phone Microphone work.
