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

Status: **FUNCTIONALLY PROMISING / RECORD-SYNC HARDENING IMPLEMENTED / FULL USER RETEST NEXT**

Primary implementation commit: `94052e8a1b4e3055fbf237c4a54c083eeb961e14` (`feat(looper): implement Gate 4 child layer recording`)

User manual feedback before timing hardening:
- core Gate 4 functionality appeared correct;
- user requested the existing Local Media microphone-sync compensation and Settings calibration be reused by Looper child recording before final Gate 4 acceptance.

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
- deterministic Replace reference behavior: loop=8, input=12 -> `[8,9,10,11,4,5,6,7]`;
- processed Windows Mic / Phone Mic capture reuses the accepted Gate 3 Record Tap;
- Cancel/Discard preserves the pre-record Track state;
- one meaningful Undo restores the pre-record destructive state;
- Stop during active recording finalizes the valid Take and then returns transport to frame 0;
- deleting all child layers unlocks Master redefinition; while any child exists, Edit/Import Master is explicitly disabled;
- leaving Looper during an active layer Take cancels safely;
- no live mic self-monitor is introduced.

### Gate 4 record-sync hardening — pulled forward by explicit user request

The initial roadmap placed final record alignment in Gate 5. During Gate 4 real-use testing, the user explicitly required the already-established Local Media sync behavior to apply to microphone-recorded Looper layers **before Gate 4 can be accepted**. This requirement is therefore pulled forward into Gate 4 hardening. Gate 5 remains locked and will later validate/refine timing across quality modes and edge cases rather than reintroduce a second calibration system.

The implemented child-Take compensation now snapshots, at Record start:

```text
active microphone source buffer
+ current Pitch/DSP latency
+ Looper local monitor path estimate
+ existing Media Sync Calibration setting
= child-Take record compensation
```

Important details:
- the existing persisted `MediaSyncOffsetMilliseconds` setting is reused; there is **no separate Looper calibration slider**;
- Windows Mic uses current capture-buffer + ring-buffer timing;
- Phone Mic uses current Remote Input fill timing;
- Looper local monitor timing uses its real path: **30 ms WASAPI target + 1,920-frame / 40 ms prebuffer**;
- the user's existing signed calibration is added to that Looper monitor estimate with the same sign convention;
- compensation is snapshotted once at Take start so a Take cannot change alignment halfway through;
- a free first-Master recording has no existing Master clock and therefore receives no automatic project-position shift; it still uses the accepted trim workflow;
- aligned child recording treats the initial compensation interval as capture pre-roll and removes it before composition;
- the UI-visible captured-frame counter excludes that pre-roll, so **One Cycle records an extra compensation tail internally** before auto-stop and does not truncate the end of the musically aligned cycle;
- source-change/engine-stop fail-safe behavior remains unchanged.

### Gate 4 local automated validation

From the repository root on `feature/grassilooper-v1.4`:

```powershell
.\tools\Test-GrassiLooperGate4Local.ps1 -Run
```

A full run rebuilds the local app, runs native deterministic tests, executes Gate 1/2/3/4 managed ModuleInitializer smoke tests, validates One Cycle/Replace/Overdub, validates the 70 ms Looper local-monitor timing baseline, verifies shared calibration wiring, and launches the local app.

The command must end with:

```text
GATE 4 LOCAL AUTOMATED VALIDATION: PASS
```

### Gate 4 real Windows acceptance checklist

```text
[ ] Existing Master -> Add Layer -> One Cycle: exactly one aligned cycle is committed, recording stops automatically, Master playback continues
[ ] Percussive/click reference: recorded hit onset is musically aligned with the Master after playback
[ ] Existing Settings Media Sync Calibration affects Looper recording alignment too; no second Looper-only calibration is required
[ ] Change calibration deliberately (for example +/-40 ms), record a fresh layer, and verify the recorded placement moves in the expected direction
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

Gate 4 passes only after the user explicitly accepts the full real Windows result.

## Gate 5 scope after Gate 4 sync hardening

Gate 5 remains mandatory and locked until Gate 4 acceptance. Its revised role is to **validate and refine** the shared record-alignment foundation now introduced during Gate 4 hardening: quality-mode changes, timing stability, edge cases, measurement diagnostics, and any residual fixed offset found by real click/percussive testing. It must not create a competing Looper-only calibration preference.

## Gate sequence

```text
Gate 0  Freeze v1.3 baseline                         USER ACCEPTED
Gate 1  UI + project + waveform                     USER ACCEPTED
Gate 2  Master Loop + transport + local monitor     USER ACCEPTED
Gate 3  First Mic Master + processed Record Tap     USER ACCEPTED
Gate 4  Child layers + recording modes + sync       CURRENT / FULL RETEST NEXT
Gate 5  Record alignment validation/refinement      LOCKED
Gate 6  Voice FX snapshots + session restore        LOCKED
Gate 7  Track editor + mixer polish                 LOCKED
Gate 8  Persistence + Project Library + DAW ZIP     LOCKED
Gate 9  Regression + soak + final acceptance        LOCKED
```

## Product decisions already scheduled

### Project Library — Gate 8

Gate 8 must provide a persistent multi-project library/recent-projects workflow with stable project identity, safe save-before-switch behavior, reopen support, explicit missing/corrupt asset states, and explicit deletion. Until Gate 8, `New Project` remains a destructive in-memory development reset.
