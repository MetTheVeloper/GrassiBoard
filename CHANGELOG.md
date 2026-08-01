# Changelog

All notable changes to GrassiBoard are documented in this file.

## [0.3.0] - 2026-08-01

### Added

- `IPitchProcessor` abstraction and a first backend based on Signalsmith Stretch 1.3.2 at pinned commit `57b93f4e`, with Signalsmith Linear 0.3.1 pinned at `56686735`.
- Live pitch control from −12 to +12 semitones and fine control from −100 to +100 cents.
- Twenty-five-millisecond pitch automation smoothing and a ten-millisecond bypass crossfade.
- Latency-aligned dry bypass so toggling Pitch does not reset the WASAPI streams or jump the monitoring timeline.
- Runtime reporting of algorithmic pitch latency in samples and milliseconds.
- Offline WAV generation and measured-frequency tests for −12, −6, −3, 0, +3, +6, and +12 semitones.
- Fixed-length, finite-sample, peak, bypass, variable-block, and rapid-automation pitch tests.

### Not implemented

- Formant preservation/shift, multiple quality modes in the UI, backend comparison, soundboard playback, and the virtual audio driver.

## [0.2.0] - 2026-08-01

### Added

- Active microphone and playback-device enumeration through the Windows MMDevice API.
- Shared-mode, event-driven WASAPI microphone capture and headset monitoring at 48 kHz float.
- A preallocated float ring buffer connecting capture to render without allocation or blocking locks in the audio loop.
- Safe engine start/stop, STA ownership of WASAPI objects, and MMCSS `Pro Audio` registration.
- Live input/output peak meters and diagnostics for buffer sizes, ring fill, underruns, overruns, and capture discontinuities.
- Device selectors and a non-blocking WPF control flow for engine operations.
- Native ABI version 2 with engine lifecycle, enumeration, statistics, and error-reporting functions.

### Not implemented

- Pitch/formant processing, soundboard playback, noise processing, and the virtual audio driver.

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
