# Current status

## Accepted product baseline

- Version: `v1.3.0` feature baseline — **USER ACCEPTED / PERSONAL-STABLE**
- v1.3 implementation commit: `f27b39055971da14bb8fa3753f28d5b690757a8f` — `feat(remote): finalize v1.3.0 phone microphone input`
- Target: Windows 10/11 x64
- Native ABI in the accepted v1.3 source: **10**
- Native engine source version string at the frozen baseline: `1.3.0-gate2`
- Program microphone route: **external VB-CABLE, unchanged**
- Remote stack: GrassiMote Remote Control + independent Remote Monitor + Remote Phone Microphone
- Physical Windows microphone, Voice FX, Pitch/Fine Pitch/Formant, Mixer/dynamics, Soundboard, Media Deck, Profiles/Presets, Hotkeys/Tray, Remote Control, Remote Monitor, and VB-CABLE remain the regression baseline for GrassiLooper work.

## v1.3 acceptance state

The real Windows + Android feature path is accepted for the user's current private/personal workflow. The accepted path includes Remote Phone Microphone feeding the existing GrassiBoard Voice/Mixer pipeline while preserving the existing Remote Control/Remote Monitor and external VB-CABLE Program route.

This acceptance is deliberately **feature-path acceptance**, not blanket package certification.

### Packaging / CI distinction

The final packaged installer after the v1.3 implementation commit was **not separately manual clean-install verified**.

GitHub Actions Build #86 / run `31789622279` for commit `f27b3905` configured and built the native engine successfully and passed the native tests, but failed later in the managed smoke-test step. Publish/package/installer steps were therefore skipped in that run. The corresponding Release workflow also failed.

Do not describe v1.3 as CI/package verified until a later packaging gate proves that separately.

## Accepted Remote history

- `v1.1.0` GrassiMote Remote Control — USER ACCEPTED / CI VERIFIED.
- `v1.2.0` Remote Monitor — USER ACCEPTED / PERSONAL-STABLE; frozen at commit `c3cf4da1a65a7f97314c265ab581dc9694d1b631`, native ABI 9.
- `v1.3.0` Remote Phone Microphone / Full Remote Audio — USER ACCEPTED / PERSONAL-STABLE for the real local Windows + Android feature path; implementation commit `f27b39055971da14bb8fa3753f28d5b690757a8f`, native ABI 10.

See `docs/remote-development-status.md` for the authoritative Remote handoff and `CHANGELOG.md` plus Git history for earlier milestone detail.

## GrassiLooper v1.4 development

Source of truth: `docs/looper-roadmap.md`.

Current development gate: **Gate 0 — Freeze/document v1.3 baseline**.

Gate 0 is a documentation/baseline-freeze gate and does not change runtime audio code. Gate 1 remains locked until Gate 0 is explicitly accepted by the user and recorded in `docs/looper-development-status.md`.
