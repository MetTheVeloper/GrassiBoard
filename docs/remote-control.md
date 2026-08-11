# GrassiBoard Remote Control — v1.1

> Status: v1.1.0 implementation candidate. Not user-accepted yet.

## Scope

v1.1 turns an Android phone browser on the same private LAN/Wi-Fi into a realtime controller for the existing Windows GrassiBoard session. It intentionally carries **control/state only**. Remote Monitor audio belongs to v1.2 and integrated phone microphone audio belongs to v1.3.

## Architecture

```text
Android browser
  Nuxt 4 / Vue 3 static SPA
        |
        | HTTP + authenticated WebSocket
        v
GrassiBoard.App
  ASP.NET Core / Kestrel
        |
        v
RemoteCommandDispatcher
        |
        v
Existing MainViewModel / services / native-engine control surface
```

The Windows application is authoritative. The browser never maintains a second persistent copy of Pads, Presets, Voice, Mixer, Media, or Engine state. After authentication and after every reconnect the server sends a fresh full snapshot. Subsequent state invalidations are coalesced before publication.

The Remote server does not call native audio callbacks, perform audio-rate transport, or change the native ABI. v1.1 keeps native ABI 8 and the existing VB-CABLE route intact.

## Frontend

Repository location:

```text
src/GrassiBoard.RemoteWeb/
```

Technology:

```text
Nuxt 4
Vue 3
TypeScript
pnpm
ssr: false
pnpm generate
```

Nuxt is frontend-only in production. There are no `server/api` endpoints and no Node/Nitro runtime in the installed Windows application. CI generates `.output/public`; the WPF publish copies that directory to `RemoteWeb/`, and Kestrel serves it.

Local frontend development can point at a running GrassiBoard server using `NUXT_PUBLIC_REMOTE_ORIGIN`. Development CORS is intentionally limited to loopback browser origins.

## Remote pages

### Board

- active Profile Pads
- Ready/error/playing/loop state
- play and stop Pad
- Engine state
- Start Engine when stopped
- global Mic Mute and hold-to-confirm Stop All in the shared shell

### Voice

- Voice FX on/off
- Pitch
- Fine Pitch
- Formant
- Preserve Vocal Character
- user presets through the existing smooth preset transition
- Reset Voice

### Mixer

- Mic Gain
- Soundboard Gain
- Master Gain
- Mic Mute

### Media

- loaded display filename only; no full local path is sent
- Play/Pause
- Stop
- ±10 seconds
- timeline seek
- volume
- local monitor toggle
- send-to-virtual-mic toggle

## Pairing and credentials

Remote Control is off by default for a fresh settings file. When enabled, GrassiBoard binds Kestrel to a selected RFC1918 private IPv4 address rather than a wildcard Internet-facing address.

First-time pairing:

1. GrassiBoard generates a cryptographically random one-time secret plus a six-digit fallback code.
2. Settings displays a QR containing the LAN URL and one-time secret.
3. The browser exchanges that one-time value for a random client credential.
4. The one-time pairing value expires after two minutes and is invalidated after successful use.
5. The browser stores the client token locally.
6. GrassiBoard stores only a SHA-256 token hash in `%APPDATA%\GrassiBoard\remote-settings.json`.
7. The reusable token is sent as the first authenticated WebSocket message, never in the WebSocket query string.
8. Revoking a device removes its stored hash and closes its live connection.

After QR pairing succeeds, the SPA removes the one-time secret from the visible browser URL.

## Protocol v1

Every client command uses a unique `messageId` and protocol version 1. The server allowlists command families and validates numeric ranges/IDs before applying them.

Representative commands:

```text
engine.start / engine.stop / engine.stopAll
mic.mute.set
voice.fx.set / voice.pitch.set / voice.finePitch.set / voice.formant.set
voice.preserveCharacter.set / voice.reset
preset.apply
pad.play / pad.stop
mixer.gain.set
media.playPause / media.stop / media.skip / media.seek
media.volume.set / media.monitor.set / media.send.set
```

The server returns `ack` or structured `error` messages. State is returned using `state.snapshot` and a monotonically increasing revision.

## Reconnect behavior

The browser reconnects with exponential backoff after temporary Wi-Fi/browser interruption. It never queues commands while disconnected, so a stale Pad, Stop All, or other command cannot replay after reconnection. Once authenticated, the browser discards stale assumptions and replaces its local view with the latest full snapshot.

## Security boundaries

v1.1 deliberately does not expose:

- arbitrary shell/process execution
- arbitrary file reads/writes
- registry operations
- driver/installer operations
- full local Media/Pad paths
- microphone or monitor audio transport

The product remains LAN-first and does not configure Internet port forwarding.

## Build and packaging

The GitHub Actions flow builds RemoteWeb before managed/native publish, then verifies:

- generated `RemoteWeb/index.html`
- generated `_nuxt` client assets
- `QRCoder.dll`
- existing native/runtime package contract

The user does not install Node.js or pnpm on the target PC.

## v1.1 manual test order

1. Enable Remote, scan QR, pair, reload, then revoke and verify access is denied.
2. Check Pad list and play/stop; add/edit/delete a Pad on Windows and verify live phone updates.
3. Change Voice FX, Pitch, Fine Pitch, Formant, and presets from the phone; verify Desktop state and actual processed output.
4. Check Mic/Soundboard/Master gains and Mic Mute.
5. Check Media Play/Pause, Stop, ±10, seek, volume, Monitor, and Send.
6. Use Stop All, then restart the Engine from the phone without touching the PC.
7. Toggle phone Wi-Fi, background/foreground the browser, and lock/unlock the phone; verify reconnect and no stale command replay.
8. Test portrait and landscape layouts.
9. Regression-test desktop Soundboard, Hotkeys, Tray, Voice DSP, Media Deck, and VB-CABLE routing.

A successful CI build is not acceptance. v1.1 becomes accepted only after explicit real-device user approval and the acceptance record is updated in `docs/remote-development-status.md`.
