# Changelog

All notable changes to GrassiBoard are documented in this file.

## [0.1.0] - 2026-08-01

### Added

- Initial repository architecture for the WPF app, native C++20 audio-engine DLL, shared build contract, tests, tools, installer, documentation, and driver placeholder.
- WPF shell displaying the package version, commit SHA, driver state, and native ABI load status.
- Versioned native C ABI with API/version queries and a smoke-test entry point.
- Managed and native smoke tests.
- Pinned .NET SDK and future WDK package versions.
- GitHub Actions workflows for x64 build, validation, packaging, artifacts, and prereleases.
- BuildInfo generation and four milestone artifact packages.

### Not implemented

- Audio capture, audio processing, monitoring, soundboard features, and the virtual audio driver.
