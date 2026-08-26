# GrassiLooper development status

> Authoritative short handoff for GrassiLooper work. Read this file first in every new development session, then read only the relevant Gate in `docs/looper-roadmap.md`.

## Current baseline

- GrassiBoard feature baseline: **v1.3.0 Full Remote Audio / Remote Phone Microphone — USER ACCEPTED / PERSONAL-STABLE by real local Windows + Android testing**
- Current native ABI in source: **10**
- Program microphone route: **external VB-CABLE, unchanged**
- Final packaged installer after the last v1.3 commit: **not separately manual clean-install verified**
- This distinction must remain explicit; feature acceptance does not imply installer-verification acceptance.

## GrassiLooper roadmap

- Source of truth: `docs/looper-roadmap.md`
- Target release family: **v1.4.x**
- Development model: **Gate-based**
- The numbered roadmap sections are requirements/design rules inside the Gates; they are **not 98 independent implementation stages**.

## Current Gate

### Gate 0 — Freeze/document v1.3 baseline

Status: **READY / NOT YET COMPLETED**

Completed before Gate 0:

- [x] GrassiLooper product concept agreed
- [x] Imported-audio Master workflow agreed
- [x] Empty-project / first-microphone Master workflow agreed
- [x] Shared sample-accurate Master Loop concept agreed
- [x] Reusable WPF waveform/component architecture agreed
- [x] Master Trim vs child Track Trim semantics agreed
- [x] Shared transport behavior agreed
- [x] No live microphone self-monitoring for MVP agreed
- [x] Three recording modes agreed: One Cycle / Loop Replace / Overdub
- [x] Loop Replace 12-second-on-8-second reference behavior agreed
- [x] Same existing Voice FX engine will be reused
- [x] Per-Track Voice FX snapshots agreed
- [x] Pre-Looper Voice state restore agreed
- [x] Dry source / non-destructive Track FX deferred to future architecture
- [x] Extend / multiply Master cycle deferred to future architecture
- [x] DAW-ready aligned stem ZIP export agreed
- [x] Master roadmap committed to repository
- [x] Looper status tracker created

Gate 0 remaining work:

- [ ] Update `docs/current-status.md` to record v1.3 USER ACCEPTED / PERSONAL-STABLE
- [ ] Update `docs/remote-development-status.md` to reflect completed real-device v1.3 acceptance
- [ ] Update `CHANGELOG.md` if appropriate
- [ ] Preserve explicit note that the final packaged installer itself was not separately manual clean-install verified
- [ ] Record/freeze the v1.3 baseline commit and then unlock Gate 1

## Next Gate

### Gate 1 — UI foundation + project model + reusable waveform architecture

Status: **LOCKED UNTIL GATE 0 COMPLETES**

Planned scope:

- Looper navigation page
- empty-project screen
- project model/store
- reusable `WaveformView`
- `WaveformAnalysisService`
- componentized Track row architecture
- imported Master audio
- large Master trim editor
- Set As Master workflow

No multi-track recording engine belongs in Gate 1.

## Gate sequence

```text
Gate 0  Freeze v1.3 baseline
Gate 1  UI + project + waveform
Gate 2  Master Loop + transport + local monitor
Gate 3  First Mic Master + processed Record Tap / ABI
Gate 4  Child layers + One Cycle / Replace / Overdub
Gate 5  Record alignment / latency compensation
Gate 6  Voice FX snapshots + Looper session restore
Gate 7  Track editor + mixer polish
Gate 8  Persistence + DAW-ready ZIP export
Gate 9  Regression + soak + final user acceptance
```

## Working rule

After every implementation iteration, update this file with:

- current Gate;
- what was implemented;
- automated-test result;
- what the user still needs to test;
- known issues/hotfixes;
- explicit user acceptance when received;
- next permitted Gate.

Do not rewrite the long roadmap for ordinary progress tracking.
