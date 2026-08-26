# GrassiBoard Remote development status

> Authoritative handoff for Remote work. Repository status wins over chat history when they disagree.

## Current accepted baseline

- Current accepted Remote stage: **v1.3.0 Remote Phone Microphone / Full Remote Audio — USER ACCEPTED / PERSONAL-STABLE**
- v1.3 implementation commit: `f27b39055971da14bb8fa3753f28d5b690757a8f`
- Native ABI baseline in v1.3 source: **10**
- Native engine source version string at the frozen baseline: `1.3.0-gate2`
- Virtual microphone / Program route: **external VB-CABLE, unchanged**
- Remote use remains LAN-first and private/personal.
- The final packaged installer after the v1.3 implementation commit was **not separately manual clean-install verified**.

## Accepted stage history

### v1.1.0 — GrassiMote Remote Control

Status: **USER ACCEPTED / CI VERIFIED**

Accepted on real Windows + Android:

- secure HTTPS/WSS GrassiMote path;
- local CA onboarding and pairing;
- authoritative Board/Voice/Mixer/Media state/control;
- Mic Mute, Stop All, Engine lifecycle, reconnect and installed-PWA use;
- Material 3 portrait/landscape Remote UI.

Accepted implementation references:

- functional Remote commit: `6f3b672`
- accepted Material 3 UI commit: `7500901`
- GitHub Actions Build #84 / run `31529903075` — SUCCESS
- native ABI remained 8 for the accepted v1.1 baseline.

### v1.2.0 — Remote Monitor

Status: **USER ACCEPTED / PERSONAL-STABLE**

Frozen baseline:

- commit: `c3cf4da1a65a7f97314c265ab581dc9694d1b631`
- native ABI: **9**

Accepted real-device behavior includes the independent Remote Monitor bus, Windows/output audio, Soundboard, Media, processed My Voice opt-in, duplicate-Media prevention, monitor-only gains, WebRTC/Opus LAN transport, GrassiMote Monitor controls, endpoint-change recovery, and Program/VB-CABLE isolation.

SIPSorcery `10.0.13` remains the already-tested transport for current private/personal use. Re-open dependency/license review before any future public/commercial distribution.

### v1.3.0 — Remote Phone Microphone / Full Remote Audio

Status: **USER ACCEPTED / PERSONAL-STABLE BY REAL LOCAL WINDOWS + ANDROID TESTING**

Implementation baseline:

- commit: `f27b39055971da14bb8fa3753f28d5b690757a8f`
- commit title: `feat(remote): finalize v1.3.0 phone microphone input`
- native ABI: **10**

Accepted product path:

```text
Android microphone
→ getUserMedia
→ WebRTC / Opus
→ authenticated GrassiMote signaling
→ managed receive / normalization / bounded buffering
→ ABI 10 Remote Input ring
→ existing Voice FX / Pitch / Fine Pitch / Formant
→ existing Mixer / dynamics
→ Program Mix
→ external VB-CABLE
```

The accepted design preserves the existing physical Windows microphone path and keeps Phone Mic routing explicit. Remote Control, Remote Monitor, Soundboard, Media, Voice FX, Mixer and VB-CABLE remain independent regression requirements.

The v1.3 feature path was accepted through real local Windows + Android use. This is the functional baseline now frozen for GrassiLooper v1.4 development.

## v1.3 packaging / CI caveat

Feature acceptance must not be confused with final package verification.

For commit `f27b3905`, GitHub Actions Build #86 / run `31789622279`:

- Remote Web dependency install: PASS
- Remote Web generation: PASS
- native configure: PASS
- native build: PASS
- native tests: PASS
- managed smoke tests: **FAIL**
- self-contained publish: SKIPPED
- portable packaging/verification: SKIPPED
- installer build/verification: SKIPPED

The corresponding Release workflow also failed. Therefore v1.3 must not be described as CI/package/installer verified.

The final packaged installer after the last v1.3 implementation commit was not separately manual clean-install verified either.

## Remote roadmap state

The Remote feature roadmap is **complete for the current private/personal feature baseline**:

```text
v1.1 Remote Control    USER ACCEPTED
v1.2 Remote Monitor    USER ACCEPTED / PERSONAL-STABLE
v1.3 Full Remote Audio USER ACCEPTED / PERSONAL-STABLE
```

No new Remote implementation Gate is active. Future Remote changes are regression/fix work unless the roadmap is explicitly reopened.

## Handoff to GrassiLooper

GrassiLooper v1.4 must preserve all accepted Remote paths above. The authoritative Looper progress tracker is `docs/looper-development-status.md` and the Looper design source of truth is `docs/looper-roadmap.md`.
