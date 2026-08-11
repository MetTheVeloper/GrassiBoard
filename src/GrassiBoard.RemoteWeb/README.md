# GrassiBoard Remote Web

Nuxt 4 + Vue 3 + TypeScript SPA used by GrassiBoard v1.1 Remote Control.

## Development

```bash
pnpm install
pnpm dev
```

When the Nuxt dev server is not served by GrassiBoard itself, point it at a running Windows Remote server:

```powershell
$env:NUXT_PUBLIC_REMOTE_ORIGIN="http://192.168.1.20:47918"
pnpm dev
```

The Windows app only allows cross-origin development requests from loopback origins. Production is same-origin.

## Static production build

```bash
pnpm generate
```

The deployable SPA is written to `.output/public`. The WPF project copies that directory to `RemoteWeb/` during publish, and its embedded Kestrel host serves it on the selected private LAN address.

No Node.js, pnpm, Nuxt server, Nitro API, or SSR runtime is required on the user's PC.

## v1.1.0 candidate lockfile note

The first source candidate intentionally does not yet include `pnpm-lock.yaml` because the authoring environment could not reach the package registry. Direct dependency versions are pinned, and CI is configured to resolve the candidate with `pnpm install --no-frozen-lockfile`. A resolved lockfile must be committed before v1.1 is marked user-accepted.
