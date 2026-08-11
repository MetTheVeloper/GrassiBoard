# GrassiMote Remote Web

GrassiMote is the static Nuxt 4 / Vue 3 control surface served by GrassiBoard over the private LAN.

## Stack

- Nuxt 4
- Vue 3
- TypeScript
- `ssr: false`
- `pnpm generate`
- Material Design 3 interaction system
- stable `@material/web` controls behind GrassiBoard `Gb*` wrappers
- locally bundled SVG Material-symbol icon subset (no runtime icon CDN)

There is no Node.js runtime requirement on the deployed Windows PC. Node/pnpm exist only for development and CI; `.output/public` is packaged with the desktop application.

## Local UI iteration

When only Remote Web files change, use the repository helper from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Deploy-RemoteWebLocal.ps1 -RestoreWeb
```

`-RestoreWeb` is required after package dependency changes so `pnpm-lock.yaml` and `node_modules` are updated before `pnpm generate`.

## Design system

See `docs/remote-ui-system.md` for Material Web integration, GrassiBoard semantic tokens, shared controls, responsive behavior, and realtime UI boundaries.
