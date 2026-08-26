# GrassiLooper development status

> Authoritative short handoff for GrassiLooper work. Read this file first in every new development session, then read the relevant Gate in `docs/looper-roadmap.md`.

## Current baseline

- GrassiBoard feature baseline: **v1.3.0 Full Remote Audio / Remote Phone Microphone — USER ACCEPTED / PERSONAL-STABLE by real local Windows + Android testing**
- Frozen v1.3 implementation commit: `f27b39055971da14bb8fa3753f28d5b690757a8f`
- Gate 0 freeze commit on `main`: `819b1e3449b91b9502886fda997eca70764a37ac`
- GrassiLooper development branch: `feature/grassilooper-v1.4`
- Long-lived draft integration PR: **#10** — do not merge before all Gates are accepted
- Current native ABI in source: **10**
- Native engine source version string at the frozen baseline: `1.3.0-gate2`
- Program microphone route: **external VB-CABLE, unchanged**
- Final packaged installer after the last v1.3 implementation commit: **not separately manual clean-install verified**

## GrassiLooper roadmap

- Source of truth: `docs/looper-roadmap.md`
- Target release family: **v1.4.x**
- Development model: **Gate-based**
- `main` remains frozen until all nine implementation/acceptance Gates complete and the final merge is explicitly approved.

## Gate 0 — Freeze/document v1.3 baseline

Status: **USER ACCEPTED — 2026-08-26**

- [x] v1.3 feature path frozen/documented as USER ACCEPTED / PERSONAL-STABLE.
- [x] Feature acceptance remains explicitly separated from final installer clean-install verification.
- [x] User explicitly approved Gate 0 on 2026-08-26.

## Gate 1 — UI foundation + project model + reusable waveform architecture

Status: **USER ACCEPTED — 2026-08-26**

Implemented and accepted:

- [x] dedicated Looper workspace between Mixer and Routing;
- [x] existing Board / Voice / Mixer / Routing / Settings navigation preserved;
- [x] empty-project UX with Import Audio and deliberately locked Record First Loop action;
- [x] `LooperProjectModel`, `LooperMasterModel`, `LooperTrackModel`, and in-memory `LooperProjectStore` foundation;
- [x] reusable `WaveformEnvelope` and off-UI-thread `WaveformAnalysisService`;
- [x] reusable efficient `WaveformView` (`FrameworkElement` + `DrawingContext` / `OnRender`);
- [x] reusable `LooperTrackRow` structure for future child layers;
- [x] WAV/MP3 imported-Master path normalized to 48 kHz stereo;
- [x] large waveform trim editor with draggable START / END handles;
- [x] selection label follows trim;
- [x] Set As Master copies only the selected sample region and establishes the sample-defined Master frame count;
- [x] Master redefinition remains allowed before dependent child tracks and becomes locked afterward;
- [x] real Windows UI test reported smooth waveform interaction with no freeze;
- [x] existing Windows Mic / Phone Mic / Voice FX / Soundboard / Media / Remote / Remote Monitor / VB-CABLE regression checklist reported clean by the user.

Gate 1 implementation commit:

`fc342b40e989f12a6a40749a4a19af3154f7fad3` — `feat(looper): implement Gate 1 UI and waveform foundation`

Gate 1 inherited build-debt fixes:

- SIPSorcery updated from `10.0.13` to `10.0.15` rather than suppressing NuGet security auditing;
- legacy package-source smoke bridge added while a stricter XML-aware Gate 1 check verifies the real dependency;
- portable `README-FIRST.txt` contract restored;
- package script made defensive while produced portable package verification remains strict.

Final automated verification before manual acceptance:

- GitHub Actions **Build #95** / run `32954855982`
- Remote Web generate: PASS
- Native build: PASS
- Native tests: **8/8 PASS**
- Managed smoke + Looper Gate 1 smoke: PASS
- WPF publish: PASS
- portable package + verification: PASS
- single-file installer + verification: PASS
- artifact uploads: PASS
- overall result: **SUCCESS**

User manual acceptance notes (2026-08-26):

- imported waveform rendered smoothly without UI freeze;
- trim handles and selection timing behaved correctly;
- selected region became the Master correctly;
- user reported no problems in the requested regression checks.

## Product decisions captured after Gate 1 acceptance

### Imported-Master selection audition

The main roadmap already requires `preview selected region` in the Imported Master path. It was intentionally absent from Gate 1 because Gate 1 contained no playback/monitor engine.

**Scheduled for Gate 2**, together with the Master transport/local-monitor foundation:

- add one Play/Pause control to the large imported-Master trim editor;
- audition only the current START → END selection;
- audition wraps continuously inside that selection;
- changing START or END invalidates the current audition position and returns it to the new selection start;
- if a trim boundary changes while audition is playing, playback restarts from the new selection start rather than continuing from an obsolete offset;
- if paused, the next Play starts from the selection start after a boundary change;
- no separate Stop button is required for this editor audition control.

This preview must reuse the Gate 2 monitor/playback infrastructure rather than creating an unrelated second audio player.

### New Project semantics and multi-project library

Current Gate 1 behavior is deliberately in-memory only: `New Project` calls `LooperProjectStore.Reset()`, replaces `Current` with a fresh project, and the previous in-memory Looper project is no longer retained by the store. The original imported source file on disk is not deleted, but the Looper project state itself is currently discarded because persistence has not been implemented yet.

The roadmap already assigns project autosave + project reopen to Gate 8. The user's requested multi-project workflow is therefore scheduled as an explicit **Gate 8 persistence requirement**:

- persistent Project Library / Recent Projects list;
- each project keeps a stable ID, name, created time, and modified time;
- `New Project` must safely save the current dirty project before switching to a fresh project;
- creating a new project must not overwrite or silently discard the previous saved project;
- saved projects can be selected and reopened later;
- missing/corrupt asset states remain explicit rather than silently losing project data;
- project deletion, when added, must be an explicit destructive action rather than a side effect of `New Project`.

A full version-history/revision system is **not** required for the v1.4 MVP; the requirement is a library of multiple independently saved Looper projects that can be reopened.

Reason for Gate 8 placement: Tracks, Voice FX snapshots, trim/mixer metadata, and asset ownership are still evolving through Gates 3–7. Freezing the durable project schema before those structures exist would create avoidable migration/rewrite risk. Gate 8 is the correct point to make the complete project format durable.

Until Gate 8, `New Project` remains a disposable in-memory reset and should be treated as destructive during development testing.

## Current Gate

### Gate 2 — Master Loop engine + transport + local monitor

Status: **UNLOCKED / READY FOR IMPLEMENTATION**

Gate 2 scope from the roadmap:

- sample-accurate Master buffer;
- shared Looper playhead;
- Play / Pause / Stop;
- gapless loop wrap;
- Looper local monitor output;
- playhead visualization;
- Master length lock contract;
- memory/performance benchmark and initial supported Loop-size safety limits;
- **Imported-Master START/END selection Play/Pause audition described above.**

Gate 3 recording work remains locked until Gate 2 is implemented, CI-green, manually tested, and explicitly accepted.

## Gate sequence

```text
Gate 0  Freeze v1.3 baseline                         USER ACCEPTED
Gate 1  UI + project + waveform                     USER ACCEPTED
Gate 2  Master Loop + transport + local monitor     CURRENT / UNLOCKED
Gate 3  First Mic Master + processed Record Tap     LOCKED
Gate 4  Child layers + recording modes              LOCKED
Gate 5  Record alignment / latency compensation     LOCKED
Gate 6  Voice FX snapshots + session restore        LOCKED
Gate 7  Track editor + mixer polish                 LOCKED
Gate 8  Persistence + Project Library + DAW ZIP     LOCKED
Gate 9  Regression + soak + final acceptance        LOCKED
```

## Working rule

After every implementation iteration, update this file with current Gate, implementation result, automated-test result, required user test, known issues/hotfixes, explicit acceptance, and next permitted Gate.
