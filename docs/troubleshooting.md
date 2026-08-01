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

## Driver questions

There is no installable driver in v0.2.0. The `driver-placeholder` directory is informational and must not be installed.
