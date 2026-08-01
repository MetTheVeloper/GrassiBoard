# Current status

- Version: `v0.4.0`
- Milestone: `3 — Formant and Backend/Configuration Comparison`
- Status: implementation and automated validation in progress
- Target: Windows 10/11 x64
- DSP: live Pitch/Fine Pitch, Formant preservation/shift, Bypass, and three quality configurations
- Default candidate: Balanced, subject to the committed benchmark policy
- Backend: Signalsmith Stretch 1.3.2 + Signalsmith Linear 0.3.1, both pinned
- Virtual driver: safe placeholder only; not installable

## Previous accepted milestones

Milestone 2 (`v0.3.0`, commit `28378232`) was accepted on 2026-08-01 on Windows 10 with a Microsoft LifeChat LX-3000. The user reported no issues. Screenshot evidence showed the engine running at `+12` semitones, Pitch Latency `2560` samples / `53.3 ms`, Capture `1056`, Render `576`, Ring Fill `576`, and U/O/D `2/0/3`.

Milestone 1 (`v0.2.0`) was accepted on the same target with physical microphone monitoring, moving meters, and repeated Start/Stop. Milestone 0 (`v0.1.0`) was accepted with the app, BuildInfo, and native engine load verified.
