# Pitch configuration benchmark

## Method

The Windows x64 Release test processes a deterministic three-second 220 Hz signal six times per mode at `+7` semitones with Formant preservation enabled. Configuration and allocation occur before timing. The report records total processing time, estimated percentage of one logical core relative to real time, algorithmic latency, measured output frequency, and frequency error.

The live implementation keeps all three configurations warm. The report therefore also sums their isolated processing percentages as a conservative estimate of the DSP worker's single-core demand. GitHub-hosted runner timing is useful for comparison but is not a target-PC CPU guarantee.

## Configurations

| Mode | Block / interval at 48 kHz | Split computation | Latency | CPU | Frequency error |
|---|---|---|---:|---:|---:|
| Low latency | 1,024 / 256 | Yes | Pending CI | Pending CI | Pending CI |
| Balanced | 2,048 / 512 | Yes | Pending CI | Pending CI | Pending CI |
| High quality | 5,760 / 1,440 | Yes | Pending CI | Pending CI | Pending CI |

## Default-selection policy

Balanced is selected when the Release benchmark confirms all of the following:

- pitch-frequency error is at most 3%;
- measured processing cost is at most 25% of one logical core in isolation;
- algorithmic latency is lower than High quality.

The first Milestone 3 CI run must replace the pending cells above with its generated results before `v0.4.0` is tagged.
