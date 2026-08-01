# Current status

- Version: `v0.4.0`
- Milestone: `3 — Formant and Backend/Configuration Comparison`
- Status: automated validation passed; awaiting manual acceptance
- Target: Windows 10/11 x64
- DSP: live Pitch/Fine Pitch, Formant preservation/shift, Bypass, and three quality configurations
- Default: Balanced, selected by the committed benchmark policy
- Backend: Signalsmith Stretch 1.3.2 + Signalsmith Linear 0.3.1, both pinned
- Virtual driver: safe placeholder only; not installable

## Milestone 3 automated result

GitHub Actions Build [run #14](https://github.com/MetTheVeloper/GrassiBoard/actions/runs/30704540252) passed on Windows x64, as did the separate Driver Placeholder Check. Native `/W4 /WX` compilation, Pitch/Formant/live-switch tests, managed tests, WPF publishing, package validation, and all four artifact uploads succeeded.

Measured latency / single-core CPU / frequency error was `26.67 ms / 1.39% / 3.62%` for Low latency, `53.33 ms / 1.42% / 1.77%` for Balanced, and `150.00 ms / 1.53% / 0.03%` for High quality. The three warm processors summed to an estimated `4.34%` of one runner core. Balanced passed the default policy; Low latency missed its 3% frequency-error limit, while High quality added 96.67 ms over Balanced.

Formant preservation and `+6` shift produced RMS differences of `0.13` and `0.11`. Live automated switching recorded a maximum adjacent-sample step of `0.06` and a longest near-silent run of one sample. Physical-headset listening and target-PC live switching remain pending.

## Previous accepted milestones

Milestone 2 (`v0.3.0`, commit `28378232`) was accepted on 2026-08-01 on Windows 10 with a Microsoft LifeChat LX-3000. The user reported no issues. Screenshot evidence showed the engine running at `+12` semitones, Pitch Latency `2560` samples / `53.3 ms`, Capture `1056`, Render `576`, Ring Fill `576`, and U/O/D `2/0/3`.

Milestone 1 (`v0.2.0`) was accepted on the same target with physical microphone monitoring, moving meters, and repeated Start/Stop. Milestone 0 (`v0.1.0`) was accepted with the app, BuildInfo, and native engine load verified.
