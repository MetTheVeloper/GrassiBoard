# Architecture

## Milestone 1 boundary

The repository has three layers:

1. `GrassiBoard.App`: a WPF `net8.0-windows` x64 UI process.
2. `GrassiBoard.AudioEngine`: a native C++20 x64 DLL exposing C ABI version 2.
3. `GrassiBoard.Driver`: a non-installable placeholder until Milestone 4.

The app calls the native layer through source-generated P/Invoke. C++/CLI is not used.

## Audio ownership

A dedicated native STA worker owns every MMDevice and WASAPI interface from creation through release. Capture and render use shared-mode event callbacks. A single worker waits on capture, render, and stop events, which avoids COM-interface handoff and keeps the audio path free of mutexes.

Microphone samples enter a preallocated mono float ring buffer. The render side removes them and duplicates the mono sample into stereo headset output. Device-format conversion and resampling to the fixed internal 48 kHz float formats are requested from the Windows shared audio engine.

The real-time loop performs no heap allocation, logging, file I/O, exception propagation, or blocking lock acquisition. Statistics cross to WPF through atomics; device enumeration and JSON serialization occur only while the engine is stopped.

## Version contract

- Product version: `0.2.0`
- Native ABI version: `2`
- Architecture: `x64`
- Processing format: `48,000 Hz`, 32-bit float, mono capture and stereo monitoring
- DSP processing: none in Milestone 1
