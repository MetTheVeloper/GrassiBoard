# GrassiBoard Remote — Development Status

> **Purpose:** authoritative handoff/status file for the GrassiBoard Remote roadmap.  
> **Roadmap:** `docs/remote-roadmap.md`  
> **Rule:** read this file before starting any Remote-development work.

---

# 1. Current baseline

```text
Current stable GrassiBoard release: v1.0.1
Baseline status: USER ACCEPTED / STABLE
Remote roadmap status: IN DEVELOPMENT
Current permitted Remote target: v1.1.x — Remote Control
```

The existing v1.0.1 desktop audio route, Voice DSP, Mixer, Soundboard, Media Deck, Profiles/Presets, Hotkeys/Tray, VB-CABLE routing, installer, and current UI are the regression baseline.

Do not begin v1.2 until v1.1 is explicitly user-accepted.

Do not begin v1.3 until v1.2 is explicitly user-accepted.

---

# 2. User / Codex workflow contract

The user does not edit source code.

Codex performs all source/documentation/CI changes.

The user only:

```text
Downloads the artifact/installer
Runs it on real Windows hardware
Tests with the Android phone and target apps
Reports results
Explicitly approves or rejects the stage
```

CI success does not count as user acceptance.

---

# 3. Acceptance rule

Only explicit user approval may change a stage to `USER ACCEPTED`.

Examples:

```text
تایید شد
همه چیز درست کار می‌کند
این مرحله نهایی است، برو بعدی
Approved
```

If testing fails:

```text
stay on the same version family
fix the reported issue
increment patch version when appropriate
rebuild
retest
```

Never skip ahead because the code “looks correct.”

---

# 4. Current work

```text
Stage: v1.1.x — Remote Control
Status: IMPLEMENTATION CANDIDATE / AWAITING CI
Implementation version: v1.1.0
Baseline commit: 4aa960d (v1.0.1 source baseline)
Implementation commit: TBD after patch is applied/committed
GitHub Actions run: TBD
Artifact: TBD
Awaiting user test: AFTER A GREEN CI ARTIFACT
```

## Current objective

Build the controller-only Remote defined in `docs/remote-roadmap.md`:

```text
Embedded LAN web server
Pairing / trusted clients
Nuxt 4 / Vue 3 / TypeScript / pnpm static SPA
WebSocket realtime state synchronization
Sound Pads
Voice FX / Pitch / Formant
User Presets
Compact Mixer
Media Deck controls
Mic Mute
Stop All
Engine Start/State
Responsive portrait/landscape UI
Haptics when supported
Reconnect/state recovery
```

No Remote Monitor audio yet.

No integrated phone microphone yet.

---

# 5. Version status table

| Version family | Feature | Implementation status | User acceptance | Notes |
|---|---|---|---|---|
| v1.0.1 | Stable desktop baseline | Complete | **USER ACCEPTED** | Regression baseline |
| v1.1.x | Remote Control | v1.1.0 candidate implemented | **NOT ACCEPTED** | Awaiting CI + real-device test |
| v1.2.x | Remote Monitor | Blocked | **NOT ACCEPTED** | Start only after v1.1 approval |
| v1.3.x | Full-Duplex Remote Audio | Blocked | **NOT ACCEPTED** | Start only after v1.2 approval |

---

# 6. v1.1 acceptance record

```text
Status: NOT ACCEPTED
Accepted version: —
Accepted date: —
Commit SHA: —
GitHub Actions run: —
Artifact tested: —
Android device/browser: —
Windows version: —
Target app(s): —
```

## v1.1 user test result

```text
Pairing: NOT TESTED
Realtime Pad sync: NOT TESTED
Pad playback from phone: NOT TESTED
Voice controls: NOT TESTED
Preset apply: NOT TESTED
Mixer controls: NOT TESTED
Media controls: NOT TESTED
Mic Mute: NOT TESTED
Stop All: NOT TESTED
Remote Engine Start: NOT TESTED
Reconnect: NOT TESTED
Portrait UI: NOT TESTED
Landscape UI: NOT TESTED
Desktop audio regression: NOT TESTED
```

## v1.1 known issues

```text
GitHub Actions CI iteration 4 passed Nuxt generation, native build/tests, and managed compilation, but the managed smoke test hung in the Remote preset transition. Root cause: after the native-call guard fixed the prior exception, the managed-only test could enter the timed WPF preset transition without a pumping Dispatcher. Hotfix: when NativeReady is false, preset snapshots apply immediately with no animation; the smoke contract also has a 5-second timeout so future regressions fail fast instead of consuming the full CI job timeout.
Authoring environment cannot run dotnet or resolve pnpm packages from the npm registry, so the real Windows/.NET/Nuxt build has not been executed here.
The initial v1.1.0 candidate intentionally uses exact direct frontend dependency versions with `pnpm install --no-frozen-lockfile`; a generated `pnpm-lock.yaml` is still required before final v1.1 acceptance, after dependency resolution is available.
Windows Firewall reachability still requires real Windows 10/11 + Android testing.
```

## v1.1 final acceptance commit message

```text
Provide only after explicit user approval.
```

---

# 7. v1.2 acceptance record

```text
Status: BLOCKED UNTIL v1.1 USER ACCEPTED
Accepted version: —
Accepted date: —
Commit SHA: —
GitHub Actions run: —
Artifact tested: —
Android device/browser: —
Windows version: —
```

## v1.2 user test result

```text
System/output loopback: NOT TESTED
Process-specific capture: NOT TESTED / capability-gated
Remote Monitor WebRTC: NOT TESTED
Soundboard monitor: NOT TESTED
Media monitor: NOT TESTED
Duplicate Media prevention: NOT TESTED
Independent monitor gains: NOT TESTED
My Voice default OFF: NOT TESTED
X Spaces scenario: NOT TESTED
Long-duration monitor: NOT TESTED
Control survives monitor failure: NOT TESTED
Desktop audio regression: NOT TESTED
```

## v1.2 WebRTC technology decision

```text
Library/stack: TBD
Pinned version: TBD
License reviewed: NO
CI verified: NO
Android browser interop verified: NO
```

## v1.2 known issues

```text
None recorded yet.
```

## v1.2 final acceptance commit message

```text
Provide only after explicit user approval.
```

---

# 8. v1.3 acceptance record

```text
Status: BLOCKED UNTIL v1.2 USER ACCEPTED
Accepted version: —
Accepted date: —
Commit SHA: —
GitHub Actions run: —
Artifact tested: —
Android client mode: browser HTTPS / companion shell / TBD
Windows version: —
```

## v1.3 secure-context decision

```text
Technology spike status: NOT STARTED
Chosen path:
[ ] HTTPS LAN web client
[ ] Minimal Android companion shell
[ ] Other approved solution

Reasoning: —
Certificate/onboarding method: —
```

## v1.3 user test result

```text
Phone mic permission/onboarding: NOT TESTED
Remote phone mic input: NOT TESTED
Pitch/Formant on phone mic: NOT TESTED
Preset on phone mic: NOT TESTED
VB-CABLE target receives phone mic: NOT TESTED
Full Duplex: NOT TESTED
Echo behavior: NOT TESTED
Disconnect safe-mute: NOT TESTED
Reconnect: NOT TESTED
Physical mic switch-back: NOT TESTED
X Spaces end-to-end: NOT TESTED
30–60 minute session: NOT TESTED
```

## v1.3 known issues

```text
None recorded yet.
```

## v1.3 final acceptance commit message

```text
Provide only after explicit user approval.
```

---

# 9. Iteration log

Append a short entry after every implementation/test iteration.

Template:

```text
## YYYY-MM-DD — vX.Y.Z — <short title>

Implementation:
- ...

Automated verification:
- ...

GitHub Actions:
- Run: ...
- Result: ...

Manual test requested:
- ...

User result:
- Awaiting / Failed / Partial / Approved

Reported issues:
- ...

Next action:
- ...
```

Do not rewrite history. Append new entries.


## 2026-08-11 — v1.1.0 — first Remote Control implementation candidate

Implementation:
- Added embedded ASP.NET Core/Kestrel LAN Remote server without changing native ABI 8.
- Added protocol v1, authoritative state snapshots, command allowlist/range validation, reconnect-safe WebSocket behavior, and coalesced state publication.
- Added two-minute one-time QR/code pairing, hashed persistent client credentials, device revoke, and LAN-only address selection.
- Added Settings Remote Control UI with QR, pairing code, status, restart, and paired-device revoke.
- Added Nuxt 4 + Vue 3 + TypeScript + pnpm static SPA with Board, Voice, Mixer, Media, haptics, landscape layout, and hold-to-confirm Stop All.
- Added CI generation/publish/package checks for RemoteWeb and QRCoder.
- Added managed smoke coverage for pairing, revoke, expiration, protocol parsing, snapshot privacy, parameter validation, preset routing, and revision/reconnect state.
- Updated version metadata to 1.1.0 for the test candidate; stable accepted release remains v1.0.1 until user approval.

Automated verification:
- Source/static verification performed in the authoring environment.
- Actual dotnet/native/Nuxt build not available in this environment; GitHub Actions is the authoritative next build gate.

GitHub Actions:
- Run: TBD after user applies/pushes patch.
- Result: NOT RUN.

Manual test requested:
- Pairing/revoke, Pads, Voice/Presets, Mixer, Media, Engine Stop All/Start, reconnect, portrait/landscape, and complete v1.0.1 regression matrix after a green artifact.

User result:
- Awaiting.

Reported issues:
- None yet.

Next action:
- Apply patch, run GitHub Actions, fix v1.1.x only if CI/manual testing reports failures; do not start v1.2.

---

# 10. Accepted-release log

Append one entry only after explicit final user acceptance.

Template:

```text
## vX.Y.Z — USER ACCEPTED

Date:
Commit:
GitHub Actions run:
Artifact:

User-confirmed behavior:
- ...

Regression baseline carried forward:
- ...

Commit message supplied to user:
`...`

Next permitted target:
vX.Y.0
```

---

# 11. What the next assistant/Codex session must do

Before touching code:

```text
1. Read this file.
2. Read docs/remote-roadmap.md.
3. Read docs/current-status.md.
4. Inspect the current repository state and version.
5. Identify the latest iteration log entry.
6. Continue only the currently permitted stage.
```

If this file conflicts with chat memory, prefer the newest repository evidence plus the latest explicit user approval.

Never claim a Remote stage is accepted without an acceptance entry in this file backed by explicit user approval.
