# Current status

- Version: `v0.3.0`
- Milestone: `2 — Pitch Shift Prototype`
- Status: automated validation passed; awaiting manual acceptance
- Target: Windows 10/11 x64
- Audio processing: live pitch shift with Fine Pitch, smoothing, latency-aligned Bypass, and latency reporting
- Pitch backend: Signalsmith Stretch 1.3.2, commit `57b93f4e9206a089a45387eaa39bdc9f310d3308`
- Virtual driver: safe placeholder only; not installable

## Milestone 2 validation

The automated suite synthesizes a 220 Hz reference signal, exercises `-12`, `-6`, `-3`, `0`, `+3`, `+6`, and `+12` semitone settings with irregular block sizes, estimates the resulting frequencies, verifies finite/bounded output, checks sample counts, tests exact latency-aligned Bypass, and stress-tests rapid parameter automation. It also writes comparison WAV files and a JSON latency report to the test-results artifact.

GitHub Actions Build [run #11](https://github.com/MetTheVeloper/GrassiBoard/actions/runs/30702768812) passed on Windows x64. Native compilation with warnings treated as errors, all CTest and managed tests, WPF publishing, package-contract validation, and all four artifact uploads succeeded.

The generated report measured algorithmic latency at `2560` samples (`53.33 ms` at 48 kHz). Measured outputs were `110.45`, `155.65`, `189.04`, `220.38`, `265.07`, `312.85`, and `442.81 Hz` for the seven pitch targets; every result passed the configured tolerance.

Physical-headset listening, perceived speed, artifact, live-control, and repeated-Bypass acceptance remain pending for the `v0.3.0` prerelease.

## Previous accepted milestones

Milestone 1 (`v0.2.0`) was accepted on 2026-08-01 on Windows 10 with a Microsoft LifeChat LX-3000. Both endpoints were visible, monitoring was audible, both meters moved, and repeated Start/Stop passed. Observed diagnostics were Capture `1056`, Render `1056`, Ring Fill `480`, U/O/D `2/0/1`.

Milestone 0 (`v0.1.0`, commit `d23f75eb`) was accepted on Windows 10 on 2026-08-01. The app opened, displayed the correct version/commit, and loaded native API 1 successfully.
