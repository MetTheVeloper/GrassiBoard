# Pitch prototype

## Backend selection

Milestone 2 uses [Signalsmith Stretch](https://github.com/Signalsmith-Audio/signalsmith-stretch), pinned as a Git submodule at commit `57b93f4e9206a089a45387eaa39bdc9f310d3308` (upstream version 1.3.2). It is licensed under the MIT License; attribution and the complete license text are included in `THIRD-PARTY-NOTICES.txt` and the portable package.

The backend was selected because it supports streaming pitch transposition without changing duration, exposes algorithmic input/output latency, accepts variable block sizes, and can be configured ahead of the real-time loop.

## Milestone configuration

- Internal format: 48 kHz mono float
- Pitch: `-12` to `+12` semitones
- Fine Pitch: `-100` to `+100` cents
- Parameter smoothing: 25 ms
- Bypass transition: 10 ms latency-aligned crossfade
- Default: Bypass enabled
- Backend mode: fixed balanced configuration for this milestone

The UI reports the backend's input plus output latency. Endpoint and ring-buffer delay are separate and are not included in that number.

Selectable low-latency/high-quality configurations, backend comparison, and formant processing are deliberately deferred to Milestone 3.

## Offline evidence

`GrassiBoard.Pitch.Tests` generates a three-second 220 Hz signal, processes it at seven semitone targets, and writes a WAV file for each result. It checks approximate frequency against `220 * 2^(semitones / 12)`, sample-count preservation, non-finite or excessive output, exact latency-aligned Bypass, and rapid live automation. CI publishes the WAV files and `pitch-test-report.json` in the test-results package for comparison.
