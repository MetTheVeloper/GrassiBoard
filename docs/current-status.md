# Current status

- Version: `v0.2.0`
- Milestone: `1 — Physical Microphone Passthrough`
- Status: CI passed; awaiting target-PC acceptance
- Target: Windows 10/11 x64
- Audio processing: capture-to-monitor passthrough only
- Virtual driver: safe placeholder only; not installable

## CI result

GitHub Actions build [run #7](https://github.com/MetTheVeloper/GrassiBoard/actions/runs/30701605192) passed on Windows x64. Native ABI/lifecycle tests, managed BuildInfo tests, WPF publishing, package validation, and all four artifact uploads succeeded. Driver Placeholder Check run #2 also passed.

## Current acceptance gate

Milestone 2 must not begin until the v0.2.0 workflow succeeds and the user confirms on the target PC that:

- the USB headset microphone and monitor endpoint are selectable;
- Start and Stop work repeatedly without a crash;
- input and output meters move while speaking;
- microphone audio is audible through the selected headset;
- the UI remains responsive and dropout counters are reported.

## Previous accepted milestone

Milestone 0 (`v0.1.0`, commit `d23f75eb`) was accepted on Windows 10 on 2026-08-01. The app opened, displayed the correct version/commit, and loaded native API 1 successfully.
