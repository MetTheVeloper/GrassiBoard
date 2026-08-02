# GrassiBoard capture diagnostic matrix

These packages isolate three cumulative hypotheses for the Windows 10 WASAPI
capture failure. They all use `ROOT\GrassiBoardVirtualAudio`, so install only one
at a time and always uninstall the current package before installing the next.

Test in this exact order:

1. `oem-format` (`0.6.4.1`) adds an explicit 48 kHz/16-bit/mono
   `PKEY_AudioEngine_OEMFormat` to the capture endpoint.
2. `reference-modes` (`0.6.4.2`) also switches the capture format/mode table to
   the unmodified Microsoft SysVAD MicIn table.
3. `reference-capture` (`0.6.4.3`) also disables GrassiBoard cable capture and
   uses the unmodified SysVAD generated-tone capture path.

For every package, close GrassiBoard, Voice Recorder, media players, and any app
using the virtual endpoints. In an elevated PowerShell opened in that package's
`scripts` directory, run:

```powershell
Get-ChildItem -Filter *.ps1 -File | Unblock-File
.\Uninstall-GrassiBoardDriver.ps1
.\Install-GrassiBoardDriver.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File "G:\ZADAK\GrassiBoard\tools\diagnostics\Test-WasapiCaptureEndpoint.ps1"
```

Record `GetMixFormat`, `MixFormat`, `InitializeShared`, and the package variant.
A variant passes endpoint activation only when both `GetMixFormat` and
`InitializeShared` are `0x00000000`. Stop after the first passing variant and
send the complete probe output. A reboot is not expected between variants.

Do not judge cable audio in variants 1 or 2 until WASAPI opens. Variant 3 emits
the SysVAD reference tone instead of cable PCM by design; it tests endpoint
activation only.
