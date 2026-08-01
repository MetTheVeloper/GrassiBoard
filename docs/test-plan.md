# Test plan

## Automated Milestone 2 checks

- Configure and build the native x64 Release DLL with warnings treated as errors.
- Verify native ABI version 3, engine version, lifecycle, idle statistics, pitch setters, and exported ping function with CTest.
- Run offline pitch tests at seven semitone targets using irregular block sizes.
- Verify output duration, finite/bounded samples, approximate output frequency, latency-aligned Bypass, and stability during rapid parameter changes.
- Generate comparison WAV files and a machine-readable pitch/latency report.
- Parse and validate v0.3.0 BuildInfo with the managed smoke-test executable.
- Build and self-contained-publish the WPF x64 application.
- Build and validate the explicitly non-installable driver placeholder.
- Verify package contents and create portable, driver-placeholder, symbols, and test-result ZIP files.

GitHub-hosted runners cannot validate audible quality, perceived speech speed, severe clicks/pops on a physical USB headset, or real endpoint timing. Those checks are manual.

## Manual acceptance

1. Select the USB headset microphone and matching headset output.
2. Lower headset volume, leave Bypass enabled, start the engine, and confirm clean monitoring.
3. Disable Bypass and test `-12`, `-6`, `-3`, `+3`, `+6`, and `+12` semitones while speaking continuously.
4. Confirm pitch changes immediately while speech speed remains stable.
5. Move Pitch and Fine Pitch controls rapidly and listen for severe clicks, pops, cuts, or instability.
6. Toggle Bypass repeatedly without stopping the engine and confirm the dry signal remains clean.
7. Record Pitch Latency, endpoint buffer sizes, Ring Fill, and U/O/D counters after at least 30 seconds.
8. Stop the engine and close the application normally.
