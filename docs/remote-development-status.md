# GrassiBoard Remote development status

> Authoritative handoff for Remote work. Repository status wins over chat history when they disagree.

## Current accepted baseline

- Published/stable desktop baseline: **v1.0.1 USER ACCEPTED**
- Native ABI baseline: **9 for the v1.2 personal-stable production candidate**; v1.0.1 remains the historical ABI-8 stable baseline
- Virtual microphone route: **external VB-CABLE, unchanged**
- Current accepted Remote stage: **v1.1.0 GrassiMote Remote Control — USER ACCEPTED / CI VERIFIED**
- Accepted UI stage: **Material 3 GrassiMote redesign — USER ACCEPTED**
- Current development stage: **v1.2 Remote Monitor — PERSONAL-STABLE PRODUCTION CANDIDATE / FINAL SOAK PENDING**

## v1.1 manual acceptance — PASSED

On **2026-08-11** the user explicitly confirmed successful real-device operation and asked to proceed to the next stage. The following v1.1 path is therefore manually accepted:

- Windows GrassiBoard Remote server starts without disturbing the accepted native audio route.
- Android can complete the one-time local-CA onboarding.
- The secure compatibility origin `https://<LAN-IP>:47919/` opens successfully.
- GrassiMote installs as a standalone PWA on Android.
- In-app **SCAN QR** pairing works and produces an authenticated **WSS Connected** session.
- Board, Voice, Mixer, Media, Mute, presets, and engine controls apply to Windows in realtime; prior testing also confirmed authoritative desktop/phone synchronization.
- The control path is subjectively responsive on the user's real LAN/hotspot setup.
- Native ABI remains **8** and the VB-CABLE Program Mix route is unchanged.

`grassimote.local` is **not** an acceptance requirement. It remains an optional convenience alias because the user's Android hotspot/mobile-data topology returned `.local` NXDOMAIN even with VPNs disabled. The direct secure LAN-IP path is the supported compatibility path.

## v1.1 final release gate — PASSED

Status: **USER ACCEPTED / CI VERIFIED**

- Functional v1.1 commit: `6f3b672` — `feat(remote): complete v1.1 realtime GrassiMote control`
- Accepted Material 3 UI commit: `7500901` — `feat(remote-ui): finalize Material 3 GrassiMote redesign`
- GitHub Actions: **Build #84 — SUCCESS**
- Actions run id: `31529903075`
- Result included successful build/test/package artifact production.

v1.2 is now unlocked, but production Remote Monitor implementation remains gated by the roadmap's WebRTC technology spike and dependency/license review.

## Consolidated v1.1 implementation

### Remote control / state

- Embedded ASP.NET Core/Kestrel server inside `GrassiBoard.App`.
- Protocol-v1 authenticated WebSocket/WSS command/state channel with ACK/error messages and authoritative snapshots.
- Pair/revoke flow with short-lived QR/code secrets and reusable client credentials stored hashed server-side.
- Reconnect with fresh snapshot and no stale command replay.
- Live Board, Voice, Mixer, Media, Mic Mute, Stop All, and Engine lifecycle controls.
- No full local audio file paths exposed to the Remote.

### GrassiMote

- Nuxt 4 + Vue 3 + TypeScript, frontend-only (`ssr: false`).
- `pnpm generate` static output; no Node/Nuxt runtime required on the installed Windows PC.
- PWA manifest, icons, Service Worker/offline shell, standalone installation, and install prompt support.
- Explicit in-app QR scanner with camera permission requested only after user action.
- LAN-safe message IDs that do not depend on `crypto.randomUUID()` being available on insecure HTTP bootstrap pages.

### Secure origin / LAN compatibility

- HTTP bootstrap/onboarding on port **47918**.
- HTTPS/WSS GrassiMote on port **47919**.
- Per-PC private GrassiMote CA plus server certificate generated under user AppData; only the public CA certificate is downloadable.
- Server certificate covers the current private IPv4 address and `grassimote.local`.
- SChannel-compatible persisted server private key (`UserKeySet | PersistKeySet | Exportable`).
- Direct secure LAN IP is primary compatibility mode.
- Lightweight mDNS responder includes RFC 6762 legacy-unicast handling, but `.local` remains optional.
- Network-address selection deprioritizes common VPN/virtual adapters when a real private Wi-Fi/Ethernet address is available.

### Build/test tooling and CI fixes

- pnpm 11 `esbuild` build-script allowlist.
- Managed Remote compile fixes and bounded preset smoke tests.
- Native-ready guard prevents managed-only preset tests from touching an unavailable native engine.
- Preset test Dispatcher deadlock removed; Remote preset test has a short timeout.
- Bounded/abortable Remote WebSocket shutdown prevents GrassiBoard from lingering during Exit.
- `Deploy-RemoteWebLocal.ps1` supports fast frontend-only deployment and force-stops the running app before replacement.
- `Build-LocalRemoteTest.ps1` supports fast managed+web local builds, project-local bootstrap of exact .NET SDK **8.0.423**, reuse of the accepted ABI-8 native DLL, robust process termination, and optional smoke tests.
- ABI-9 spike local builds resolve Visual Studio-bundled `cmake.exe`/`ctest.exe` through `vswhere` and both Program Files roots, so Build Tools installations do not need CMake added to the global `PATH`.
- ABI-9 local native-build stdout is kept out of PowerShell's function return stream, preventing CMake/MSBuild log lines from corrupting the resolved native DLL path during publish/copy.
- Release CI generates and verifies PWA assets and now requires a committed frozen pnpm lockfile.

## Material 3 UI/UX redesign — USER ACCEPTED

The Material 3 redesign was manually verified on Android in portrait and intentional landscape/deck layouts. All Board, Voice, Mixer, and Media controls were visible and usable after the wrapper-registration fixes. It leaves the Remote protocol, pairing, WSS connection logic, authoritative state model, command names, Kestrel endpoints, certificate onboarding, and native audio route unchanged.

Accepted scope:

- `@material/web` **2.5.0**, pinned, with explicit stable component imports only; no `labs` dependency in critical UI paths.
- Nuxt compile-time `md-*` custom-element recognition.
- GrassiBoard `Gb*` wrapper layer for buttons, icon buttons, switches, sliders, chips, status, and empty states.
- Self-hosted Material Symbols Rounded variable font; no runtime Google Fonts/CDN dependency.
- Semantic GrassiBoard design tokens mapped onto Material system/component tokens.
- Redesigned global session header, connection/live/mute status, bottom navigation, wide/landscape navigation rail, quick actions, and destructive Stop All hold behavior.
- Redesigned Board, Voice, Mixer, Media, pairing, and QR scanner surfaces with mobile-first touch targets and explicit realtime states.
- Deliberate landscape control-deck composition, safe-area handling, reduced-motion support, and lightweight telemetry rendering.
- PWA shell cache bumped so installed GrassiMote clients receive the redesigned frontend.


## Security / privacy boundaries

- LAN-first only; no automatic Internet exposure or port forwarding.
- No arbitrary shell, registry, process-launch, file-read, or file-write Remote API.
- Reusable pairing tokens are hashed on Windows and are not logged.
- CA/server private keys are not shipped in the repository and are not served to the phone.
- Remote audio transport is **not** implemented in v1.1.
- No custom virtual driver is revived; external VB-CABLE remains the program-output route.

## v1.2 current gate

- [x] v1.1 real-device manual acceptance
- [x] v1.1 final GitHub Actions verification
- [x] Material 3 portrait/landscape manual acceptance
- [x] Build v1.2 WebRTC/Opus spike locally with `-RemoteMonitorSpike`
- [x] Hear stable synthetic audio on Android Chrome over the accepted LAN path
- [x] Record Peer/ICE connected + track received + audible Opus result
- [x] Validate default Windows render-endpoint WASAPI loopback → Opus → Android
- [x] Confirm real Windows media playback is audible on Android over the live WebRTC peer
- [x] Validate automatic capture handoff when the Windows default render endpoint changes
- [x] Measure the first real-device receive path: ~100–104 kbps typical (occasional ~64), 22–24 ms jitter, 0 packet loss, Opus, 48 kHz stereo / 20 ms capture
- [x] Validate the explicit 128 kbps VBR / complexity-10 Windows-loopback Opus quality profile: real-device audio is clear, pitch-correct, effectively latency-free by ear, ~132 kbps observed, ~25 ms jitter, 0 packet loss
- [x] Validate PWA session preservation/recovery: route navigation is seamless; minimize no longer destroys the session and foreground recovery is automatic. Android may locally mute/suspend background playback; overlay/PiP-style use keeps continuous playback and is accepted as the web-only workaround for this stage
- [x] Validate experimental ABI-9 internal Soundboard tap → Opus → Android while leaving Program/VB-CABLE behavior unchanged
- [x] **Gate 4A USER ACCEPTED 2026-08-12:** Windows + Soundboard independent Monitor Mix is clean after the full-frame cadence/prebuffer/limiter stability fix; monitor-only gains and Program/VB-CABLE isolation passed on the real device
- [x] **Gate 4B USER ACCEPTED 2026-08-12:** direct Media contribution and automatic duplicate prevention passed on the real Windows + Android path; direct Media gain works when local monitoring is off, same-endpoint local monitoring is correctly folded into Windows / Space, and Windows / Space + Monitor Master controls behave as designed during suppression
- [x] **Gate 4C USER ACCEPTED 2026-08-12:** processed My Voice opt-in passed on the real Windows + Android path; Voice FX/Mute/isolation/stability are correct, no crackle/stutter was reported, and acoustic feedback is eliminated by using phone headphones/earbuds as designed
- [x] **Monitor UX brutal-minimal baseline manually reviewed 2026-08-12:** the user approved the compact first-glance layout with levels/diagnostics collapsed on demand
- [x] **Interactive quick-tile UX USER ACCEPTED 2026-08-12:** the six first-glance tiles are directly actionable with tap/horizontal-drag gain control, dedicated Voice ON/OFF, separate Voice Level, subtle percentage fill, haptic quarter-step ticks, and duplicate-aware Media behavior; the user explicitly reported all interactions working with no issue
- [x] **WebRTC dependency decision — ACCEPTED FOR CURRENT PERSONAL USE 2026-08-12:** keep the already-tested SIPSorcery 10.0.13 transport unchanged. GrassiBoard is currently a private/personal tool with no planned public distribution; re-open the license review before any future public/commercial distribution.


### Interactive brutal-minimal quick tiles — USER ACCEPTED 2026-08-12

The user explicitly confirmed the Hotfix 34 Monitor quick-tile interaction model works without issues. The accepted first-glance 3×2 control surface now supports direct horizontal tap/drag for Windows, Board, Media, Voice Level and Master; direct Voice ON/OFF; percentage background fill; quarter-step haptics where supported; and duplicate-aware Media behavior. The conventional Monitor Levels disclosure remains available as the precise fallback. This acceptance is UI-only and does not alter the already accepted Remote Monitor audio path.

### WebRTC dependency/license gate — ACCEPTED FOR CURRENT PERSONAL USE

The tested v1.2 transport remains **SIPSorcery 10.0.13**. A fresh upstream review found that current SIPSorcery licensing is not an unmodified BSD-3-Clause grant: it includes a geographic field-of-use/distribution restriction. Outside the restricted geography, the upstream text states that BSD-3-Clause terms apply without extra commercial-use or derivative-work licensing requirements.

The older **v8.0.12** tag is **not** selected as a workaround. Although its headline grant is BSD-3-Clause, that tag's own license file explicitly documents an unresolved provenance/AGPL claim around a small number of DTLS/SRTP-derived files and advises downstream users to assume an AGPL claim or remove the affected files (which would break DTLS/SRTP/WebRTC). That makes the old tag a worse production choice for GrassiBoard.

Therefore the engineering recommendation is:

- keep the already real-device-tested SIPSorcery 10.0.13 transport unchanged for now;
- keep the exact transport already validated on Windows + Android;
- promote ABI 9 + Remote Monitor into the v1.2 personal-stable production candidate;
- keep public/commercial distribution out of scope until the license is reviewed again;
- run one final 30–60 minute soak plus the normal CI/installer/portable verification before marking v1.2 USER ACCEPTED.

This is a dependency policy decision, not a new audio defect. The accepted monitor implementation remains stable.

### Hotfix 36 — v1.2 personal-stable production candidate

The user explicitly chose to keep the current tested WebRTC transport for personal/private use and asked to finalize v1.2 before beginning v1.3.

Hotfix 36 promotes the accepted monitor stack into the normal local/release build defaults without changing its audio behavior:

- normal managed builds enable the accepted Remote Monitor compile path by default;
- normal `windows-x64-release` native builds now produce ABI 9 with Soundboard + processed-Voice taps;
- native engine version reports `1.2.0` instead of `1.2.0-spike`;
- the local test build no longer depends on `-RemoteMonitorSpike` and no longer reuses an installed ABI-8 DLL;
- v1.2 native tests run on the normal release preset;
- local BuildInfo reports `1.2.0`;
- installer/build version metadata is migrated to `1.2.0` by the included apply script;
- public distribution remains out of scope; dependency review must be reopened if that changes.

**Final v1.2 acceptance is intentionally still pending one final production-candidate validation:** build/smoke success, normal GrassiMote control/monitor regression check, and a 30–60 minute mixed-audio soak with no growing delay, dropouts, memory runaway, or engine instability.

### Hotfix 36b — native ABI smoke version assertion fix

The first normal v1.2 production-candidate build successfully configured and built the ABI-9 native engine with `GRASSIBOARD_REMOTE_MONITOR_TAP=ON`. Six of seven native tests passed. The only failure was `GrassiBoard.AudioEngine.AbiSmokeTest`, because the test source still expected the historical engine string `1.2.0-spike` while Hotfix 36 correctly changed the production engine version to `1.2.0`.

Hotfix 36b updates only that stale test expectation. No native engine, DSP, monitor tap, WebRTC, Opus, UI, routing, or VB-CABLE behavior is changed.

Re-run the same normal v1.2 build command after applying this patch. Final v1.2 acceptance remains pending the normal-build smoke/regression/soak gate.

### Hotfix 36c — native version sync + clean rebuild

The ABI smoke test still failed after Hotfix 36b, which means the running test process was still seeing a native engine version different from the production expectation even though ABI 9 itself loaded successfully. Hotfix 36c removes ambiguity by shipping both synchronized runtime/test sources (`1.2.0`), improving the smoke-test failure message to print actual vs expected versions, and providing a PowerShell 5.1-safe one-time script that deletes the `windows-x64-release` CMake/MSBuild output before the next build.

This is a build-cache/version-synchronization fix only. No DSP, monitor mix, WebRTC, Opus, UI, routing, or VB-CABLE behavior changes.

### Hotfix 36d — managed product-version contract fix

After Hotfix 36c, the normal ABI-9 native production build passed **7/7 native tests**. The next gate failed immediately in the managed smoke suite with `Managed version/native ABI contract is inconsistent.`

The cause is a separate managed product-version constant in `GrassiBoard.Shared/BuildInfo.cs` that still reported the historical `1.0.1`. Hotfix 36 had already promoted the native engine and build metadata to `1.2.0`, so the smoke test correctly caught this remaining stale managed version source.

Hotfix 36d updates `BuildInfo.CurrentVersion` to `1.2.0` and makes the smoke-test error print the actual managed version and ABI if this contract ever regresses again. No audio, WebRTC, monitor-mix, UI, routing, or VB-CABLE behavior changes.

### Hotfix 36e — force managed dependency rebuild

The normal v1.2 native production candidate is now fully green at **7/7 native tests**. The managed smoke gate then reported `actual version=1.1.0, ABI=9` even after the shared source was promoted to `1.2.0`.

That exact combination proves the ABI-9 managed code is current while the compiled `GrassiBoard.Shared.dll` is stale from the previous v1.1 output. The likely trigger is incremental MSBuild timestamp reuse after ZIP-overwriting a source file with older archive metadata.

Hotfix 36e makes the smoke gate deterministic: before running managed smoke tests, `Build-LocalRemoteTest.ps1` now executes `dotnet build --no-incremental` for the smoke-test project and its project-reference dependency graph, then executes the tests with `dotnet run --no-build`. The correct `BuildInfo.CurrentVersion = 1.2.0` source is also re-shipped.

No audio, DSP, WebRTC, Opus, Monitor Mix, UI, routing, or VB-CABLE behavior changes.

### Hotfix 36f — production smoke-contract cleanup

The normal v1.2 native path remains green at 7/7 tests and the managed dependency graph now rebuilds correctly. The next managed smoke failure was a stale source-contract assertion that still required the historical local-build text `EnableRemoteMonitorSpike=true`.

Hotfix 36 intentionally removed that explicit spike-only property from the normal build path, so the old assertion was no longer valid. Hotfix 36f replaces it with production-candidate assertions for the normal v1.2 path: the personal-stable banner, the `windows-x64-release` preset, and the managed `--no-incremental` rebuild gate.

No runtime, audio, DSP, WebRTC, Opus, Monitor Mix, UI, routing, or VB-CABLE behavior changes.

### Latest ABI-9 Soundboard result — MANUALLY PASSED 2026-08-12

The user manually validated the ABI-9 internal Soundboard tap on the real Windows + Android path. Pads triggered from both Windows and GrassiMote were heard clearly and correctly on Android. Changing the normal Program **Soundboard Gain** and **Master Gain** did not change the direct Soundboard tap level, and the Program/VB-CABLE Soundboard route remained unchanged. This unlocks the first real independent multi-source Remote Monitor mix gate.

### Gate 4A independent Monitor Mix — USER ACCEPTED 2026-08-12

The stability follow-up fully removed the mix-only scratchy/micro-stutter artifact. The user repeated the requested Windows-only-inside-mix, Soundboard-only-inside-mix, simultaneous-source, monitor-gain, Program/VB-CABLE isolation, and continuity checks and explicitly reported the result as clean. Final observed Android receive metrics were approximately **128–133 kbps**, **7–11 ms jitter**, and **0 packet loss**. No growing delay was reported.

The accepted Gate 4A mixer therefore retains:
- complete ABI-9 Soundboard frames as the mix cadence;
- complete 20 ms Windows loopback frames after a small 40 ms reservoir;
- bounded loopback buffering;
- a monitor-only peak limiter;
- independent Windows / Space, Soundboard, and Monitor Master gains;
- no Program/VB-CABLE changes.

### Gate 4B direct Media + duplicate prevention — USER ACCEPTED 2026-08-12

The user explicitly approved moving on after validating both routing modes on the real Windows + Android path. With **Monitor in headphones OFF**, Media remains audible on GrassiMote through the direct 48 kHz stereo Media tap and the dedicated Media monitor-only slider changes only the phone level. With local Media monitoring enabled on the same endpoint captured by Windows loopback, duplicate prevention suppresses the direct tap and Media correctly follows the **Windows / Space** branch instead; the dedicated Media slider is intentionally retained for direct mode and no longer changes that already-embedded Windows copy. The user also confirmed **Windows / Space → 0%** removes that Media copy and **Monitor Master → 0%** silences the whole phone monitor, matching the designed bus logic. No Program/VB-CABLE change was reported.

### Gate 4C processed My Voice opt-in — USER ACCEPTED 2026-08-12

The user completed the real-device My Voice matrix successfully. Processed self-monitoring follows Pitch/Formant and Mic Mute, remains independent from Program Mic Gain/Master, and coexists cleanly with Windows / Space, Soundboard, and Media. The user reported no crackle, micro-stutter, or other audio artifact. The only observed issue was the expected acoustic feedback loop when the phone speaker was allowed to feed the physical microphone; connecting headphones/earbuds to the phone eliminated it completely and produced the intended stable behavior.

The accepted design remains: My Voice is **OFF by default**, explicitly enabled, tapped after Voice FX + Mic Mute and before Program Mic Gain/Master, and reset to OFF after an explicit Monitor stop. Program/VB-CABLE remains untouched.

### Current work — brutal-minimal interactive quick tiles

The user manually reviewed the first compact Monitor layout and explicitly liked the new hierarchy. The next UI-only iteration turns the first-glance 3×2 summary itself into the primary mixer control surface:

- **Windows**, **Board**, **Media**, **Voice Level**, and **Master** tiles behave as invisible horizontal sliders: tap sets a value, horizontal drag adjusts continuously, and vertical movement remains page scrolling;
- each gain tile uses a subtle left-to-right background fill proportional to its current percentage, inspired by Android system volume/brightness controls without copying their visual chrome;
- a dedicated **Voice** tile toggles My Voice ON/OFF, while a separate **Voice Lv** tile retains its monitor-only percentage;
- optional haptic ticks fire only when crossing 0/25/50/75/100 where browser vibration is available;
- when Media duplicate prevention is active, the Media tile becomes non-draggable, displays **Via Windows**, and its fill reflects the effective Windows/Space monitor gain instead of pretending the retained direct Media value is currently controlling audio;
- the full **Monitor levels** disclosure remains available for conventional precise sliders, and **Connection details / Advanced diagnostics** remain unchanged;
- this iteration remains RemoteWeb UI-only: no AudioEngine, WebRTC, Opus, routing, or Program/VB-CABLE behavior changes.

### Latest real-device quality/background result

The explicit 128 kbps VBR / complexity-10 profile removed the earlier audible degradation and pitch shift. The accepted device reported 48 kHz / 2-channel / 20 ms capture, Opus receive bitrate around the configured target, roughly 25 ms jitter, and zero packet loss while simultaneously playing multiple Windows audio sources. Changing the default Windows render endpoint also hands off automatically without renegotiating the WebRTC peer.

The installed PWA can keep the monitor session alive across GrassiMote route changes. On this Android device, ordinary minimization can suspend/local-mute audible playback even though the session is retained and restored automatically on foreground; keeping the PWA in the system overlay/floating view preserves continuous playback. This is accepted as a non-blocking browser/OS limitation for the web-only v1.2 path.

See `docs/remote-monitor-spike.md`.
