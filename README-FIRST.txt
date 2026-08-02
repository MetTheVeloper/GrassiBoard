GrassiBoard v0.7.0 — External virtual cable integration

PREREQUISITE: Install a compatible external virtual audio cable and reboot if its publisher requires it.

1. Extract the complete GrassiBoard portable ZIP and run GrassiBoard.exe.
2. INPUT MICROPHONE: select the physical Microsoft LifeChat microphone.
3. SEND TO VIRTUAL CABLE INPUT: select the cable playback endpoint.
4. Confirm the green Cable ready message names the paired recording endpoint.
5. Start GrassiBoard in Balanced mode with Bypass checked.
6. In Voice Recorder or OBS, select the exact target microphone shown by GrassiBoard.
7. Confirm speech is recorded, then uncheck Bypass and test Pitch at +7.
8. Confirm the recording contains the processed voice and the meters move.
9. Repeat Start/Stop three times and confirm the target app can reopen the microphone.

The portable ZIP does not contain or install a virtual audio driver. The old GrassiBoard test driver is retired and should be uninstalled before this test. Local headphone monitoring is deferred; this build validates system-wide microphone routing first.
