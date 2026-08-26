# GrassiLooper development status

> Authoritative short handoff for GrassiLooper work. Read this file first in every new development session, then read only the relevant Gate in `docs/looper-roadmap.md`.

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
- This distinction remains part of the frozen baseline; GrassiLooper work does not retroactively claim a manual v1.3 clean-install verification.

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
- [x] Feature acceptance is explicitly separated from the unverified final v1.3 package/installer state.
- [x] v1.3 implementation baseline frozen at `f27b39055971da14bb8fa3753f28d5b690757a8f`.
- [x] Freeze committed to `main` as `819b1e3449b91b9502886fda997eca70764a37ac`.
- [x] User explicitly approved Gate 0 on 2026-08-26.

Known baseline caveats carried forward:

- final v1.3 packaged installer was not separately manual clean-install verified;
- native engine source version string still reports `1.3.0-gate2` even though the accepted feature baseline is v1.3.0.

## Current Gate

### Gate 1 — UI foundation + project model + reusable waveform architecture

Status: **IMPLEMENTED / CI GREEN / AWAITING USER MANUAL ACCEPTANCE**

Implemented:

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
- [x] deterministic Gate 1 smoke coverage for waveform envelope analysis, WAV import/resample, sample-defined Master storage, and Master-length lock semantics.

Deliberately not implemented in Gate 1:

- transport/sample clock;
- Master playback/local monitor;
- microphone recording;
- native ABI 11;
- child-track recording;
- One Cycle / Loop Replace / Overdub;
- persistence/export.

### Gate 1 CI/hotfix history

Initial Gate 1 implementation commit:

`fc342b40e989f12a6a40749a4a19af3154f7fad3` — `feat(looper): implement Gate 1 UI and waveform foundation`

Build #90 / run `32950454747` proved RemoteWeb + native configure/build + **8/8 native tests** were green, but managed restore was blocked by inherited NuGet security auditing for `SIPSorcery 10.0.13`.

Gate 1 fixed the inherited build debt without suppressing security warnings:

- [x] SIPSorcery moved from `10.0.13` to **`10.0.15`**, the smallest security patch selected for this branch;
- [x] Gate 1 smoke parses the real project XML and requires the actual SIPSorcery PackageReference to remain exactly `10.0.15`;
- [x] the retired `README-FIRST.txt` portable-package guide was restored so package creation and package verification share the same explicit contract;
- [x] `Package-Milestone.ps1` now handles the legacy guide defensively while CI verification still requires it in the produced portable package.

### Gate 1 final automated verification

GitHub Actions **Build #95** / run `32954855982` for branch head after the Gate 1 hotfixes:

```text
Remote Web dependency install     PASS
Remote Web static generation      PASS
Native configure                  PASS
Native build                      PASS
Native tests                      PASS — 8/8
Managed smoke tests               PASS
Looper Gate 1 module smoke        PASS (inside managed smoke)
Self-contained WPF publish        PASS
Native output staging             PASS
Portable/symbol/test packaging    PASS
Portable package verification     PASS
Single-file installer publish     PASS
Installer contract verification   PASS
Portable artifact upload          PASS
Symbols artifact upload           PASS
Installer artifact upload         PASS
Test-results artifact upload      PASS
```

CI result: **SUCCESS**.

The dependency/package hotfixes change no Looper realtime engine, Voice DSP, Phone Mic PCM bridge, Remote Monitor mix, Soundboard, Media, or VB-CABLE routing. Because SIPSorcery changed from 10.0.13 to 10.0.15, real Remote Monitor + Phone Mic regression remains part of the manual Gate 1 acceptance below.

### Manual Gate 1 acceptance required

Build/run the current `feature/grassilooper-v1.4` branch with:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Build-LocalRemoteTest.ps1 -Run -RunSmokeTests
```

Then verify:

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
[ ] Windows physical Mic still works
[ ] Phone Mic still captures/routes through the accepted path
[ ] Voice FX/Pitch/Formant still work for the active microphone source
[ ] Soundboard still works
[ ] Media Deck still works
[ ] Remote Control still works
[ ] Remote Monitor still starts and remains clean
[ ] Program/VB-CABLE output remains unchanged
```

Gate 1 is not accepted until the user explicitly confirms the real Windows/Android result.

## Next Gate

### Gate 2 — Master Loop + transport + local monitor

Status: **LOCKED UNTIL GATE 1 IS EXPLICITLY USER ACCEPTED**

No Gate 2 implementation is permitted yet.

## Gate sequence

```text
Gate 0  Freeze v1.3 baseline                         USER ACCEPTED
Gate 1  UI + project + waveform                     CI GREEN / USER TEST PENDING
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
