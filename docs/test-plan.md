# Test plan

## Automated Milestone 4 checks

- Build the native x64 Release DLL with warnings treated as errors and validate native ABI version 4.
- Retain Milestone 2 frequency, sample-count, finite/peak, variable-block, Bypass, and automation coverage.
- Generate voice-like source audio with deterministic harmonic/formant envelopes.
- Verify measurable output differences for preservation on/off and a +6-semitone Formant shift.
- Exercise live Low latency, High quality, and Balanced transitions without resetting the processor.
- Reject non-finite/excessive output, severe sample discontinuities, or long silent gaps during live switching.
- Benchmark all three configurations for algorithmic latency, processing time, estimated single-core percentage, and pitch-frequency error.
- Enforce the explicit Balanced default-selection policy.
- Generate input, pitch, Formant, and live-switch WAV files plus JSON reports.
- Retain all accepted Milestone 3 native and managed tests.
- Build the x64 kernel driver and statically linked SetupAPI device helper with pinned WDK/SDK NuGet packages.
- Validate the unique hardware ID, exact endpoint names, one-render/one-capture registration, and pinned SysVAD provenance.
- Generate the INF catalog, sign SYS and CAT with an ephemeral test certificate, verify both signatures, and reject private-key material from the artifact.
- Publish the portable app and test-signed driver as separate packages; reject driver/certificate material from the portable ZIP.

Runner timing is comparative evidence, not a promise of target-PC CPU usage. Audible Formant character, perceived quality, and physical endpoint stability remain manual checks.

## Manual Milestone 4 acceptance

1. Save the BitLocker recovery key, enable TESTSIGNING with the packaged script, and reboot manually.
2. Install the driver from an elevated PowerShell window.
3. Confirm `GrassiBoard Virtual Cable Input` and `GrassiBoard Virtual Microphone` both appear.
4. Confirm `GrassiBoard Virtual Audio` has no warning/error in Device Manager.
5. Confirm Windows Audio and Windows Audio Endpoint Builder remain Running.
6. Remove the driver with the packaged script and confirm both endpoints disappear.
7. Disable TESTSIGNING and reboot manually.

PCM transport between the two endpoints is not an acceptance requirement until Milestone 5.
