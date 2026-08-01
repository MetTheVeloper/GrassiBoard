# Architecture

## Milestone 2 boundary

The repository has three layers:

1. `GrassiBoard.App`: a WPF `net8.0-windows` x64 UI process.
2. `GrassiBoard.AudioEngine`: a native C++20 x64 DLL exposing C ABI version 3.
3. `GrassiBoard.Driver`: a non-installable placeholder until Milestone 4.

The app calls the native layer through source-generated P/Invoke. C++/CLI is not used.

## Audio ownership

A dedicated native STA worker owns every MMDevice and WASAPI interface from creation through release. Capture and render use shared-mode event callbacks. A single worker waits on capture, render, and stop events, which avoids COM-interface handoff and keeps the audio path free of mutexes.

Microphone samples are converted to the fixed 48 kHz mono processing format, passed through a preallocated `IPitchProcessor`, and written to a preallocated mono float ring buffer. The render side removes them and duplicates the mono sample into stereo headset output. Windows Audio performs endpoint-format conversion and resampling where needed.

The real-time loop performs no logging, file I/O, exception propagation, or blocking lock acquisition. Audio buffers and pitch-backend working memory are prepared before streaming starts. Statistics and live pitch targets cross threads through atomics; device enumeration and JSON serialization occur only while the engine is stopped.

## Pitch backend

Milestone 2 uses Signalsmith Stretch 1.3.2 at pinned commit `57b93f4e9206a089a45387eaa39bdc9f310d3308`. The native adapter implements `IPitchProcessor`, smooths live pitch changes over 25 ms, keeps dry and wet paths latency-aligned, and crossfades Bypass over 10 ms. Formant controls and selectable quality modes remain outside this milestone.

## Version contract

- Product version: `0.3.0`
- Native ABI version: `3`
- Architecture: `x64`
- Processing format: `48,000 Hz`, 32-bit float, mono processing and stereo monitoring
- Pitch range: `-12` to `+12` semitones plus `-100` to `+100` cents fine adjustment
- Default state: Bypass enabled
