# GrassiMote UI System — Material 3 integration

GrassiMote uses Material Design 3 as an interaction/component foundation while keeping GrassiBoard's dark blue live-audio identity.

## Architecture

- Nuxt 4 + Vue 3 + TypeScript remains unchanged.
- `ssr: false` and `pnpm generate` remain the production build path.
- `@material/web` is used only for stable foundational controls: buttons, icon buttons, switches, sliders, assist chips, and progress.
- Raw Material custom elements are kept behind small `Gb*` Vue wrappers where application semantics benefit from a stable boundary.
- The Remote protocol, WebSocket state, pairing, HTTPS/WSS, PWA, and command semantics are unchanged by the UI layer.

## Material Web integration

`nuxt.config.ts` marks the `md-*` prefix as custom elements at Vue compile time. Material Web component modules are imported from `app/plugins/material.client.ts`, so browser-only custom element registration does not introduce SSR/runtime-server requirements.

The project intentionally uses stable Material Web components only. No `labs` component is required by the v1.1 redesign candidate.

## Icons

The UI uses a small local SVG subset through `GbIcon`. The shapes follow Google Material Symbols / Material icon geometry and are bundled directly with the static application. No external icon font or Google Fonts runtime request is required, preserving LAN/offline PWA behavior and avoiding icon-ligature flashes.

## Tokens

`app/assets/main.css` is the source of truth for shared UI tokens:

- semantic GrassiBoard colors and realtime states;
- Material system-role mappings;
- spacing;
- shape hierarchy;
- motion/easing;
- touch sizing;
- safe-area-aware layout;
- Material component token overrides.

Literal colors should not be added inside page components when an existing semantic role is suitable.

## Shared controls

Current wrapper layer:

- `GbButton`
- `GbIconButton`
- `GbSwitch`
- `GbSlider`
- `GbActionChip`
- `GbStatusChip`
- `GbEmptyState`
- `GbIcon`

Custom controls such as Sound Pads and the hold-to-Stop-All action remain GrassiBoard-specific because their realtime interaction semantics are not a good fit for a generic Material component.

## Responsive behavior

- compact portrait: bottom navigation, two-column pad grid, reachable quick actions;
- larger portrait: expanded grids and mixer composition;
- landscape phone: navigation rail, condensed header, persistent live actions, wider control-deck composition;
- tablet/wide: navigation rail and wider pad/mixer grids.

The UI respects `env(safe-area-inset-*)` and includes `prefers-reduced-motion` behavior.

## Realtime rules

Visual interaction state may be transient (pressed feedback, ripple, local slider drag), but functional state continues to come from the authoritative GrassiBoard snapshot. UI changes must not queue commands while disconnected or invent persistent local playing/mute/engine state.

## Offline / disconnected recovery

The installed/cached GrassiMote PWA does not expose stale live controls while the WebSocket is unavailable. A centered recovery surface asks the user to start GrassiBoard on Windows and offers QR/code pairing. Scanning a QR whose host differs from the PWA's current IP-origin navigates to the newly scanned onboarding URL so DHCP/IP changes can be recovered.

`public/offline.html` is also precached as a no-JavaScript fallback. The service worker (`grassimote-shell-v3`) precaches the generated `_nuxt` entry assets discovered from `index.html` when possible, allowing the interactive recovery shell to survive a later PC/server outage. A completely fresh browser visit to an offline PC cannot receive any page because no server/service worker is available for that origin; offline recovery applies to an already installed or previously cached PWA.


## Icon delivery and browser-selection policy

- Application icons use **Material Symbols Rounded variable font** through the pinned `@fontsource-variable/material-symbols-rounded` package. Nuxt/Vite bundles the font into local generated assets; GrassiMote never depends on Google Fonts or another runtime CDN.
- `GbIcon` remains the app abstraction and maps GrassiBoard semantic icon names to Material Symbol ligatures. Active/selected state can vary the `FILL` and `wght` axes without page-level dependency on Material names.
- The UI is an appliance/control surface, so browser text selection and Android long-press text callouts are disabled globally with `user-select: none` / `-webkit-touch-callout: none`. Pairing inputs remain editable even though text-selection handles are intentionally suppressed.

## Offline launch behavior

- Once the service worker has been installed during a successful secure session, failed top-level navigations prefer `/offline.html` instead of a previously cached live-control route. This gives an intentionally static recovery screen when the Windows Remote server is disabled or unreachable.
- The already-running Vue app still transitions to `DisconnectedGate` on WebSocket loss, retaining Scan QR, manual pairing-code, and retry actions.
## v1.1 Material integration hardening

- `nuxt.config.ts` scans `~/components` with `pathPrefix: false` because the app-level wrappers live under `components/ui` but are intentionally named/used as `GbSlider`, `GbSwitch`, `GbButton`, etc. Without this, Nuxt 4 prefixes nested component names (for example `UiGbSlider`) and the intended wrapper tags are left unresolved.
- `app/plugins/material.client.ts` is an explicit Nuxt client plugin (`defineNuxtPlugin`) so Material custom elements are registered during client app creation before the live control wrappers are used.
- Primary live controls keep GrassiBoard wrappers (`GbSlider`, `GbSwitch`, `GbFab`, etc.) rather than exposing `md-*` tags to page code.
- Engine start/stop and microphone mute/unmute are persistent FABs above the phone bottom navigation so they remain one-tap reachable on every live-control page. `Stop All` remains a separate hold-to-activate destructive action and is never conflated with normal engine stop.
- Command-level errors use `GbSnackbar`, a small Material 3-style app component. Material Web does not currently provide a stable Snackbar, so the Remote does not add a second legacy UI package solely for transient messages. Network/disconnection state remains persistent and is handled by the recovery/connection surfaces.

