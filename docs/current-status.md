# Current status

- Version: `v0.1.0`
- Milestone: `0 — Repository and base CI`
- Status: CI passed; awaiting user acceptance
- Target: Windows 10/11 x64
- Audio processing: not implemented
- Virtual driver: safe placeholder only; not installable

## CI result

GitHub Actions build [run #4](https://github.com/MetTheVeloper/GrassiBoard/actions/runs/30699952322) passed on Windows x64. Native and managed smoke tests passed, the portable package contract was validated, and all four required artifact groups were uploaded.

## Acceptance gate

Milestone 1 must not begin until the v0.1.0 GitHub Actions build succeeds, the portable artifact is downloaded, and the user confirms that the app opens, displays the correct version/commit, and loads native API version 1.
