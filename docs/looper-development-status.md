# GrassiLooper development status

> Authoritative short handoff for GrassiLooper work. Read this file first, then the active Gate in `docs/looper-roadmap.md`.

## Current baseline

- GrassiBoard v1.3 feature baseline: **USER ACCEPTED / PERSONAL-STABLE**.
- Frozen Gate 0 main commit: `819b1e3449b91b9502886fda997eca70764a37ac`.
- Long-lived integration branch: `feature/grassilooper-v1.4`.
- Native production ABI entering Looper work: **10**.
- Program microphone route remains **external VB-CABLE**; Looper must not replace or alter it.
- The final v1.3 packaged installer was not separately manual clean-install verified; Looper work does not retroactively change that distinction.

## Gate 0 — Freeze v1.3 baseline

Status: **USER ACCEPTED — 2026-08-26**

## Gate 1 — UI foundation + project model + reusable waveform

Status: **USER ACCEPTED — 2026-08-26**

Accepted implementation includes:

- dedicated Looper workspace;
- in-memory project/Master/Track model foundation;
- reusable `WaveformView` based on `FrameworkElement` + `DrawingContext`;
- off-UI-thread WAV/MP3 decode + waveform analysis at 48 kHz stereo;
- imported-Master START/END trim editor;
- sample-defined Master frame count and Master-length lock after dependent tracks exist;
- real Windows UI test reported smooth waveform interaction and no regression in the requested existing GrassiBoard paths.

Gate 1 implementation commit: `fc342b40e989f12a6a40749a4a19af3154f7fad3`.

## Current Gate

### Gate 2 — Master Loop engine + transport + local monitor

Status: **IMPLEMENTED IN SOURCE / AUTOMATED VERIFICATION PENDING / USER TEST PENDING**

Gate 3 remains **LOCKED** until Gate 2 is CI-green, manually tested on Windows, and explicitly accepted by the user.

### Gate 2 implementation contract

Implemented in this iteration:

- native `LooperEngine` owns the authoritative Master PCM buffer and sample-frame playhead;
- one shared Looper clock advances exactly once per existing Program render frame;
- Looper PCM is written only to a dedicated bounded monitor tap and is **not** added to Program/VB-CABLE output;
- Play starts/resumes from the current playhead;
- Pause publishes the paused state, waits out any in-flight realtime frame, drains stale monitor PCM, and preserves the exact playhead;
- Stop publishes stopped state, waits out any in-flight realtime frame, drains monitor PCM, and returns the playhead exactly to frame `0`;
- loop wrap is modulo the Master frame count with no intentional gap;
- a 32-frame seam fade is applied only to sufficiently long loops to reduce arbitrary trim-boundary clicks without changing loop length;
- local Looper monitoring uses the selected GrassiBoard monitor endpoint through a separate WASAPI shared-mode sink;
- the local monitor follows the native clock through a bounded prebuffer plus small drop/duplicate drift correction; these corrections never move the authoritative project playhead;
- imported-Master editor has one Play/Pause audition control for the current START → END selection;
- changing START/END resets audition to the new START; while dragging, native re-copy is debounced briefly to avoid large PCM copy storms;
- Master Play/Pause/Stop and playhead visualization are wired to native state;
- leaving the Looper workspace stops Looper playback;
- microphone recording, record tap, Tracks and recording modes remain intentionally absent until later Gates.

### Native API / ABI decision

Gate 2 adds Looper functions to the existing native boundary while retaining **ABI version 10** for this branch. ABI 11 remains reserved for the later recording boundary when public recording functions actually require the progression described by the roadmap.

Added Gate 2 native API family:

```text
gb_looper_load_master
gb_looper_clear
gb_looper_set_transport
gb_looper_get_state
gb_looper_monitor_read
```

### Safety baseline

The existing imported-audio service already limits source files to **10 minutes**, so Gate 2 keeps that limit rather than silently widening it.

At 48,000 Hz, stereo, 32-bit float:

```text
10-minute Master frames         28,800,000
one Master PCM copy             230,400,000 bytes
one Master PCM copy             ~219.7 MiB
managed Master + native Master  ~439.5 MiB steady PCM storage
```

A full-length import can temporarily require more memory while the original decoded import, selected managed Master, and native Master copy overlap before garbage collection. This is why 10 minutes is an initial hard safety ceiling, not a promise to increase loop length during Gate 2.

The roadmap's **30–60 minute** Gate 2 requirement refers to continuous playback/soak duration, not a 30–60 minute Master loop.

### Automated coverage added

Native deterministic Gate 2 tests cover:

- exact PCM modulo wrap;
- Pause freezing the exact sample frame;
- resume from the paused frame;
- Stop returning exactly to frame `0`;
- one-hour-equivalent modulo clock arithmetic without wall-clock timing;
- one minute worth of the real `RenderFrame` path as a CPU benchmark;
- 10-minute memory-size arithmetic;
- rejection of an oversized Master;
- clear/reset semantics.

Managed smoke coverage checks:

- managed/native Looper state layout;
- 10-minute memory safety constant;
- transport/audition XAML wiring;
- frame-zero playhead rendering contract;
- native Looper API/source-clock integration contract.

CI result for this implementation: **PENDING**.

## Gate 2 manual acceptance checklist

After CI is green, real Windows testing must verify:

```text
[ ] Start the normal GrassiBoard audio engine
[ ] Import WAV and MP3
[ ] Drag START / END and use Play selection
[ ] Audition loops only START → END
[ ] Pause/resume audition behaves correctly
[ ] Changing START/END while auditioning returns to the new START
[ ] Set selection as Master
[ ] Master Play loops continuously with no growing gap
[ ] Listen specifically for seam clicks on arbitrary trim points
[ ] Pause 5–10 seconds, then Play resumes from the same playhead
[ ] Stop returns the playhead exactly to the beginning
[ ] Local audio comes from the selected Monitor Output
[ ] Leaving Looper stops its playback
[ ] 30–60 minute continuous short-loop soak shows no growing delay/drift
[ ] Windows Mic still works
[ ] Phone Mic still works
[ ] Voice FX / Pitch / Formant still work
[ ] Soundboard still works
[ ] Media Deck still works
[ ] Remote Control still works
[ ] Remote Monitor still works
[ ] Program/VB-CABLE output remains unchanged
```

## Product decisions already scheduled

### Project Library — Gate 8

Gate 8 must provide a persistent multi-project library/recent-projects workflow with stable project identity, safe save-before-switch behavior, reopen support, explicit missing/corrupt asset states, and explicit deletion. Until Gate 8, `New Project` remains a destructive in-memory development reset.

## Gate sequence

```text
Gate 0  Freeze v1.3 baseline                         USER ACCEPTED
Gate 1  UI + project + waveform                     USER ACCEPTED
Gate 2  Master Loop + transport + local monitor     CURRENT / CI PENDING
Gate 3  First Mic Master + processed Record Tap     LOCKED
Gate 4  Child layers + recording modes              LOCKED
Gate 5  Record alignment / latency compensation     LOCKED
Gate 6  Voice FX snapshots + session restore        LOCKED
Gate 7  Track editor + mixer polish                 LOCKED
Gate 8  Persistence + Project Library + DAW ZIP     LOCKED
Gate 9  Regression + soak + final acceptance        LOCKED
```
