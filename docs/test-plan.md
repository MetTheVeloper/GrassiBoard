# Test plan

## Automated Milestone 3 checks

- Build the native x64 Release DLL with warnings treated as errors and validate native ABI version 4.
- Retain Milestone 2 frequency, sample-count, finite/peak, variable-block, Bypass, and automation coverage.
- Generate voice-like source audio with deterministic harmonic/formant envelopes.
- Verify measurable output differences for preservation on/off and a +6-semitone Formant shift.
- Exercise live Low latency, High quality, and Balanced transitions without resetting the processor.
- Reject non-finite/excessive output, severe sample discontinuities, or long silent gaps during live switching.
- Benchmark all three configurations for algorithmic latency, processing time, estimated single-core percentage, and pitch-frequency error.
- Enforce the explicit Balanced default-selection policy.
- Generate input, pitch, Formant, and live-switch WAV files plus JSON reports.
- Run managed BuildInfo tests, publish WPF, validate the non-installable driver placeholder, package, and verify four artifacts.

Runner timing is comparative evidence, not a promise of target-PC CPU usage. Audible Formant character, perceived quality, and physical endpoint stability remain manual checks.

## Manual acceptance

1. Start with headphones at low volume, Balanced mode, preservation enabled, and Pitch Bypass enabled.
2. Confirm clean Bypass, then disable it and set Pitch to `+7`.
3. Toggle Preserve formants and confirm an audible vocal-character difference.
4. Test Formant Shift at `-6`, `0`, and `+6`.
5. Speak continuously while switching among Low latency, Balanced, and High quality.
6. Confirm the stream never stops or resets; compare delay and sound quality.
7. Move Pitch/Formant quickly and listen for severe clicks, pops, cuts, or UI stalls.
8. Record each mode's Pitch Latency plus Capture/Render, Ring Fill, and U/O/D after at least 30 seconds.
9. Toggle Bypass repeatedly and repeat Start/Stop three times.
