# Changelog

All notable changes to GrassiBoard are documented in this file.

## [0.6.4] - 2026-08-02

### Diagnostics

- Added a three-package capture matrix that tests explicit OEM format registration, the official SysVAD MicIn mode table, and the untouched SysVAD tone-capture path cumulatively.
- Assigned distinct driver versions `0.6.4.1`, `0.6.4.2`, and `0.6.4.3`, embedded a machine-readable variant marker, and documented a sequential uninstall/install/probe workflow.
- Extended the WASAPI probe to verify endpoint-volume and meter channel/range calls. Those controls succeed on v0.6.3 even while Audio Engine mix-format construction fails.

### Investigation

- Manual Windows 10 testing showed v0.6.3 still fails `GetMixFormat` and shared initialization with `0x88890008`, disproving the mono-channel-contract hypothesis.

## [0.6.3] - 2026-08-02

### Fixed

- Restored the capture endpoint to the reference SysVAD MicIn contract: mono PCM, a mono topology jack, five processing modes, and event-driven WaveRT.
- Kept the working virtual speaker stereo and added a bounded, allocation-free PCM16 stereo-to-mono downmix before frames enter the capture ring.
- Added regression coverage for the endpoint contract and downmix arithmetic, including full-scale samples.

### Investigation

- Manual v0.6.2 testing disproved the timer-driven hypothesis: WASAPI still failed at `GetMixFormat` with `0x88890008`. The remaining material divergence from Microsoft's working external MicIn reference was GrassiBoard's stereo capture conversion, so v0.6.3 removes that divergence.

## [0.6.2] - 2026-08-02

### Fixed

- Removed the event-driven opt-in from the capture endpoint after Windows 10 build 19045 repeatedly failed to construct its Audio Engine graph with `0x80070490` (`Element not found`), surfaced to clients as `AUDCLNT_E_UNSUPPORTED_FORMAT`.
- Kept the render endpoint event-driven and retained the fixed PCM ring transport; only capture scheduling falls back to the timer-driven WaveRT path.
- Added a source-contract check that prevents the incompatible capture opt-in from returning.

### Diagnostics

- Added direct KS, WASAPI, and focused ETW probes. They verified that exact capture pin creation succeeds while shared Audio Engine activation fails, separating the valid kernel format contract from the Windows service failure.

## [0.6.1] - 2026-08-02

### Fixed

- Restored the SysVAD capture pin's reference five-instance capacity during the initial Windows 10 compatibility investigation.
- Added a source-contract check that preserves that reference capacity. Later ETW diagnostics showed that instance capacity was not the cause of the Voice Recorder failure.

## [0.6.0] - 2026-08-01

### Added

- A lock-free, preallocated PCM ring connecting the WaveRT system-render stream to the system-capture stream.
- A shared `48 kHz`, 16-bit, stereo device format for both cable endpoints; Windows Audio performs client conversion outside the transport.
- Ten-millisecond capture pre-roll, silence on underrun or inactive render, and generation-based flushing across pause/stop/restart.
- Explicit underrun, overrun, and fill accounting in the kernel transport.
- Deterministic transport-policy tests covering wrap order, pre-roll, underrun silence, no stale repetition, overrun, and restart flushing.
- A packaged PowerShell generator for a fixed 440/660/880 Hz cable acceptance WAV.

### Scope

- Milestone 5 validates the cable independently of the GrassiBoard app. Routing the processed app output into the cable remains Milestone 6.

## [0.5.1] - 2026-08-01

### Fixed

- Identify the root audio device by its `ROOT\GrassiBoardVirtualAudio` hardware ID instead of assuming its generated PnP instance ID has the same spelling.
- Wait for the base device to report `OK`, persist the actual OEM INF identity, and recognize both Windows speaker naming and the custom microphone endpoint.
- Recover the active driver signer certificate and OEM INF before removal, including v0.5.0 installations whose state file was not written after the false validation failure.
- Verify that the device and both endpoints disappear after uninstall, while retaining explicit reboot handling.
- Include the actual root device, signed-driver record, installer-state status, and relevant GrassiBoard events in diagnostics.

## [0.5.0] - 2026-08-01

### Added

- Minimal x64 SysVAD/WaveRT driver skeleton with one render endpoint named `GrassiBoard Virtual Cable Input` and one capture endpoint named `GrassiBoard Virtual Microphone`.
- Project-specific product GUID, endpoint-name GUIDs, hardware ID `ROOT\GrassiBoardVirtualAudio`, service, binary, INF, and catalog identity.
- Native SetupAPI helper for controlled root-device creation, status inspection, and removal.
- Administrator scripts for explicit TESTSIGNING enable/disable, certificate trust, install, uninstall, and read-only diagnostics.
- CI-only ephemeral test certificate generation, SYS/CAT signing, signature verification, SHA-256 manifest, and driver artifact upload.
- Pinned Microsoft SysVAD provenance and Microsoft Public License notice.

### Safety and scope

- The portable app and test driver are separate packages. No private signing key is retained or uploaded.
- Scripts never disable Secure Boot, suspend BitLocker, reboot Windows, or silently disable TESTSIGNING.
- PCM transport between render and capture is intentionally deferred to Milestone 5.

## [0.4.0] - 2026-08-01

### Added

- Formant preservation and independent formant shift from −12 to +12 semitones.
- Low latency, Balanced, and High quality Signalsmith configurations.
- Pre-warmed live processors and a 20 ms quality-mode crossfade that does not restart WASAPI.
- Smoothed preservation and Formant automation.
- Per-configuration CPU, algorithmic-latency, and frequency-accuracy benchmarks.
- Voice-like comparison WAVs for preservation, Formant shift, and live configuration changes.
- Native ABI version 4 with Formant and quality-mode controls.

### Default selection

- Balanced remains the default when its measured frequency error is at most 3%, its measured single-core cost is at most 25%, and its latency remains below High quality. Exact CI results are recorded in `docs/pitch-benchmark.md`.

### Not implemented

- Additional pitch backends, soundboard playback, noise processing, and the virtual audio driver.

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
