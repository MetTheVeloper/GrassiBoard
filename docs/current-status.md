# Current status

- Version: `v0.2.0`
- Milestone: `1 — Physical Microphone Passthrough`
- Status: accepted by user
- Target: Windows 10/11 x64
- Audio processing: capture-to-monitor passthrough only
- Virtual driver: safe placeholder only; not installable

## CI result

GitHub Actions build [run #7](https://github.com/MetTheVeloper/GrassiBoard/actions/runs/30701605192) passed on Windows x64. Native ABI/lifecycle tests, managed BuildInfo tests, WPF publishing, package validation, and all four artifact uploads succeeded. Driver Placeholder Check run #2 also passed.

## Manual acceptance

Milestone 1 was accepted on 2026-08-01 with the following target-PC result:

- Windows version: Windows 10
- USB headset: Microsoft LifeChat LX-3000
- Input and monitor endpoints visible: Yes
- Engine started and microphone monitoring was audible: Yes
- Input and output meters moved: Yes
- Repeated Start/Stop: Yes
- Observed diagnostics: Capture `1056`, Render `1056`, Ring Fill `480`, U/O/D `2/0/1`
- Screenshot evidence: supplied by the user

Milestone 2 is unblocked.

## Previous accepted milestone

Milestone 0 (`v0.1.0`, commit `d23f75eb`) was accepted on Windows 10 on 2026-08-01. The app opened, displayed the correct version/commit, and loaded native API 1 successfully.
