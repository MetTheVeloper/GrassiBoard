# Troubleshooting

## App does not open

Extract the entire portable ZIP before starting the app. Do not run the executable from inside the archive. Record the Windows version and attach a screenshot or Event Viewer application error.

## Native DLL load failed

Confirm that `GrassiBoard.AudioEngine.dll` is beside `GrassiBoard.exe` and that the x64 package was downloaded. Do not download individual files from inside the artifact.

## Start failed

Refresh the device lists, ensure both endpoints are active in Windows Sound settings, select the physical microphone under Input, and select the cable playback endpoint under Send. Copy the HRESULT shown in Engine Status when reporting the problem.

## Routing loop or excessive level

Stop the engine immediately. Do not select the cable recording endpoint as GrassiBoard's physical input. The source must be a real microphone and the send destination must be the cable playback endpoint.

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

## Retired GrassiBoard driver

The v0.6.x custom driver is no longer a product dependency. If it is still installed, use the matching v0.6.5 scripts to uninstall it, verify both GrassiBoard endpoints are gone, disable TESTSIGNING, and reboot. Do not install it for v0.7.0 testing.

## Virtual cable is silent

Require the green `Cable ready` message. GrassiBoard must use the playback/input side of the external cable; Voice Recorder, OBS, or Discord must use the paired recording/output side named by GrassiBoard. Confirm microphone privacy access is enabled for the target application and close any client holding the endpoint in exclusive mode.

If no pair is detected, refresh after installing/rebooting the cable. The installed AMM virtual device may be tested first; otherwise follow the official VB-CABLE installation instructions in `external-virtual-cable.md`.

The v0.5.0 installer could report a false failure because Windows generated instance ID `ROOT\GRASSIBOARD_VIRTUAL_AUDIO\0000`, which does not resemble hardware ID `ROOT\GrassiBoardVirtualAudio`. Do not rerun that installer. Use the v0.5.1 diagnostics and uninstaller, which resolve the device by HardwareID and abort before removal if the exact OEM INF cannot be recovered.
