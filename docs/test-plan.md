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
- Exercise lifecycle helpers with a generated instance ID that differs from the hardware ID; require correct root-device, OEM INF, and two-endpoint discovery.
- Generate the INF catalog, sign SYS and CAT with an ephemeral test certificate, verify both signatures, and reject private-key material from the artifact.
- Publish the portable app and test-signed driver as separate packages; reject driver/certificate material from the portable ZIP.

Runner timing is comparative evidence, not a promise of target-PC CPU usage. Audible Formant character, perceived quality, and physical endpoint stability remain manual checks.

## Manual Milestone 4 acceptance

1. For the affected v0.5.0 installation, collect diagnostics and use the v0.5.1 package to remove the device, old OEM INF, and old signer certificate.
2. Confirm both endpoints disappear and both Windows Audio services remain Running.
3. Install the v0.5.1 driver from an elevated PowerShell window while TESTSIGNING remains enabled.
4. Confirm the installer reports Device Manager `OK`, records the OEM INF, and detects two endpoints.
5. Confirm `GrassiBoard Virtual Audio` has no warning/error in Device Manager.
6. Remove v0.5.1 and confirm the device, both endpoints, OEM INF, and test certificate disappear.
7. Disable TESTSIGNING and reboot manually.

PCM transport between the two endpoints is not an acceptance requirement until Milestone 5.
