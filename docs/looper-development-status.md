# GrassiLooper development status

> Authoritative short handoff for GrassiLooper work. Read this file first, then the active Gate in `docs/looper-roadmap.md`.

## Current baseline

- GrassiBoard v1.3 feature baseline: **USER ACCEPTED / PERSONAL-STABLE**.
- Frozen Gate 0 main commit: `819b1e3449b91b9502886fda997eca70764a37ac`.
- Long-lived integration branch: `feature/grassilooper-v1.4`.
- Native production ABI entering Looper work: **10**.
- Program microphone route remains **external VB-CABLE**; Looper must not replace or alter it.

## Gate 0 — Freeze v1.3 baseline

Status: **USER ACCEPTED — 2026-08-26**

## Gate 1 — UI foundation + project model + reusable waveform

Status: **USER ACCEPTED — 2026-08-26**

Accepted implementation includes dedicated Looper workspace, in-memory project/Master/Track model foundation, reusable `WaveformView`, off-UI-thread WAV/MP3 decode + waveform analysis at 48 kHz stereo, imported-Master START/END trim editor, and Master-length lock after dependent tracks exist.

Gate 1 implementation commit: `fc342b40e989f12a6a40749a4a19af3154f7fad3`.

## Current Gate

### Gate 2 — Master Loop engine + transport + local monitor

Status: **HARDENING IMPLEMENTED / LOCAL USER TEST NEXT / CI RE-RUN PENDING**

Gate 3 remains **LOCKED** until this hardened Gate 2 build is manually tested on Windows, CI is green for the hardened source, and the user explicitly accepts Gate 2.

Gate 2 implementation commit: `8d8169634cd7c4e34452ef317678043caebe4847`.
Gate 2 build-fix commits: `cedc753ea516f7e9015f224912985eea29564ef5`, `b355ff2ff782900adfe5188c99ff8fad2b8e7a6d`.

### Gate 2 implementation contract

- native `LooperEngine` owns the authoritative Master PCM buffer and sample-frame playhead;
- one shared Looper clock advances exactly once per Program render frame;
- Looper PCM is written only to a dedicated bounded monitor tap and is not added to Program/VB-CABLE output;
- Play starts/resumes from the current playhead;
- Pause freezes the exact playhead and drains stale monitor PCM;
- Stop returns the playhead exactly to frame `0`;
- loop wrap is modulo the Master frame count with no intentional gap;
- a 32-frame seam fade reduces arbitrary trim-boundary clicks without changing loop length;
- local Looper monitoring uses the shared GrassiBoard monitor endpoint through a separate WASAPI shared-mode sink;
- imported-Master editor has one Play/Pause audition control for START → END;
- changing START/END resets audition to the new START with a short debounce to avoid PCM copy storms;
- Master Play/Pause/Stop and playhead visualization are wired to native state;
- leaving the Looper workspace stops Looper playback.

### Gate 2 UX hardening from real Windows test — 2026-08-27

The first real Gate 2 Windows test confirmed that selected-range audition and the committed Master play without growing gap or delay once the correct local monitor endpoint is selected. That test also exposed four Master-authoring UX requirements which are now part of Gate 2 hardening rather than being deferred:

1. **Shared Monitor Output selector** — Looper now exposes the same `SelectedMonitorOutput` used by Media Deck. Changing either UI changes the same persisted MainViewModel setting.
2. **Seekable audition playhead** — clicking/dragging the white playhead inside the imported-Master editor performs a real native sample-clock seek, allowing the user to jump near END and judge the END → START seam.
3. **Waveform zoom + pan** — the reusable `WaveformView` supports mouse-wheel zoom, explicit +/- controls, Fit Selection, Shift+drag pan and middle-drag pan. Imported source analysis uses a denser editor envelope so zoom reveals useful timing detail instead of merely scaling the old 2,048-bucket overview.
4. **Edit Master** — before child tracks exist, `Edit Master` reopens the original source using the currently committed SourceStartFrame/SourceEndFrame. Cancel is non-destructive; Apply rebuilds the Master with the new trim. This follows the roadmap contract that Master trim is freely editable before dependent tracks exist.

The native seek operation preserves Play/Pause state semantics, clears stale Looper monitor PCM, and rejects positions outside the current loop frame count.

### Native API / ABI decision

Gate 2 remains an additive **ABI version 10** branch extension. The hardening adds:

```text
gb_looper_load_master
gb_looper_clear
gb_looper_set_transport
gb_looper_seek
gb_looper_get_state
gb_looper_monitor_read
```

ABI 11 remains reserved for the later recording boundary.

### Safety baseline

Imported source files remain limited to **10 minutes**. At 48,000 Hz stereo 32-bit float, one full 10-minute PCM copy is 230,400,000 bytes (~219.7 MiB); managed Master + native Master is ~439.5 MiB steady PCM storage, with additional temporary import memory possible during analysis/commit.

The roadmap's **30–60 minute** Gate 2 requirement refers to continuous playback/soak duration, not a 30–60 minute Master source.

### Automated verification history

GitHub Actions **Build #100** / run `33054680743` on pre-hardening code commit `b355ff2ff782900adfe5188c99ff8fad2b8e7a6d`: **SUCCESS**.

- Remote Web install/generate: PASS
- native configure/build: PASS
- native tests: 9/9 PASS, including `GrassiBoard.AudioEngine.LooperGate2Test`
- managed smoke tests: PASS
- WPF publish: PASS
- portable + installer verification: PASS

The hardened source requires a fresh CI run. Local Windows testing may proceed immediately from the branch using `tools/Build-LocalRemoteTest.ps1 -Run`; the user does not need to wait for or download a GitHub artifact for iterative UX testing.

### Hardened automated coverage

Native deterministic coverage now also checks exact paused seek, stale monitor clearing after seek, resume from the sought frame, and rejection of frame == loop length. Managed smoke additionally checks seek wiring, zoom/editor controls, Edit Master source contract, and shared Monitor Output wiring.

## Gate 2 manual acceptance checklist

```text
[ ] Start normal GrassiBoard audio engine
[ ] Import WAV and MP3
[ ] Shared Looper Monitor Output changes the same setting as Media Deck
[ ] Drag START / END and use Play selection
[ ] Click/drag playhead near END and confirm audio starts from that exact region
[ ] Audition wraps END → START with no growing gap
[ ] Mouse-wheel / +/- zoom works and reveals useful waveform timing detail
[ ] Shift+drag or middle-drag pans a zoomed waveform
[ ] Fit Selection frames the current trim
[ ] Set selection as Master
[ ] Edit Master reopens the same source with previous START / END restored
[ ] Cancel Edit Master leaves current Master unchanged
[ ] Apply Master Changes updates Master without re-importing manually
[ ] Master Play loops continuously with no growing gap
[ ] Pause 5–10 seconds then Play resumes from same playhead
[ ] Stop returns exactly to beginning
[ ] Leaving Looper stops playback
[ ] 30–60 minute continuous short-loop soak shows no growing delay/drift
[ ] Windows Mic / Phone Mic / Voice FX / Pitch / Formant still work
[ ] Soundboard / Media Deck / Remote Control / Remote Monitor still work
[ ] Program/VB-CABLE output remains unchanged
```

## Product decisions already scheduled

### Project Library — Gate 8

Gate 8 must provide a persistent multi-project library/recent-projects workflow with stable project identity, safe save-before-switch behavior, reopen support, explicit missing/corrupt asset states, and explicit deletion. Until Gate 8, `New Project` remains a destructive in-memory development reset.

## Gate sequence

```text
Gate 0  Freeze v1.3 baseline                         USER ACCEPTED
Gate 1  UI + project + waveform                     USER ACCEPTED
Gate 2  Master Loop + transport + local monitor     CURRENT / HARDENING / USER TEST PENDING
Gate 3  First Mic Master + processed Record Tap     LOCKED
Gate 4  Child layers + recording modes              LOCKED
Gate 5  Record alignment / latency compensation     LOCKED
Gate 6  Voice FX snapshots + session restore        LOCKED
Gate 7  Track editor + mixer polish                 LOCKED
Gate 8  Persistence + Project Library + DAW ZIP     LOCKED
Gate 9  Regression + soak + final acceptance        LOCKED
```