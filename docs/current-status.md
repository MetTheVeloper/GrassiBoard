# Current status

- Version: `v0.6.3`
- Milestone: `5 — Virtual Cable PCM Transport`
- Status: v0.6.0 through v0.6.2 capture activation failed on Windows 10; v0.6.3 reference-MicIn contract fix is awaiting CI and manual retest
- Target: Windows 10/11 x64
- DSP: live Pitch/Fine Pitch, Formant preservation/shift, Bypass, and three quality configurations
- Default: Balanced, selected by the committed benchmark policy
- Backend: Signalsmith Stretch 1.3.2 + Signalsmith Linear 0.3.1, both pinned
- Virtual driver: one test-signed render endpoint and one capture endpoint connected by a fixed-format PCM ring

## Milestone 5 implementation

The driver is a minimal extraction of Microsoft's SysVAD/WaveRT code pinned to commit `ef7c3074748ab05726c3a9161d3256118efd76e2`. It uses hardware ID `ROOT\GrassiBoardVirtualAudio` and exposes `GrassiBoard Virtual Cable Input` plus `GrassiBoard Virtual Microphone`.

The render stream advertises fixed 48 kHz, 16-bit stereo PCM. Its frames are downmixed without allocation into a fixed 48 kHz, 16-bit mono ring consumed by the capture stream, preserving Microsoft's reference external-MicIn channel contract. Capture uses a 10 ms pre-roll, zero-fills underruns, and invalidates queued frames whenever render or capture pauses/stops so old audio cannot repeat. The transport counts fill, underruns, and overruns.

The cable is deliberately testable without the GrassiBoard app. App-to-cable routing is Milestone 6.

## Milestone 5 Windows 10 capture finding

Manual testing on Windows 10 build 19045 found that the v0.6.0 through v0.6.2 endpoints were present and Device Manager reported `OK`, but Voice Recorder could not open the virtual microphone. A direct KS probe verified the capture filter, its two pins, all five processing modes, exact format negotiation, and successful pin creation. A direct WASAPI probe still reproduced `AUDCLNT_E_UNSUPPORTED_FORMAT` from `IAudioClient::GetMixFormat` and both shared/exclusive initialization paths.

A focused ETW trace exposed the underlying Audio Engine result as `0x80070490` (`Element not found`) during per-endpoint policy construction; Windows then mapped it to `AUDCLNT_E_UNSUPPORTED_FORMAT`. The unchanged v0.6.2 result disproved event-driven scheduling as the cause, just as v0.6.1 disproved the earlier stream-instance hypothesis. v0.6.3 restores event-driven capture and removes the remaining material divergence from Microsoft's working external MicIn reference: the capture endpoint is mono again, with a matching mono topology jack and default formats. The stereo render input is downmixed before entering the mono ring.

## Milestone 5 automated result

Pull request CI [Build](https://github.com/MetTheVeloper/GrassiBoard/actions/runs/30713553221) and [Driver Artifact](https://github.com/MetTheVeloper/GrassiBoard/actions/runs/30713553216) passed for implementation commit `03907d7`. The WDK compiled the WaveRT driver with warnings as errors, generated and test-signed its package, and uploaded all artifacts. Native CTest included the exact platform-neutral ring policy used by the kernel wrapper and passed wrap-order, pre-roll, silence, stale-data, overrun, and restart cases. Existing native, managed, DSP, package-isolation, and lifecycle tests also passed.

## Milestone 4 manual acceptance

Milestone 4 was accepted on 2026-08-01 on Windows 10 build 19045. The v0.5.1 recovery uninstaller removed the affected v0.5.0 device, exact OEM INF, both endpoints, and signer certificate. A fresh v0.5.1 install reported Device Manager `OK` and detected `2 of 2` endpoints; the final uninstall again removed both endpoints and package. After disabling TESTSIGNING and rebooting, the Test Mode watermark disappeared, no present GrassiBoard PnP device remained, and both `Audiosrv` and `AudioEndpointBuilder` were Running.

Windows 10 testing of v0.5.0 had exposed an installer lifecycle bug: generated instance ID `ROOT\GRASSIBOARD_VIRTUAL_AUDIO\0000` did not resemble hardware ID `ROOT\GrassiBoardVirtualAudio`. v0.5.1 resolved the device by HardwareID and recovered the installed INF and signer before removal.

## Milestone 4 hotfix automated result

GitHub Actions [Build](https://github.com/MetTheVeloper/GrassiBoard/actions/runs/30711936461) and [Driver Artifact](https://github.com/MetTheVeloper/GrassiBoard/actions/runs/30711936484) passed for commit `227779b6`. The lifecycle regression fixture reproduced hardware ID `ROOT\GrassiBoardVirtualAudio` with generated instance ID `ROOT\GRASSIBOARD_VIRTUAL_AUDIO\0000`, then verified root-device discovery, actual-instance-to-OEM-INF mapping, and two healthy endpoints.

The downloaded v0.5.1 driver artifact contained 14 valid manifest entries, no private key material, the corrected lifecycle scripts, and SYS/CAT signatures whose signer thumbprints matched the packaged public certificate. Full native, managed, driver, packaging, and artifact-isolation checks also passed.

## Milestone 4 automated result

GitHub Actions [Build run #24](https://github.com/MetTheVeloper/GrassiBoard/actions/runs/30709327790) and [Driver Artifact run #12](https://github.com/MetTheVeloper/GrassiBoard/actions/runs/30709327823) passed for commit `6a9ed916`. The native and managed regression suites, `/W4 /WX` driver build, root-device helper build, INF signability check, CAT generation, ephemeral test signing, portable-package isolation, and all artifact uploads succeeded.

The independently downloaded driver artifact contained the expected 14 manifest entries. Every SHA-256 entry matched, the SYS and CAT signer thumbprints matched the packaged public certificate, and no PFX, PVK, PEM, or private key was present. The packaged driver ZIP SHA-256 was `d430c8903a7e70932366d70d74b7a570948d35903f4e18a3e73e5105ef9f78f4`.

## Milestone 3 automated result

GitHub Actions Build [run #14](https://github.com/MetTheVeloper/GrassiBoard/actions/runs/30704540252) passed on Windows x64, as did the separate Driver Placeholder Check. Native `/W4 /WX` compilation, Pitch/Formant/live-switch tests, managed tests, WPF publishing, package validation, and all four artifact uploads succeeded.

Measured latency / single-core CPU / frequency error was `26.67 ms / 1.39% / 3.62%` for Low latency, `53.33 ms / 1.42% / 1.77%` for Balanced, and `150.00 ms / 1.53% / 0.03%` for High quality. The three warm processors summed to an estimated `4.34%` of one runner core. Balanced passed the default policy; Low latency missed its 3% frequency-error limit, while High quality added 96.67 ms over Balanced.

Formant preservation and `+6` shift produced RMS differences of `0.13` and `0.11`. Live automated switching recorded a maximum adjacent-sample step of `0.06` and a longest near-silent run of one sample.

## Manual acceptance

Milestone 3 was accepted on 2026-08-01 on Windows 10 with a Microsoft LifeChat LX-3000. The user confirmed that every requested test passed, including Formant controls and live switching among all three quality modes. Screenshot evidence showed `v0.4.0`, commit `53539541`, the engine remaining active, and the expected latencies: Low latency `1280 / 26.7 ms`, Balanced `2560 / 53.3 ms`, and High quality `7200 / 150.0 ms`. Device buffers remained at Capture/Render `1056/1056`; observed U/O were `2/0`.

Milestone 3 acceptance unblocked Milestone 4.

## Previous accepted milestones

Milestone 2 (`v0.3.0`, commit `28378232`) was accepted on 2026-08-01 on Windows 10 with a Microsoft LifeChat LX-3000. The user reported no issues. Screenshot evidence showed the engine running at `+12` semitones, Pitch Latency `2560` samples / `53.3 ms`, Capture `1056`, Render `576`, Ring Fill `576`, and U/O/D `2/0/3`.

Milestone 1 (`v0.2.0`) was accepted on the same target with physical microphone monitoring, moving meters, and repeated Start/Stop. Milestone 0 (`v0.1.0`) was accepted with the app, BuildInfo, and native engine load verified.
