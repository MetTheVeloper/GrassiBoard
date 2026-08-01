# GrassiBoard v0.6.0 test driver

This package is a test-signed Milestone 5 driver for Windows 10/11 x64. It creates:

- `GrassiBoard Virtual Cable Input` (render)
- `GrassiBoard Virtual Microphone` (capture)

PCM played to the render endpoint is delivered by a kernel ring to the capture endpoint.
The cable uses a fixed 48 kHz, 16-bit, stereo device format and can be tested without
opening the GrassiBoard app.

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

1. Confirm both endpoint names in Sound settings, confirm `GrassiBoard Virtual Audio`
   has no warning icon in Device Manager, and confirm `Windows Audio` remains Running.
2. In each endpoint's Advanced properties, confirm the default format is
   `2 channel, 16 bit, 48000 Hz`.
3. In this package's `scripts` directory, run `./New-CableTestWave.ps1`. It creates
   `GrassiBoard-cable-test.wav` on the Desktop with 440, 660, and 880 Hz tones separated
   by silence and followed by two seconds of silence.
4. In Windows App volume and device preferences, route a media player to
   `GrassiBoard Virtual Cable Input`. Select `GrassiBoard Virtual Microphone` as the
   input for Voice Recorder (or another recorder).
5. Start recording, play the entire WAV once, stop playback, and keep recording for at
   least three more seconds.
6. Restore your physical headset as output before listening to the recording. Confirm
   all three tones and silent gaps are present, the tail stays silent, and no old audio
   loops or repeats.
7. Repeat play/stop three times. Confirm the recorder does not hang and both
   `Audiosrv` and `AudioEndpointBuilder` remain Running.

The GrassiBoard app does not send its processed microphone to the cable until Milestone 6.

## Removal and recovery

The uninstaller resolves the actual PnP instance and exact OEM INF before changing the
device, then removes only the matching driver package and signer certificate.

1. Run `./Uninstall-GrassiBoardDriver.ps1` as Administrator.
2. Confirm both endpoints disappear and Windows Audio remains Running.
3. Run `./Disable-TestSigning.ps1`, then reboot manually.
4. If anything fails, run `./Collect-DriverDiagnostics.ps1` before removal and keep the
   generated text file.

The uninstall script removes only the unique `ROOT\GrassiBoardVirtualAudio` device,
its matching OEM INF package, and the exact GrassiBoard test certificate thumbprint.
