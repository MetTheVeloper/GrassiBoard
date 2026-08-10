# Latency benchmark and stability policy

## Accepted baseline

The committed Pitch benchmark measured the pre-v0.11 processor on a GitHub Windows runner:

| Mode | Pitch latency | Single-core CPU | Frequency error | Policy |
|---|---:|---:|---:|---|
| Low latency | 1280 samples / 26.67 ms | 1.39% | 3.62% | Available; misses the 3% accuracy gate |
| Balanced | 2560 samples / 53.33 ms | 1.42% | 1.77% | Stable default |
| High quality | 7200 samples / 150.00 ms | 1.53% | 0.03% | Accuracy-first option |

The largest controlled contributor is the Pitch algorithm. Capture/render periods are device-selected shared-mode WASAPI values and the processed ring fill varies at runtime. Mixer work adds no algorithmic look-ahead. Media virtual-send read-ahead is intentionally about 200 ms for stable long-form playback and is independent of microphone Pitch latency.

## v0.11 decision

v0.11 preserves the accepted shared/event-driven STA WASAPI worker, 48 kHz float core, three prewarmed Pitch processors, live crossfades, and MMCSS `Pro Audio` classification. No exclusive-mode default or smaller USB buffer is forced because the available measurements do not justify the compatibility/stability risk. Balanced remains the default; Low remains an explicit user choice.

The real-time audit keeps file I/O, decode/resampling, WPF, logging, heap growth, and blocking locks outside capture/render. Media adds only atomic ring state, two sample pops, fixed Mixer arithmetic, and atomic statistics per render frame/block. Diagnostics show capture/render frames, Pitch samples/ms, microphone ring fill, Media fill/capacity/underruns, U/O/D counters, and an estimated processing total.

## Final measurement rule

CI reruns all Pitch benchmark gates and native real-time contracts for every v0.11 commit. Final end-to-end/perceived latency, USB-headset stability, Media monitor responsiveness, CPU behavior, and long-run counter growth require the supplied Windows 10 manual matrix. An apparently lower number is rejected if it introduces crackling, Pitch artifacts, underruns, or instability.
