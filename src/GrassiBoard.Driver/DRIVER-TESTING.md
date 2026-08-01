# GrassiBoard v0.5.1 test driver

This package is a test-signed Milestone 4 driver for Windows 10/11 x64. It creates:

- `GrassiBoard Virtual Cable Input` (render)
- `GrassiBoard Virtual Microphone` (capture)

Milestone 4 validates installation and endpoint stability only. The endpoints do not
transport PCM between each other yet.

## Before testing

1. Use a non-production machine and save your BitLocker recovery key.
2. Open PowerShell as Administrator in this package's `scripts` directory. If the ZIP
   was downloaded in a browser, review the source and run
   `Get-ChildItem -Filter *.ps1 -File | Unblock-File` in that directory.
3. Run `./Enable-TestSigning.ps1`. If BitLocker is active, review the warning and rerun
   with `-AcknowledgeBitLockerRecoveryRisk` only after saving the recovery key.
4. Reboot manually. Windows should show the Test Mode watermark.
5. Run `./Install-GrassiBoardDriver.ps1`.

Never disable Secure Boot, suspend BitLocker, or reboot from these scripts: those are
deliberately manual decisions.

## Acceptance check

Confirm both endpoint names in Sound settings, confirm `GrassiBoard Virtual Audio` has
no warning icon in Device Manager, and confirm `Windows Audio` remains Running. Do not
expect cable audio transport in this milestone.

## Removal and recovery

The v0.5.1 uninstaller can recover the actual PnP instance, OEM INF name, and installed
signer certificate from a v0.5.0 installation that showed a false validation error.

1. Run `./Uninstall-GrassiBoardDriver.ps1` as Administrator.
2. Confirm both endpoints disappear and Windows Audio remains Running.
3. Run `./Disable-TestSigning.ps1`, then reboot manually.
4. If anything fails, run `./Collect-DriverDiagnostics.ps1` before removal and keep the
   generated text file.

The uninstall script removes only the unique `ROOT\GrassiBoardVirtualAudio` device,
its matching OEM INF package, and the exact GrassiBoard test certificate thumbprint.
