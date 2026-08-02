# Architecture

## Milestone 5 boundary

The repository has three layers:

1. `GrassiBoard.App`: a WPF `net8.0-windows` x64 UI process.
2. `GrassiBoard.AudioEngine`: a native C++20 x64 DLL exposing C ABI version 4.
3. `GrassiBoard.Driver`: a test-signed SysVAD/WaveRT virtual cable with one render and one capture endpoint.

`GrassiBoard.DeviceTool` is a statically linked SetupAPI helper used only by the elevated install/removal scripts to create and remove the unique root-enumerated device. The portable app and driver package remain separate artifacts.

The app calls the native layer through source-generated P/Invoke. C++/CLI is not used.

The kernel cable uses fixed `48,000 Hz`, 16-bit stereo PCM on render and fixed mono PCM on capture. A bounded PCM16 downmix feeds a preallocated lock-free mono ring from the render DPC to the capture DPC. Capture pre-rolls 10 ms, zero-fills shortages, and flushes all queued data when either cable stream pauses or stops. Both endpoints retain the reference event-driven WaveRT contract. No resampling, allocation, file I/O, or blocking lock occurs in the transport path.

## Audio ownership

A dedicated native STA worker owns every MMDevice and WASAPI interface from creation through release. Capture and render use shared-mode event callbacks. A single worker waits on capture, render, and stop events, avoiding COM-interface handoff and mutexes in the audio path.

Microphone samples are converted to 48 kHz mono float, processed, and written to a preallocated mono ring buffer. Render duplicates the processed mono signal to stereo. Windows Audio performs endpoint-format conversion and resampling where needed.

## Live DSP ownership

`LivePitchProcessor` owns three `SignalsmithPitchProcessor` instances configured as Low latency, Balanced, and High quality. All three are configured, allocated, and reset before WASAPI starts. They remain warm from the same input timeline, allowing a 20 ms crossfade to another mode without stopping or resetting the stream. This trades additional CPU for deterministic live switching; the aggregate cost is measured by CI.

Pitch, Fine Pitch, Formant Shift, preservation, Bypass, and requested quality mode cross threads through atomics. Pitch/Formant/preservation targets are smoothed over 25 ms. Each mode owns its latency-aligned dry delay, and Bypass crossfades over 10 ms.

The real-time loop performs no logging, file I/O, exception propagation, blocking lock acquisition, or first-use allocation.

## Version contract

- Product version: `0.6.4`
- Native ABI version: `4`
- Architecture: `x64`
- Processing format: `48,000 Hz`, 32-bit float, mono processing and stereo monitoring
- Pitch range: `-12` to `+12` semitones plus `-100` to `+100` cents
- Formant shift range: `-12` to `+12` semitones
- Default mode: Balanced, preservation enabled, Bypass enabled
