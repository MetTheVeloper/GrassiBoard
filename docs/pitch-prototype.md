# Pitch and formant processing

## Backend

GrassiBoard uses [Signalsmith Stretch](https://github.com/Signalsmith-Audio/signalsmith-stretch), pinned at commit `57b93f4e9206a089a45387eaa39bdc9f310d3308` (version 1.3.2). Its [Signalsmith Linear](https://github.com/Signalsmith-Audio/linear) dependency is pinned at release `0.3.1`, commit `5668673560146a9cfe38c25315071e3fd68c8317`. Both are MIT-licensed and their notices ship in `THIRD-PARTY-NOTICES.txt`.

## Configurations

| UI mode | Analysis block at 48 kHz | Interval | Split computation |
|---|---:|---:|---|
| Low latency | 1,024 samples | 256 samples | Yes |
| Balanced | 2,048 samples | 512 samples | Yes |
| High quality | Signalsmith default: 5,760 samples | 1,440 samples | Yes |

All three processors are allocated and kept aligned to the live input. A requested mode is selected with a 20 ms output crossfade and no WASAPI restart. Each mode retains its own reported algorithmic latency and latency-aligned Bypass path.

## Controls

- Pitch: `-12` to `+12` semitones
- Fine Pitch: `-100` to `+100` cents
- Formant Shift: `-12` to `+12` semitones
- Preserve formants: enabled by default
- Parameter smoothing: 25 ms
- Bypass transition: 10 ms
- Quality transition: 20 ms
- Default mode: Balanced

Preservation is applied as a smooth compensation term derived from the current pitch map, while Formant Shift remains independent. This lets preservation toggle without an abrupt boolean change inside the backend.

## Evidence

`GrassiBoard.Pitch.Tests` retains the seven pitch targets from Milestone 2, generates a deterministic voice-like harmonic source, verifies measurable preservation and Formant Shift differences, stress-tests live mode changes, and creates comparison WAVs. CPU, latency, and frequency results are recorded in `pitch-benchmark.json` and summarized in [pitch-benchmark.md](pitch-benchmark.md).
