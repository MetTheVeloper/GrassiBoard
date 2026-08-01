# Architecture

## Milestone 0 boundary

The repository establishes three independently buildable layers:

1. `GrassiBoard.App`: a WPF `net8.0-windows` x64 UI process.
2. `GrassiBoard.AudioEngine`: a native C++20 x64 DLL exposing a versioned C ABI.
3. `GrassiBoard.Driver`: a non-installable placeholder until Milestone 4.

The app calls the native layer through source-generated P/Invoke. C++/CLI is not used. The only current calls query the ABI and product versions; no audio device is opened.

## Planned process boundary

DSP, device I/O, mixing, monitoring, metering, and resampling remain in user mode. The future kernel component is limited to virtual render/capture transport. UI commands will cross the ABI through a non-blocking command queue once real-time processing begins.

## Version contract

- Product version: `0.1.0`
- Native ABI version: `1`
- Architecture: `x64`
- Processing implementation: not present in Milestone 0
