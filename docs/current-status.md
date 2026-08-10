# Current status

- Version: `v0.8.2`
- Milestone: `7 — Soundboard + UI architecture foundation`
- Status: Sound Pad/UI stability hotfix implemented; automated validation in progress; manual Windows 10 retest pending
- Target: Windows 10/11 x64
- DSP: live Pitch/Fine Pitch, Formant preservation/shift, Bypass, and three quality configurations
- Default: Balanced, selected by the committed benchmark policy
- Backend: Signalsmith Stretch 1.3.2 + Signalsmith Linear 0.3.1, both pinned
- Virtual routing: vendor-neutral external cable selected as the processed WASAPI render destination
- UI: persistent Board/Voice/Routing/Settings shell with shared top bar and state
- Soundboard: background WAV/MP3 decode, native cached mixer, 32 simultaneous voices, and JSON persistence

## v0.8.2 Sound Pad and UI stability hotfix

Windows 10 manual testing confirmed the v0.8.1 app starts and preserves the accepted microphone route, but creating the first Sound Pad materialized a deferred WPF template containing the invalid three-value margin `0,12,0`. That raised a `XamlParseException` and terminated the process. v0.8.2 corrects the template, validates XAML thickness values, provides theme-aware ComboBox templates, adds a custom title bar with persisted light/dark switching, and records unexpected runtime failures under `%LOCALAPPDATA%\GrassiBoard\CrashReports`.

## v0.7.0 manual acceptance

Milestone 6 was accepted on 2026-08-09 on Windows 10. The user confirmed that the processed Microsoft LifeChat microphone, including Pitch and the existing Voice controls, was successfully recorded in Telegram and Windows Voice Recorder through the installed VB-CABLE route. This acceptance freezes the v0.7 microphone/DSP/WASAPI behavior as the regression baseline for v0.8.0.

## v0.8.0 implementation boundary

v0.8.0 adds a native cached Soundboard mixer after the accepted Voice DSP and establishes the final application shell. Sound Pads never enter the microphone Pitch/Formant path. Mute Mic gates only the processed microphone branch; Stop All clears only Soundboard voices. WAV/MP3 decode, disk access, resampling, and large allocation happen on a background thread before PCM crosses the native ABI.

GitHub Actions [Build run #50](https://github.com/MetTheVeloper/GrassiBoard/actions/runs/31332077929) passed for implementation commit `7fead3f`. Native `/W4 /WX`, accepted DSP regressions, the isolated Soundboard mixer contract, managed WAV decode/JSON persistence, self-contained WPF publish, package verification, and all three artifact uploads succeeded.

The milestone is not accepted until the user explicitly approves the manual Windows 10 regression/UI/Soundboard report.

## v0.8.1 startup hotfix

Manual Windows 10 launch testing found that v0.8.0 exited before showing its window. WPF treated `ProgressBar.Value` bindings as TwoWay by default and attempted to write meter values back to read-only ViewModel properties. v0.8.1 makes every meter binding explicitly OneWay and adds a startup exception report at `%LOCALAPPDATA%\GrassiBoard\startup-error.txt` so any future initialization failure is visible instead of silent. The corrected build was compiled and launched locally on the affected Windows 10 system; the GrassiBoard window remained open and responsive.

GitHub Actions [v0.8.1 hotfix Build](https://github.com/MetTheVeloper/GrassiBoard/actions/runs/31333906678) passed native compilation/tests, the new managed meter-binding regression, WAV decode, JSON persistence, self-contained WPF publish, portable-package verification, and all artifact uploads for commit `efd7d4c`.

## Architecture decision

The custom SysVAD path is retired from product builds. Manual v0.6.5 testing confirmed that its capture endpoint still failed `GetMixFormat` and shared initialization with `AUDCLNT_E_UNSUPPORTED_FORMAT`; the expected Audio Engine mix-format property did not materialize. The source and diagnostic history remain in the repository, but official GrassiBoard packages no longer contain or publish that driver.

v0.7.0 and later send the accepted user-mode DSP output to an installed external cable. The UI pairs its playback and recording endpoints, ignores the retired GrassiBoard endpoints, and tells the user which microphone to select in the target application.

## Historical custom-driver implementation

The driver is a minimal extraction of Microsoft's SysVAD/WaveRT code pinned to commit `ef7c3074748ab05726c3a9161d3256118efd76e2`. It uses hardware ID `ROOT\GrassiBoardVirtualAudio` and exposes `GrassiBoard Virtual Cable Input` plus `GrassiBoard Virtual Microphone`.

The render stream advertises fixed 48 kHz, 16-bit stereo PCM. Its frames are downmixed without allocation into a fixed 48 kHz, 16-bit mono ring consumed by the capture stream, preserving Microsoft's reference external-MicIn channel contract. Capture uses a 10 ms pre-roll, zero-fills underruns, and invalidates queued frames whenever render or capture pauses/stops so old audio cannot repeat. The transport counts fill, underruns, and overruns.

The cable is deliberately testable without the GrassiBoard app. App-to-cable routing is Milestone 6.

## Historical Windows 10 capture finding

Manual testing on Windows 10 build 19045 found that the v0.6.0 through v0.6.2 endpoints were present and Device Manager reported `OK`, but Voice Recorder could not open the virtual microphone. A direct KS probe verified the capture filter, its two pins, all five processing modes, exact format negotiation, and successful pin creation. A direct WASAPI probe still reproduced `AUDCLNT_E_UNSUPPORTED_FORMAT` from `IAudioClient::GetMixFormat` and both shared/exclusive initialization paths.

A focused ETW trace exposed the underlying Audio Engine result as `0x80070490` (`Element not found`) during per-endpoint policy construction; Windows then mapped it to `AUDCLNT_E_UNSUPPORTED_FORMAT`. The unchanged v0.6.2 result disproved event-driven scheduling as the cause, just as v0.6.1 disproved the earlier stream-instance hypothesis. v0.6.3 restores event-driven capture and removes the remaining material divergence from Microsoft's working external MicIn reference: the capture endpoint is mono again, with a matching mono topology jack and default formats. The stereo render input is downmixed before entering the mono ring.

Manual v0.6.3 testing produced the same failure and disproved the mono-channel-contract hypothesis. Direct endpoint-volume and meter calls nevertheless succeed, including channel count, volume range, master level, mute, and peak queries. v0.6.4 therefore packages three cumulative variants in one CI run: explicit OEM format, official MicIn mode tables, and finally the official tone-capture stream without GrassiBoard ring participation.

All three v0.6.4 variants failed identically. The final variant used the untouched
SysVAD tone-capture path and official mode table, proving that the PCM ring and custom
capture stream were not involved. On the affected machine, the working GrassiBoard
render endpoint and the working LifeChat and AMM capture endpoints all contained the
Audio Engine mix-format and period policy properties under property-set GUID
`E4870E26-3CC5-4CD2-BA46-CA0A9A70ED04`. The broken GrassiBoard capture endpoint was
the only one missing property IDs `0` and `1`, matching the ETW `ERROR_NOT_FOUND`.
v0.6.5 seeds a 48 kHz/32-bit-float mono engine mix format and the standard 10 ms
period on the capture topology interface while retaining its 48 kHz/16-bit mono device
format.

## Historical custom-driver automated result

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
