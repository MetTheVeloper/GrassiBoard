# GrassiLooper development status

> Authoritative short handoff for GrassiLooper work. Read this file first in every new development session, then read only the relevant Gate in `docs/looper-roadmap.md`.

## Current baseline

- GrassiBoard feature baseline: **v1.3.0 Full Remote Audio / Remote Phone Microphone — USER ACCEPTED / PERSONAL-STABLE by real local Windows + Android testing**
- Frozen v1.3 implementation commit: `f27b39055971da14bb8fa3753f28d5b690757a8f`
- Current native ABI in source: **10**
- Native engine source version string at the frozen baseline: `1.3.0-gate2`
- Program microphone route: **external VB-CABLE, unchanged**
- Final packaged installer after the last v1.3 implementation commit: **not separately manual clean-install verified**
- GitHub Actions Build #86 for the v1.3 implementation passed native configure/build/tests but failed managed smoke tests; publish/package/installer steps were skipped.
- This distinction is part of the frozen baseline: feature acceptance does not imply CI/package/installer verification.

## GrassiLooper roadmap

- Source of truth: `docs/looper-roadmap.md`
- Target release family: **v1.4.x**
- Development model: **Gate-based**
- The numbered roadmap sections are requirements/design rules inside the Gates; they are **not 98 independent implementation stages**.

## Current Gate

### Gate 0 — Freeze/document v1.3 baseline

Status: **IMPLEMENTED / AWAITING USER CONFIRMATION**

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

Gate 0 implementation result:

- [x] Updated `docs/current-status.md` to record the real v1.3 USER ACCEPTED / PERSONAL-STABLE feature baseline.
- [x] Updated `docs/remote-development-status.md` to reflect completed real-device v1.3 acceptance.
- [x] Preserved the explicit distinction between accepted feature behavior and unverified final package/installer state.
- [x] Recorded/froze the v1.3 implementation baseline at commit `f27b39055971da14bb8fa3753f28d5b690757a8f`.
- [x] Reviewed `CHANGELOG.md`; no v1.3 release-promotion entry is added in Gate 0 because the final v1.3 CI/package/installer gate is not verified. The authoritative personal-stable state is recorded in the status documents instead.

## Gate 0 verification

This iteration is documentation-only. It changes no C#, C++, XAML, RemoteWeb, native ABI, build script, routing, DSP, Soundboard, Media, Remote, or VB-CABLE runtime behavior.

Baseline verification recorded from the frozen v1.3 implementation:

```text
GitHub Actions Build #86 / run 31789622279
Remote Web install/generate     PASS
Native configure               PASS
Native build                   PASS
Native tests                   PASS
Managed smoke tests            FAIL
Publish/package/installer      SKIPPED
```

The real local Windows + Android v1.3 feature path had already been accepted before GrassiLooper planning began. Gate 0 does not claim a new runtime build or a new installer test.

User action required for Gate 0:

```text
[ ] Confirm that this frozen baseline accurately reflects the accepted v1.3 state
[ ] Accept the explicit CI/package/installer caveat
[ ] Explicitly approve Gate 0 before Gate 1 begins
```

Known baseline caveats carried forward:

- final v1.3 packaged installer not separately manual clean-install verified;
- final v1.3 GitHub Actions run is not fully green because managed smoke tests failed;
- native engine source version string still reports `1.3.0-gate2` even though the accepted feature baseline is v1.3.0.

These are frozen facts, not silent regressions introduced by Looper work.

## Next Gate

### Gate 1 — UI foundation + project model + reusable waveform architecture

Status: **LOCKED UNTIL GATE 0 IS EXPLICITLY USER ACCEPTED**

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
