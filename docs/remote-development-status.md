# GrassiBoard Remote development status

> Authoritative handoff for Remote work. Repository status wins over chat history when they disagree.

## Current accepted baseline

- Published/stable desktop baseline: **v1.0.1 USER ACCEPTED**
- Native ABI baseline: **8**
- Virtual microphone route: **external VB-CABLE, unchanged**
- Current Remote stage: **v1.1.0 GrassiMote Remote Control consolidation**
- Next roadmap stage: **v1.2 Remote Monitor, blocked until the consolidated v1.1 CI run is green**

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

## Remaining release gate — FINAL CI

Status: **USER MANUAL ACCEPTANCE RECORDED / FINAL CONSOLIDATED GITHUB ACTIONS RUN PENDING**

Before v1.2 work begins, the consolidated repository must be pushed and GitHub Actions must complete successfully. The final RemoteWeb release restore requires the committed `src/GrassiBoard.RemoteWeb/pnpm-lock.yaml` and uses `pnpm install --frozen-lockfile`. After the green run, record the resulting commit SHA and Actions run here and mark v1.1 **USER ACCEPTED / CI VERIFIED**.

Do not begin v1.2 implementation before that green run.

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
- Release CI generates and verifies PWA assets and now requires a committed frozen pnpm lockfile.

## Security / privacy boundaries

- LAN-first only; no automatic Internet exposure or port forwarding.
- No arbitrary shell, registry, process-launch, file-read, or file-write Remote API.
- Reusable pairing tokens are hashed on Windows and are not logged.
- CA/server private keys are not shipped in the repository and are not served to the phone.
- Remote audio transport is **not** implemented in v1.1.
- No custom virtual driver is revived; external VB-CABLE remains the program-output route.

## Final CI checklist

- [ ] `src/GrassiBoard.RemoteWeb/pnpm-lock.yaml` is committed.
- [ ] `pnpm install --frozen-lockfile` passes.
- [ ] Nuxt PWA generation/assets pass.
- [ ] Native configure/build/tests pass with ABI 8.
- [ ] Managed smoke tests pass without hang.
- [ ] Self-contained WPF publish passes.
- [ ] Portable package verification passes.
- [ ] Installer publish/verification passes.
- [ ] Artifacts upload successfully.
- [ ] Record final commit SHA and GitHub Actions run here.
- [ ] Mark v1.1 `USER ACCEPTED / CI VERIFIED`, then unlock v1.2.
