# GrassiLooper development status

> Authoritative short handoff for GrassiLooper work. Read this file first, then the active Gate in `docs/looper-roadmap.md` and the execution policy in `docs/looper-local-test-policy.md`.

## Operational test policy — effective 2026-08-27

GrassiLooper Gate development is **LOCAL-FIRST**.

- GitHub Actions is **not** a Gate prerequisite.
- The assistant commits a complete testable iteration to `feature/grassilooper-v1.4`.
- The user pulls it and runs the exact local automated build/test command supplied by the assistant.
- The user then performs the requested real Windows/audio/UI test.
- The user's PASS/FAIL report and explicit Gate acceptance/rejection are authoritative.
- Do not delay a Gate waiting for GitHub-hosted CI when the same deterministic checks can be run locally.
- Do not keep a PR open merely to trigger CI during iterative Gates.
- GitHub Actions may be used only when the user explicitly requests it or for a final integration/release checkpoint after the gated work is accepted.

`docs/looper-local-test-policy.md` is the operational Source of Truth for how tests are executed and overrides any older interpretation that hosted CI is required during Gate development.

The old long-lived Draft PR #10 was intentionally **closed without merge** on 2026-08-27 because its `pull_request` event triggered the full hosted Build workflow on every branch update. Normal pushes to `feature/grassilooper-v1.4` do not match the Build workflow's push branches (`main`, `codex/**`), so iterative commits no longer start that hosted build.

## Current baseline

- GrassiBoard v1.3 feature baseline: **USER ACCEPTED / PERSONAL-STABLE**.
- Frozen Gate 0 main commit: `819b1e3449b91b9502886fda997eca70764a37ac`.
- Long-lived integration branch: `feature/grassilooper-v1.4`.
- Program microphone route remains external **VB-CABLE**; Looper must not replace or alter it.

## Gate 0 — Freeze v1.3 baseline

Status: **USER ACCEPTED — 2026-08-26**

## Gate 1 — UI foundation + project model + reusable waveform

Status: **USER ACCEPTED — 2026-08-26**

Accepted implementation includes dedicated Looper workspace, in-memory project/Master/Track model foundation, reusable `WaveformView`, off-UI-thread WAV/MP3 decode + waveform analysis at 48 kHz stereo, imported-Master START/END trim editor, and Master-length lock after dependent tracks exist.

## Gate 2 — Master Loop engine + transport + local monitor

Status: **USER ACCEPTED — 2026-08-27**

Accepted real Windows behavior:
- imported selection audition works;
- Master playback loops without growing gap/delay;
- shared Media/Looper Monitor Output works;
- seekable playhead works;
- waveform zoom/pan + Fit Selection work;
- Edit Master reopens the original source with committed trim;
- local build and requested automated tests passed on the user's machine.

Important Gate 2 contract remains:
- native `LooperEngine` owns authoritative Master PCM + sample-frame playhead;
- one shared sample clock;
- Looper monitor remains separate from Program/VB-CABLE;
- Pause preserves exact position; Stop returns frame 0;
- imported source safety limit remains 10 minutes.

## Gate 3 — First Mic Master + processed Record Tap

Status: **USER ACCEPTED — 2026-08-28**

Accepted implementation includes:
- native ABI **11** dedicated Looper Record Tap boundary;
- `gb_looper_record_start`, `gb_looper_record_stop`, `gb_looper_record_read`, and `gb_looper_record_get_state`;
- recording from the currently active GrassiBoard microphone source;
- Record Tap after current Voice FX/Pitch/Formant processing and before Program Mic Mute;
- Program Mic Mute does not destroy an intentional Looper take;
- no delayed live microphone monitoring during recording;
- microphone-source change during an active Take fails safe/discards instead of combining two inputs;
- engine stop during an active Take fails safe/discards the partial Take;
- Stop hands the recorded first Take into the same reusable trim/zoom/seek editor used by imported Master audio;
- first recorded Master is trimmed/auditioned before committing it as Master;
- recording safety ceiling remains **10 minutes / 28,800,000 frames at 48 kHz**;
- Gate 3 native/managed local deterministic validation exists in `tools/Test-GrassiLooperGate3Local.ps1`.

Accepted real Windows result:
- local Gate 3 validation completed successfully on the user's machine;
- first Master can be recorded directly from the microphone;
- imported/audio Master path still works;
- the existing zoom + seek tooling makes precise loop-boundary selection practical and accurate;
- user explicitly reported the Gate as working strongly and approved it.

## Current Gate

### Gate 4 — Child layers + One Cycle / Loop Replace / Overdub

Status: **UNLOCKED / READY TO IMPLEMENT**

Gate 5 remains **LOCKED** until the user explicitly accepts Gate 4.

Gate 4 must implement child Track/layer recording against the existing authoritative Master clock, including the planned recording modes:
- **One Cycle**;
- **Loop Replace**;
- **Overdub**;
- dependent-track length remains locked to the Master loop length;
- existing Program/VB-CABLE, microphone, Voice FX, Soundboard, Media and Remote paths must not regress.

## Gate sequence

```text
Gate 0  Freeze v1.3 baseline                         USER ACCEPTED
Gate 1  UI + project + waveform                     USER ACCEPTED
Gate 2  Master Loop + transport + local monitor     USER ACCEPTED
Gate 3  First Mic Master + processed Record Tap     USER ACCEPTED
Gate 4  Child layers + recording modes              CURRENT / READY TO IMPLEMENT
Gate 5  Record alignment / latency compensation     LOCKED
Gate 6  Voice FX snapshots + session restore        LOCKED
Gate 7  Track editor + mixer polish                 LOCKED
Gate 8  Persistence + Project Library + DAW ZIP     LOCKED
Gate 9  Regression + soak + final acceptance        LOCKED
```

## Product decisions already scheduled

### Project Library — Gate 8

Gate 8 must provide a persistent multi-project library/recent-projects workflow with stable project identity, safe save-before-switch behavior, reopen support, explicit missing/corrupt asset states, and explicit deletion. Until Gate 8, `New Project` remains a destructive in-memory development reset.
