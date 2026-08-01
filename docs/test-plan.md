# Test plan

## Automated Milestone 5 checks

- Compile and run the platform-neutral PCM ring policy used by the kernel wrapper.
- Preserve PCM byte order across wrap-around and enforce block-aligned writes/reads.
- Require capture pre-roll and zero-filled output while render/capture is inactive or insufficiently primed.
- Require partial underruns to zero-fill their tail and subsequent empty reads never to repeat old PCM.
- Exercise bounded overrun behavior plus underrun/overrun counters.
- Flush queued audio across render stop/restart so no previous session can leak into capture.
- Build the x64 WaveRT driver with the fixed 48 kHz, 16-bit, stereo format advertised on both system streams.
- Package the deterministic 440/660/880 Hz WAV generator with the driver.

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

## Manual Milestone 5 acceptance

1. Enable TESTSIGNING, reboot, and install the v0.6.1 driver using the packaged elevated scripts.
2. Confirm Device Manager is `OK`, both endpoints exist, and their Advanced default format is `2 channel, 16 bit, 48000 Hz`.
3. Run `scripts/New-CableTestWave.ps1` to create the fixed acceptance WAV.
4. Route a media player to `GrassiBoard Virtual Cable Input`, select `GrassiBoard Virtual Microphone` as the recording input, and record the complete WAV.
5. Restore the physical headset output before listening. Confirm the recorded sequence contains 440, 660, and 880 Hz tones with the intended silent gaps and acceptable quality.
6. Continue recording for at least three seconds after playback ends; require silence with no loop or repeated tail.
7. Repeat play/stop three times and confirm Windows Audio remains Running and no application hangs.
8. Uninstall v0.6.1, confirm both endpoints disappear, disable TESTSIGNING, and reboot.

Milestone 4 lifecycle acceptance passed on Windows 10 before Milestone 5 began. App-to-cable routing and processed microphone output are not Milestone 5 acceptance requirements.
