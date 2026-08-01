# Pitch configuration benchmark

## Method

The Windows x64 Release test processes a deterministic three-second 220 Hz signal six times per mode at `+7` semitones with Formant preservation enabled. Configuration and allocation occur before timing. The report records total processing time, estimated percentage of one logical core relative to real time, algorithmic latency, measured output frequency, and frequency error.

The live implementation keeps all three configurations warm. The report therefore also sums their isolated processing percentages as a conservative estimate of the DSP worker's single-core demand. GitHub-hosted runner timing is useful for comparison but is not a target-PC CPU guarantee.

## Configurations

| Mode | Block / interval at 48 kHz | Split computation | Latency | CPU (one core) | Frequency error |
|---|---|---|---:|---:|---:|
| Low latency | 1,024 / 256 | Yes | 1,280 samples / 26.67 ms | 1.39% | 3.62% |
| Balanced | 2,048 / 512 | Yes | 2,560 samples / 53.33 ms | 1.42% | 1.77% |
| High quality | 5,760 / 1,440 | Yes | 7,200 samples / 150.00 ms | 1.53% | 0.03% |

These values came from GitHub Actions Windows x64 Release [run #14](https://github.com/MetTheVeloper/GrassiBoard/actions/runs/30704540252). Six passes processed 18 seconds of audio per configuration. Total measured processing time was `250.99 ms`, `255.05 ms`, and `274.59 ms` respectively. The summed live estimate for all three pre-warmed processors was `4.34%` of one logical core on that runner.

## Default-selection policy

Balanced is selected when the Release benchmark confirms all of the following:

- pitch-frequency error is at most 3%;
- measured processing cost is at most 25% of one logical core in isolation;
- algorithmic latency is lower than High quality.

Balanced passed every policy constraint. Low latency had half the Balanced delay but its `3.62%` frequency error exceeded the default threshold. High quality produced the best frequency result but added `96.67 ms` over Balanced. Balanced is therefore the default; Low latency and High quality remain explicit user choices.

## Switching and Formant evidence

The same run measured an RMS difference of `0.13` between preserved and unpreserved Formant output and `0.11` between preserved and `+6` Formant Shift output. During automated live quality/Formant changes, the maximum adjacent-sample step was `0.06` and the longest near-silent run was one sample. Comparison WAV files and the raw JSON reports are included in the test-results artifact.
