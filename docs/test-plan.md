# Test plan

## Automated v0.7.0 checks

- Build the native x64 Release DLL with warnings treated as errors and validate native ABI version 4.
- Retain all accepted pitch, formant, quality switching, benchmark, and DSP regression tests.
- Enumerate active capture and render endpoints with friendly name, default state, and Windows Container ID.
- Pair VB-CABLE endpoints by Container ID.
- Pair AMM-style virtual endpoints by the conservative family-name fallback.
- Reject a physical headset pair as a virtual cable.
- Reject the retired GrassiBoard test-driver endpoints.
- Publish portable app, symbols, and test results without SYS, INF, CAT, certificate, or third-party cable files.

The experimental custom driver workflow is manual and is not a product/release dependency.

## Manual v0.7.0 acceptance

1. Confirm the retired GrassiBoard test driver is uninstalled and TESTSIGNING is disabled.
2. Use the already installed AMM virtual device if it exposes a working endpoint pair, or install VB-CABLE from its official publisher and reboot.
3. Open GrassiBoard and confirm the physical LifeChat microphone is selected as `INPUT MICROPHONE`.
4. Confirm `SEND TO VIRTUAL CABLE INPUT` selects the cable playback endpoint.
5. Require a green `Cable ready` message and note the paired recording endpoint named below it.
6. Start in Balanced mode with Bypass enabled; require moving input and cable-send meters.
7. Select the named cable recording endpoint as the microphone in Voice Recorder and record speech.
8. Disable Bypass, set Pitch to `+7`, and require the recorded voice to contain the pitch change.
9. Repeat with OBS or another target application that accepts a microphone.
10. Repeat Start/Stop three times and require the target microphone to reopen without restarting Windows Audio.

Separate low-latency headphone monitoring and Soundboard mixing are not v0.7.0 acceptance requirements.
