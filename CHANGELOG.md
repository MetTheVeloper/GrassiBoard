# Changelog


## [1.2.0] - 2026-08-12

### Added
- GrassiMote Remote Monitor over same-LAN WebRTC/Opus.
- Independent Windows/Space, Soundboard, Media, and opt-in processed My Voice monitor sources.
- Automatic Media duplicate prevention.
- Phone-only source gains and monitor master.
- Brutal-minimal six-tile Monitor control surface with direct tap/drag levels.

### Changed
- Native audio ABI baseline advances to 9 for v1.2.
- Normal v1.2 builds enable the accepted Remote Monitor path by default.
- Native engine version reports 1.2.0.

### Notes
- Program/VB-CABLE routing is unchanged.
- Current v1.2 use is private/personal; dependency license review must be reopened before future public distribution.
- Final USER ACCEPTED status is pending the production-candidate soak/package gate.
All notable changes to GrassiBoard are documented in this file.

## [1.0.1] - 2026-08-10

### Added

- Add a live, profile-persisted Media Sync Calibration control from `-100` to `+100 ms` in one-millisecond steps.
- Apply calibration immediately to the computed Media virtual-send alignment without restarting the engine or Media Deck. Negative values advance Media; positive values delay Media.

### Stable release

- Promote the accepted installer, routing, UI, automatic microphone recovery, Soundboard, Voice FX, Mixer, Profiles, Hotkeys, and Local Media baseline to the first stable GitHub release.

## [1.0.0] - 2026-08-10

### Added

- Add automatic live-route recovery when the selected physical microphone disappears: switch to the next usable physical capture device while preserving Voice/Mixer/Media state, or force-mute the microphone branch and retry safely when none exists.
- Add a branded single-file per-user Windows installer using the supplied GrassiBoard poster, destination selection, progress/completion states, launch action, shortcuts, Apps & features registration, and manifest-based uninstall.
- Detect common external cable endpoints during Setup and show the official VB-CABLE Windows download link when missing without blocking installation.
- Extend native ABI 8 with explicit Media alignment reporting and managed control of the local-monitor path estimate.

### Fixed

- Render Profile and user-preset selectors by their human-readable names instead of CLR type names.
- Keep text and icons white on green action buttons in both themes.
- Replace font-fragile Media ±10 controls with stable `-10s` and `+10s` labels.
- Align the Media virtual-send branch to the complete microphone pre-render path and account for the buffered local headphone monitor, rather than delaying by Pitch latency alone.

### Release state

- This build is published as a prerelease pending the final Windows 10 long-duration and installer/uninstaller acceptance pass.

## [0.11.2] - 2026-08-10

### Fixed

- Remove the undefined `UiFont` StaticResource that caused v0.11.1 to fail during `MainWindow.InitializeComponent` with a fatal startup `XamlParseException`.
- Add a managed smoke-test contract that rejects unknown simple `StaticResource` keys before packaging.

### Changed

- Use the user-supplied GrassiBoard artwork as a multi-resolution 16–256 px Windows icon for the executable, WPF window, custom title bar, Alt+Tab/taskbar, and system tray.

## [0.11.1] - 2026-08-10

### Fixed

- Show only the endpoint `Name` in the Local Media monitor-device selector.
- Keep the Media timeline under user control while dragging so seeking works during active playback as well as while paused.
- Replace font-dependent/broken Stop and ±10 transport glyphs with theme-safe vector or UI-font content.
- Delay only the Local Media virtual-microphone branch by the active Pitch algorithm latency (150.0/53.3/26.7 ms for High/Balanced/Low), while keeping the independent headphone monitor direct.
- Preserve 200 ms of true Media read-ahead in addition to the alignment delay and report read-ahead without counting alignment frames.

### Verified

- Read the running v0.11.0 High-quality diagnostics: Pitch Algorithm 150.0 ms, Reported Pipeline approximately 234–244 ms, and Media Read-Ahead 5%. This confirms that synchronization must follow fixed algorithm latency rather than variable total pipeline latency.

## [0.11.0] - 2026-08-10

### Added

- Add versioned Profiles that restore devices, Pads, user presets, hotkeys, app/tray preferences, and Voice/Mixer state.
- Add persistent user-created Voice + Mixer presets with Save As, apply, update, duplicate, rename, delete, optional global hotkeys, and approximately 200 ms live parameter transitions.
- Add global hotkeys for Pads, presets, Mic Mute, Stop All, Voice FX, Push-to-Talk, Show/Hide, and Media Deck transport, including duplicate/Windows-registration conflict reporting.
- Add System Tray operation, Start Minimized, and an optional reversible per-user Start with Windows setting.
- Add a streaming Local Media Deck with load, play/pause/resume, stop, seek/timeline, ±10 seconds, volume, meter, independent headphone monitoring and virtual-mic send, missing-file safety, and persisted controls.
- Add a bounded preallocated native Media ring, ABI version 7, Media fill/underrun/meter diagnostics, and regression tests.

### Improved

- Keep the accepted event-driven shared-mode WASAPI worker, MMCSS `Pro Audio` scheduling, 48 kHz float pipeline, prewarmed latency modes, and callback allocation/lock/file-I/O rules.
- Report capture/render buffers, Pitch latency, ring fill, Media fill/underruns, and estimated total processing latency together in Diagnostics.
- Isolate malformed Profile, Pad, or Preset JSON items so valid siblings remain available.

### Preserved

- Preserve the user-accepted v0.9.0 Voice, Mixer, Soundboard, UI/theme, external VB-CABLE routing, meters, custom window, and Stop All behavior. Stop All additionally stops Media Deck playback.

### Verified

- Record complete Windows 10 manual acceptance of v0.9.0 as the regression baseline. v0.11.0 remains pending its required manual acceptance test.

## [0.9.0] - 2026-08-10

### Added

- Add live Mic, Soundboard, and Master gain stages.
- Add microphone Noise Gate and Compressor, microphone-keyed Soundboard Ducking, a stereo-linked Limiter, and Clipping Protection.
- Add latency-aligned Pitch Wet/Dry and Clean, Broadcast, Streaming, and Voice Chat presets.
- Add a dedicated Mixer page and native callback-safe Mixer/Dynamics processor through ABI version 6.

### Fixed

- Restore animated MIC, BOARD, MASTER, and Board Quick Levels fills in the shared meter template, with safe `-60..0 dBFS` mapping.
- Add a subtle theme-aware restored-window border and native DWM shadow.
- Make custom maximize respect the current monitor work area and Windows taskbar.
- Make global Stop All stop every Sound Pad and the audio engine, reset runtime meters/state, remain safe while already stopped, and allow a later restart.

### Preserved

- Preserve the accepted v0.8.3 external-cable route, Pitch/Formant behavior, Soundboard playback/persistence, Light/Dark themes, and custom application shell.

### Verified

- Record complete Windows 10 manual acceptance of v0.8.3 as the regression baseline for this milestone.

## [0.8.3] - 2026-08-10

### Changed

- Refresh the shared Light/Dark design system with semantic surface, interaction, state, spacing, and accessibility tokens.
- Redesign Sound Pad cards into compact 158 px controls with collision-safe titles, status dots, icon-only header and transport actions, disabled idle Stop, and calm playing/error states.
- Restyle sliders, checkboxes, meters, dropdowns, title-bar controls, navigation, routing status, diagnostics, and page cards around one Segoe MDL2 icon family.
- Improve device-name scanning with truncation and full-name tooltips while preserving the accepted audio routing and Soundboard behavior.

### Verified

- Record complete Windows 10 functional acceptance of v0.8.2 as the regression baseline for this presentation-only refresh.

## [0.8.2] - 2026-08-10

### Fixed

- Fix the invalid three-value Sound Pad button margin that raised a `XamlParseException` when the first Pad card was created.
- Replace the system ComboBox rendering with theme-aware selected-item and popup templates so device and quality labels remain readable.

### Added

- Add a custom draggable Windows title bar with minimize, maximize/restore, close, and persisted light/dark theme controls.
- Record startup, dispatcher, AppDomain, and unobserved-task failures in `%LOCALAPPDATA%\GrassiBoard\CrashReports`, including a stable `latest.txt` report.
- Add XAML thickness validation and a real Pad-card materialization diagnostic to prevent the first-Pad crash from returning.

## [0.8.1] - 2026-08-09

### Fixed

- Make Mic, Soundboard, and Master `ProgressBar.Value` bindings explicitly OneWay so WPF does not write into read-only meter properties and crash before showing the window.
- Add a visible startup-error dialog and `%LOCALAPPDATA%\GrassiBoard\startup-error.txt` report instead of silently exiting on future initialization failures.

### Verified

- Compile the corrected WPF app with .NET SDK 8.0.423 on the affected Windows 10 system and confirm the GrassiBoard window opens and remains responsive.

## [0.8.0] - 2026-08-09

### Added

- Add a real Soundboard with WAV/MP3 import, drag/drop, volume, Loop, per-pad stop, Stop All, simultaneous playback, edit/delete, and JSON persistence.
- Decode/resample Sound Pad files on a background thread with pinned NAudio 2.3.0, then cache immutable stereo 48 kHz PCM in the native engine.
- Add a fixed-command-queue, 32-voice native Soundboard mixer after the accepted microphone Voice DSP.
- Add Mic, Soundboard, and Master meters plus microphone-only Mute.
- Add native and managed regression tests for Soundboard mixing, offline WAV decode, and Pad persistence.
- Add the persistent Board, Voice, Routing, and Settings application shell with shared theme resources and copy-safe diagnostics.

### Changed

- Make Board the default daily workspace and move detailed Voice, device routing, and diagnostics controls to dedicated pages.
- Present native Pitch bypass as the positive user-facing Voice FX ON/OFF state.
- Simplify ordinary routing language to “virtual microphone” while keeping exact external-cable endpoint names in Routing and Diagnostics.
- Bump the native ABI to version 5 and product/native version to 0.8.0.

### Preserved

- Preserve the manually accepted v0.7.0 physical-microphone, Pitch/Formant, WASAPI, and external-cable route.
- Keep Sound Pads outside the microphone Pitch/Formant path.
- Keep the experimental custom driver out of product builds and packages.

### Deferred

- Separate low-latency headphone monitoring and Mixer/Dynamics controls remain future milestones.

## [0.7.0] - 2026-08-03

### Added

- Route the accepted live voice engine directly to any installed external virtual cable playback endpoint.
- Pair render and capture endpoints by Windows Container ID with a conservative vendor-neutral name fallback.
- Display the exact cable recording endpoint that target applications should select as their microphone.
- Prefer a detected external cable while keeping the physical microphone as the source.
- Add automated VB-CABLE, AMM, physical-headset, and retired-GrassiBoard endpoint matching fixtures.
- Add an external-cable setup and manual acceptance guide.

### Changed

- Retire the custom test-signed SysVAD driver from product builds and releases after the Windows 10 WASAPI capture investigation.
- Publish only the portable app, symbols, and test results; experimental driver source remains available for research.
- Rename the former monitor output as the explicit virtual-cable send destination.

## [0.6.5] - 2026-08-02

### Fixed

- Seeded the capture endpoint's missing Audio Engine mix-format and 10 ms engine-period policy values on Windows 10.
- Kept the device format at 48 kHz/16-bit mono while using the normal 48 kHz/32-bit-float mono shared-engine mix format.

### Investigation

- All three v0.6.4 variants failed identically, proving that OEM format registration, the custom mode table, and the GrassiBoard ring-backed capture path were not the source of the failure.
- Direct comparison on the affected machine showed the working GrassiBoard render endpoint, LifeChat capture endpoint, and AMM virtual capture endpoint all contained Audio Engine properties `{E4870E26-3CC5-4CD2-BA46-CA0A9A70ED04},0` and `,1`; only the broken GrassiBoard capture endpoint omitted both.
- Extended the WASAPI probe to report the presence of those two properties before attempting `GetMixFormat` and shared initialization.

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
