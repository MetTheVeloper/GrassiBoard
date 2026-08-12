# Third-party licenses

## Signalsmith Stretch

- Source: `third_party/signalsmith-stretch`
- Upstream: https://github.com/Signalsmith-Audio/signalsmith-stretch
- Pinned commit: `57b93f4e9206a089a45387eaa39bdc9f310d3308`
- Reported library version: `1.3.2`
- License: MIT

The required MIT notice is included in `THIRD-PARTY-NOTICES.txt` and in the portable package.

## Signalsmith Linear

- Source: `third_party/signalsmith-linear`
- Upstream: https://github.com/Signalsmith-Audio/linear
- Pinned release/commit: `0.3.1` / `5668673560146a9cfe38c25315071e3fd68c8317`
- License: MIT

Signalsmith Linear is the pinned FFT/STFT dependency used by Signalsmith Stretch. Its complete MIT notice is included in `THIRD-PARTY-NOTICES.txt`.

The project source is licensed under the Apache License 2.0; see `LICENSE`.

## NAudio

- Source: https://github.com/naudio/NAudio
- Package: `NAudio` `2.3.0` (pinned NuGet reference)
- License: MIT
- Usage: background WAV/MP3 decode and 48 kHz resampling; not used in the native real-time callback

The complete MIT notice is included in `THIRD-PARTY-NOTICES.txt` and in the portable package.

## External virtual cable

VB-CABLE and other virtual-cable products are independent optional prerequisites. They are not linked, embedded, or redistributed by GrassiBoard. The installer may display the publisher's official download link when no compatible endpoint is detected; the user downloads and licenses that software directly from its publisher.

## Microsoft Windows Driver Samples (SysVAD extraction)

- Source: `src/GrassiBoard.Driver/Sysvad`
- Upstream: https://github.com/microsoft/Windows-driver-samples
- Pinned commit: `ef7c3074748ab05726c3a9161d3256118efd76e2`
- Upstream path: `audio/sysvad`
- License: Microsoft Public License

The complete license is stored at `src/GrassiBoard.Driver/THIRD-PARTY-MS-PL.txt` and included in the driver package.

## QRCoder

- Upstream: https://github.com/codebude/QRCoder
- NuGet package: `QRCoder` `1.8.0` (pinned)
- License: MIT
- Usage: creates the one-time Remote pairing QR image in the WPF Settings UI; never used in the native audio callback

The MIT notice is included in `THIRD-PARTY-NOTICES.txt`.

## Nuxt

- Upstream: https://github.com/nuxt/nuxt
- npm package: `nuxt` `4.5.2` (pinned direct dependency)
- License: MIT
- Usage: build-time/application framework for the static GrassiBoard Remote SPA; no Node/Nitro runtime is installed on the user's PC

The MIT notice is included in `THIRD-PARTY-NOTICES.txt`.

## Vue

- Upstream: https://github.com/vuejs/core
- npm package: `vue` `3.5.41` (pinned direct dependency)
- License: MIT
- Usage: client runtime for the generated GrassiBoard Remote SPA

The MIT notice is included in `THIRD-PARTY-NOTICES.txt`.

> Material UI candidate note: after adding `@material/web`, regenerate and commit `src/GrassiBoard.RemoteWeb/pnpm-lock.yaml` before running the release CI with `--frozen-lockfile`.

## Material Web

- Upstream: https://github.com/material-components/material-web
- npm package: `@material/web` `2.5.0` (pinned direct dependency)
- License: Apache-2.0
- Usage: stable Material 3 Web Components used by the generated GrassiMote static SPA; bundled at build time with no runtime CDN requirement

Material Web is isolated behind GrassiBoard `Gb*` Vue wrappers where application-level consistency benefits from the abstraction. No experimental `labs` component is required by the v1.1 UI redesign candidate. The Apache-2.0 attribution is recorded in `THIRD-PARTY-NOTICES.txt`; the repository `LICENSE` contains the Apache License 2.0 text.

### SIPSorcery 10.0.13
Used for the v1.2 WebRTC transport. Upstream licensing is BSD 3-Clause plus an Additional Use Restriction. Current GrassiBoard use is private/personal; re-review the upstream license before any future public distribution.

### Concentus 2.2.2
Used for managed Opus encoding. See the upstream Concentus LICENSE and included third-party notices.
