# Capture engine-policy test

This `v0.6.5` package addresses the Windows 10 build 19045 failure isolated by the
`v0.6.4` matrix. The render endpoint and healthy capture endpoints contained Audio
Engine mix-format and period policy values; only the GrassiBoard capture endpoint
omitted both, matching AudioSrv's `ERROR_NOT_FOUND` trace.

## Test

Close GrassiBoard, Voice Recorder, media players, and any app using a GrassiBoard
endpoint. In an elevated PowerShell opened in this package's `scripts` directory:

```powershell
Get-ChildItem -Filter *.ps1 -File | Unblock-File
./Uninstall-GrassiBoardDriver.ps1
./Install-GrassiBoardDriver.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File "G:\ZADAK\GrassiBoard\tools\diagnostics\Test-WasapiCaptureEndpoint.ps1"
```

The endpoint-policy fix passes when all four lines are:

```text
EngineMixFormatPresent : True
EnginePeriodPresent    : True
GetMixFormat           : 0x00000000
InitializeShared       : 0x00000000
```

Do not continue to the PCM transport test if either property is absent or either
HRESULT is nonzero. Keep TESTSIGNING enabled and send the complete probe output.
