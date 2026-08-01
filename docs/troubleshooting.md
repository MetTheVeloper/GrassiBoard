# Troubleshooting

## App does not open

Extract the entire portable ZIP before starting the app. Do not run the executable from inside the archive. Record the Windows version and attach a screenshot or Event Viewer application error.

## Native DLL load failed

Confirm that `GrassiBoard.AudioEngine.dll` is beside `GrassiBoard.exe` and that the x64 package was downloaded. Do not download individual files from inside the artifact.

## Driver questions

There is no installable driver in v0.1.0. The `driver-placeholder` directory is informational and must not be installed.
