# GrassiLooper development status

> Authoritative short handoff for GrassiLooper work. Read this file first in every new development session, then read only the relevant Gate in `docs/looper-roadmap.md`.

## Current baseline

- GrassiBoard feature baseline: **v1.3.0 Full Remote Audio / Remote Phone Microphone — USER ACCEPTED / PERSONAL-STABLE by real local Windows + Android testing**
- Frozen v1.3 implementation commit: `f27b39055971da14bb8fa3753f28d5b690757a8f`
- Gate 0 freeze commit on `main`: `819b1e3449b91b9502886fda997eca70764a37ac`
- GrassiLooper development branch: `feature/grassilooper-v1.4`
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
- Long-lived integration branch: `feature/grassilooper-v1.4`
- `main` remains frozen until all nine implementation/acceptance Gates complete and the final merge is explicitly approved.

## Gate 0 — Freeze/document v1.3 baseline

Status: **USER ACCEPTED — 2026-08-26**

- [x] `docs/current-status.md` records the real v1.3 USER ACCEPTED / PERSONAL-STABLE feature baseline.
- [x] `docs/remote-development-status.md` records completed real-device v1.3 acceptance.
- [x] Feature acceptance is explicitly separated from the unverified final CI/package/installer state.
- [x] v1.3 implementation baseline frozen at `f27b39055971da14bb8fa3753f28d5b690757a8f`.
- [x] Freeze committed to `main` as `819b1e3449b91b9502886fda997eca70764a37ac`.
- [x] User explicitly approved Gate 0 on 2026-08-26.

Known baseline caveats carried forward:

- final v1.3 packaged installer not separately manual clean-install verified;
- final v1.3 GitHub Actions run is not fully green because managed smoke tests failed;
- native engine source version string still reports `1.3.0-gate2` even though the accepted feature baseline is v1.3.0.

## Current Gate

### Gate 1 — UI foundation + project model + reusable waveform architecture

Status: **IMPLEMENTED / AUTOMATED CI PENDING**

Implemented in this iteration:

- [x] dedicated Looper workspace added to desktop navigation without changing native audio routing;
- [x] empty-project UX with Import Audio and deliberately locked Record First Loop action;
- [x] `LooperProjectModel`, `LooperMasterModel`, `LooperTrackModel`, and in-memory `LooperProjectStore` foundation;
- [x] reusable `WaveformEnvelope` plus `WaveformAnalysisService` running decode/resample/envelope work outside the UI thread;
- [x] reusable `WaveformView` implemented as a `FrameworkElement` using `DrawingContext` / `OnRender`, not thousands of XAML shapes;
- [x] reusable `LooperTrackRow` component created for future child layers;
- [x] WAV/MP3 imported-Master path at 48 kHz stereo;
- [x] large waveform trim editor with draggable START / END handles;
- [x] Set As Master workflow copies the selected sample region and establishes the sample-defined Master frame count;
- [x] Master redefinition is blocked once dependent child tracks exist;
- [x] deterministic Gate 1 smoke coverage added for envelope analysis, WAV import/resample contract, sample-defined Master storage, and Master-length lock semantics.

Deliberately not implemented in Gate 1:

- transport/sample clock;
- Master playback/local monitor;
- microphone recording;
- native ABI 11;
- child-track recording;
- One Cycle / Loop Replace / Overdub;
- persistence/export.

Automated verification still required:

```text
[ ] Windows x64 GitHub Actions build
[ ] Native regression tests
[ ] Managed smoke tests including Looper Gate 1 module initializer
[ ] Self-contained publish/package verification if CI reaches packaging
```

Manual Gate 1 test after CI is green:

```text
[ ] Looper appears between Mixer and Routing
[ ] Existing Board / Voice / Mixer / Routing / Settings navigation still works
[ ] Empty Looper project shows Import Audio + disabled Record First Loop
[ ] Import a WAV and an MP3
[ ] Waveform renders without UI freeze
[ ] START / END handles drag correctly
[ ] Selection time label follows trim
[ ] Set As Master produces the selected Master only
[ ] Master duration/frame count look correct
[ ] Import a replacement Master before child tracks exist
[ ] Existing Windows Mic / Phone Mic / Voice FX / Soundboard / Media / Remote / VB-CABLE behavior is unchanged
```

## Next Gate

### Gate 2 — Master Loop + transport + local monitor

Status: **LOCKED UNTIL GATE 1 IS CI-GREEN AND EXPLICITLY USER ACCEPTED**

No Gate 2 implementation is permitted yet.

## Gate sequence

```text
Gate 0  Freeze v1.3 baseline                         USER ACCEPTED
Gate 1  UI + project + waveform                     CURRENT
Gate 2  Master Loop + transport + local monitor     LOCKED
Gate 3  First Mic Master + processed Record Tap     LOCKED
Gate 4  Child layers + recording modes              LOCKED
Gate 5  Record alignment / latency compensation     LOCKED
Gate 6  Voice FX snapshots + session restore        LOCKED
Gate 7  Track editor + mixer polish                 LOCKED
Gate 8  Persistence + DAW-ready ZIP export          LOCKED
Gate 9  Regression + soak + final acceptance        LOCKED
```

## Working rule

After every implementation iteration, update this file with current Gate, implementation result, automated-test result, required user test, known issues/hotfixes, explicit acceptance, and next permitted Gate.
