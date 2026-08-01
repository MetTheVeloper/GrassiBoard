# Troubleshooting

## App does not open

Extract the entire portable ZIP before starting the app. Do not run the executable from inside the archive. Record the Windows version and attach a screenshot or Event Viewer application error.

## Native DLL load failed

Confirm that `GrassiBoard.AudioEngine.dll` is beside `GrassiBoard.exe` and that the x64 package was downloaded. Do not download individual files from inside the artifact.

## Start failed

Refresh the device lists, ensure both endpoints are active in Windows Sound settings, and select a microphone under Input and headphones under Monitor. Copy the HRESULT shown in Engine Status when reporting the problem.

## Feedback or excessive volume

Stop the engine immediately. Use headphones instead of speakers and lower the Windows output volume before starting again.

## Dropouts

Report the Capture/Render buffer frames, Ring Fill, and U/O/D counters shown after speaking for at least 30 seconds. Also include the exact USB headset model and whether other audio software was running.

## Pitch sounds unchanged

Confirm Bypass is unchecked and set Pitch to at least `-3` or `+3`. Fine Pitch is deliberately subtle and is limited to one semitone in either direction.

## Pitch delay or artifacts

Pitch shifting introduces reported algorithmic latency in addition to endpoint buffering. Include the Pitch Latency value, selected pitch/fine values, and whether the issue also occurs at `0` semitones when reporting artifacts.

## Formant change is difficult to hear

Disable Bypass, set Pitch to `+7` or `-7`, keep Formant Shift at `0`, and toggle Preserve formants while speaking continuously. Then enable preservation and compare Formant Shift `-6` with `+6`.

## Quality mode switch issue

Report the source and destination modes, the latency shown after the switch, whether Engine Status ever stopped, and the U/O/D counters. All three processors are prepared before Start; a mode change should not restart WASAPI.

## Driver questions

The v0.5.0 driver is in a separate test-signed ZIP. Read `DRIVER-TESTING.md` before changing TESTSIGNING. If installation or removal fails, run `scripts/Collect-DriverDiagnostics.ps1` before making further changes. The scripts do not disable Secure Boot, suspend BitLocker, or reboot automatically.
