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
