# Current status

- Version: `v0.5.1`
- Milestone: `4 — Virtual Driver Skeleton`
- Status: lifecycle hotfix automated acceptance passed; awaiting manual uninstall/reinstall/uninstall acceptance
- Target: Windows 10/11 x64
- DSP: live Pitch/Fine Pitch, Formant preservation/shift, Bypass, and three quality configurations
- Default: Balanced, selected by the committed benchmark policy
- Backend: Signalsmith Stretch 1.3.2 + Signalsmith Linear 0.3.1, both pinned
- Virtual driver: one test-signed render endpoint and one capture endpoint; PCM transport intentionally disabled until Milestone 5

## Milestone 4 implementation

The driver is a minimal extraction of Microsoft's SysVAD/WaveRT code pinned to commit `ef7c3074748ab05726c3a9161d3256118efd76e2`. It uses hardware ID `ROOT\GrassiBoardVirtualAudio` and exposes `GrassiBoard Virtual Cable Input` plus `GrassiBoard Virtual Microphone`.

CI builds the driver and native root-device helper from pinned WDK/SDK NuGet packages. An ephemeral certificate signs the SYS and generated CAT; only the public CER is packaged. Installation, removal, TESTSIGNING changes, and diagnostics are explicit scripts. No script changes Secure Boot or BitLocker and no script reboots automatically.

Manual acceptance is pending on Windows 10 x64. PCM cable transport is outside this milestone.

Windows 10 build 19045 testing of v0.5.0 confirmed that the driver, both endpoints, and Windows Audio were healthy, but exposed an installer lifecycle bug. The generated instance ID was `ROOT\GRASSIBOARD_VIRTUAL_AUDIO\0000`, while scripts incorrectly assumed that it matched hardware ID `ROOT\GrassiBoardVirtualAudio`. v0.5.1 identifies the device by its HardwareID property and recovers the installed INF and signer certificate before removal. Milestone 4 remains open until the corrected uninstall/reinstall/uninstall sequence passes.

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
