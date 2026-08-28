# GrassiLooper development status

> Authoritative short handoff for GrassiLooper work. Read this file first, then the active Gate in `docs/looper-roadmap.md` and the execution policy in `docs/looper-local-test-policy.md`.

## Operational test policy — LOCAL-FIRST

GrassiLooper Gate development is **LOCAL-FIRST**.

- GitHub Actions is **not** a Gate prerequisite during iterative GrassiLooper development.
- The assistant commits a complete testable iteration to `feature/grassilooper-v1.4`.
- The user pulls it and runs the exact local automated build/test command supplied by the assistant.
- The user then performs the requested real Windows/audio/UI test.
- The user's PASS/FAIL report and explicit Gate acceptance/rejection are authoritative.
- Do not delay a Gate waiting for hosted CI when the same deterministic checks can run locally.
- GitHub Actions may be used only when the user explicitly requests it or for a final integration/release checkpoint after gated work is accepted.

`docs/looper-local-test-policy.md` is the operational Source of Truth for test execution and overrides older hosted-CI requirements.

The old Draft PR #10 remains closed without merge because its `pull_request` event triggered the full hosted Build workflow on every branch update. Normal pushes to `feature/grassilooper-v1.4` do not match the Build workflow's push branches.

## Current baseline

- GrassiBoard v1.3 feature baseline: **USER ACCEPTED / PERSONAL-STABLE**.
- Frozen Gate 0 main commit: `819b1e3449b91b9502886fda997eca70764a37ac`.
- Long-lived integration branch: `feature/grassilooper-v1.4`.
- Program microphone route remains external **VB-CABLE**; Looper must not replace or alter it.

## Accepted Gates

### Gate 0 — Freeze v1.3 baseline
Status: **USER ACCEPTED — 2026-08-26**

### Gate 1 — UI foundation + project model + reusable waveform
Status: **USER ACCEPTED — 2026-08-26**

Accepted implementation includes the dedicated Looper workspace, in-memory project/Master/Track foundation, reusable `WaveformView`, off-UI-thread WAV/MP3 analysis, trim editor, and Master-length lock contract.

### Gate 2 — Master Loop engine + transport + local monitor
Status: **USER ACCEPTED — 2026-08-27**

Accepted real Windows behavior includes imported-selection audition, gapless Master playback, shared Media/Looper Monitor Output, seekable playhead, waveform zoom/pan/Fit Selection, Edit Master, Pause/Stop semantics, and stable local monitoring.

### Gate 3 — First Mic Master + processed Record Tap
Status: **USER ACCEPTED — 2026-08-28**

Accepted real Windows behavior:
- first Master can be recorded from the active GrassiBoard microphone;
- processed Voice FX/Pitch/Formant Record Tap works;
- recorded Take enters the same trim/zoom/seek editor;
- precise Master loops can be built from the recorded Take;
- local Gate 3 build/native/managed validation passed before user acceptance.

Important Gate 3 contract remains:
- native ABI is **11**;
- dedicated Record Tap is after current Voice processing and before Program Mic Mute;
- Windows Mic and Phone Mic use the same processed capture path;
- no delayed live microphone self-monitor during recording;
- source change / engine stop fail safe instead of combining invalid input;
- recording safety ceiling remains 10 minutes.

## Current Gate

### Gate 4 — Child layers + One Cycle / Loop Replace / Overdub

Status: **IMPLEMENTED / LOCAL AUTOMATED + USER MANUAL TEST NEXT**

Implementation commit: `94052e8a1b4e3055fbf237c4a54c083eeb961e14` (`feat(looper): implement Gate 4 child layer recording`)

Gate 4 implementation includes:
- `Add Layer` with exact Master-length **48 kHz mono float** buffers;
- up to **32 child layers** with a **256 MiB child-PCM safety budget**;
- native child Track playback on the same authoritative Master playhead;
- child Tracks contribute only to the dedicated Looper monitor mix, not Program/VB-CABLE;
- per-layer Mute/Solo plus native Gain/Pan metadata path;
- Record while stopped/at frame 0 starts the layer flow immediately;
- Record while Master is already mid-cycle enters **ARMED** state and starts on the next observed Master boundary;
- pressing Record again while ARMED cancels before capture;
- **One Cycle** keeps exactly one Master-length pass and automatically stops recording while playback may continue;
- **Loop Replace** circularly overwrites the child buffer from the beginning on each additional pass;
- **Overdub** circularly adds new PCM to existing layer PCM without hard-clipping each pass;
- deterministic Replace reference behavior: loop=8, input=12 → `[8,9,10,11,4,5,6,7]`;
- processed Windows Mic / Phone Mic capture reuses the accepted Gate 3 Record Tap;
- Cancel/Discard preserves the pre-record Track state;
- one meaningful Undo restores the pre-record destructive state;
- Stop during active recording finalizes the valid Take and then returns transport to frame 0;
- deleting all child layers unlocks Master redefinition; while any child exists, Edit/Import Master is explicitly disabled;
- leaving Looper during an active layer Take cancels safely;
- no live mic self-monitor is introduced.

### Gate 4 local automated validation

From the repository root on `feature/grassilooper-v1.4`:

```powershell
.\tools\Test-GrassiLooperGate4Local.ps1 -Run
```

This first reruns the accepted Gate 3 local baseline, then verifies the Gate 4 native child-track API/engine, deterministic One Cycle/Replace/Overdub contracts, source contracts, UI wiring, and launches the local app.

The command must end with:

```text
GATE 4 LOCAL AUTOMATED VALIDATION: PASS
```

### Gate 4 real Windows acceptance checklist

```text
[ ] Existing Master → Add Layer → One Cycle: exactly one cycle is committed, recording stops automatically, Master playback continues
[ ] Add multiple layers: all child layers remain locked to the same Master loop and can be heard together
[ ] Press Record mid-cycle: layer shows ARMED and starts on the next Master boundary; pressing Record again before the boundary cancels the arm
[ ] Loop Replace for more than one pass: later pass overwrites the beginning circularly rather than extending Track length
[ ] Overdub for more than one pass: repeated material accumulates instead of replacing previous audio
[ ] Cancel / Discard during a Take leaves the previous layer unchanged
[ ] Undo after Replace/Overdub restores the immediately previous layer state
[ ] Stop during an active Take commits the valid portion and returns transport to frame 0
[ ] Mute / Solo behave correctly with multiple layers
[ ] Edit Master / Import different Master are locked while child layers exist and unlock after all child layers are deleted
[ ] Windows Mic and Phone Mic both record processed Voice through the existing Gate 3 path
[ ] No delayed live microphone self-monitor appears during child recording
[ ] Program/VB-CABLE, Soundboard, Media, Voice FX, Remote Control and Remote Monitor remain unchanged
```

### Gate 4 timing boundary vs Gate 5

Gate 4 establishes recording-mode semantics, bounded exact-length buffers, shared Master-clock playback, and next-boundary arm behavior. The current arm detector observes the native Master boundary with a small bounded UI polling window. **Gate 5 remains responsible for final capture/DSP/playback latency measurement and sample-alignment compensation.**

For Gate 4 acceptance, report any gross wrong-boundary start, missed arm, gap, drift, wrong Replace/Overdub behavior, crash, source leak, or Program/VB-CABLE regression. A tiny fixed transient offset that is clearly latency-related belongs to Gate 5 and should be reported but does not get silently “fixed” with magic offsets in Gate 4.

Gate 4 passes only after the user explicitly accepts the real Windows result.

## Gate sequence

```text
Gate 0  Freeze v1.3 baseline                         USER ACCEPTED
Gate 1  UI + project + waveform                     USER ACCEPTED
Gate 2  Master Loop + transport + local monitor     USER ACCEPTED
Gate 3  First Mic Master + processed Record Tap     USER ACCEPTED
Gate 4  Child layers + recording modes              CURRENT / LOCAL + REAL TEST NEXT
Gate 5  Record alignment / latency compensation     LOCKED
Gate 6  Voice FX snapshots + session restore        LOCKED
Gate 7  Track editor + mixer polish                 LOCKED
Gate 8  Persistence + Project Library + DAW ZIP     LOCKED
Gate 9  Regression + soak + final acceptance        LOCKED
```

## Product decisions already scheduled

### Project Library — Gate 8

Gate 8 must provide a persistent multi-project library/recent-projects workflow with stable project identity, safe save-before-switch behavior, reopen support, explicit missing/corrupt asset states, and explicit deletion. Until Gate 8, `New Project` remains a destructive in-memory development reset.
