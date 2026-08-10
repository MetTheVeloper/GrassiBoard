# Architecture

## v1 product boundary

The active product has three layers:

1. `GrassiBoard.App`: WPF `net8.0-windows` x64 shell, Profiles/persistence, hotkeys/Tray, background Pad decode, streaming Media decode/monitoring, and Voice/Mixer controls.
2. `GrassiBoard.AudioEngine`: native C++20 x64 DLL exposing C ABI version 8 and owning WASAPI, Voice DSP, Mixer/Dynamics, the predecoded Soundboard mixer, bounded streaming Media ring, alignment, and meters.
3. `GrassiBoard.Installer`: branded per-user WPF bootstrapper embedding the portable payload and registering safe manifest-based uninstall.
4. An independently installed external virtual cable providing the Windows render/capture endpoint pair.

`GrassiBoard.Driver` and `GrassiBoard.DeviceTool` remain historical experimental source. They are not built, installed, or published by the product workflow.

The app calls the native engine through source-generated P/Invoke. NAudio 2.3.0 is pinned for Pad decoding plus Media streaming/resampling and the independent headphone monitor. It does not own the microphone-to-cable live stream.

## Long-lived application state

`MainViewModel` owns one `NativeAudioEngine` service for the lifetime of `MainWindow`. Board, Voice, Mixer, Routing, and Settings are presentation views over that same state. Navigation never recreates the engine, devices, Voice/Mixer parameters, meters, or pad playback.

Versioned Profiles live in `%APPDATA%\GrassiBoard\profiles.json` and contain device choices, Voice/Mixer state, Pads, user presets, hotkeys, and app/Media preferences. The legacy `soundboard.json` is migrated into the first Profile. JSON loading isolates a malformed Profile, Pad, or Preset from valid siblings. Missing source files become recoverable states instead of startup failures.

See [UI architecture](ui-architecture.md) and [Soundboard behavior](soundboard.md).

## Audio ownership

A dedicated native STA worker owns every MMDevice and WASAPI interface from creation through release. Capture and render use shared-mode event callbacks. A single worker waits on capture, render, and stop events, avoiding COM-interface handoff and blocking locks in the audio path.

Microphone samples are converted to 48 kHz mono float, processed, and written to the existing preallocated mono ring. Render combines processed microphone, cached Soundboard, and streaming Media frames, then applies Master gain, linked limiting, clipping protection, and a final safety clamp before rendering to the external-cable endpoint. Media never enters Pitch/Formant.

## Media ownership

`MediaDeckService` opens local media on a background worker and streams at 48 kHz stereo through bounded read-ahead. The virtual-send branch writes blocks into a preallocated native SPSC ring; the render callback only pops frames. A separate NAudio WASAPI output may monitor Media in the selected headphone device. It receives Media only—never the microphone branch. Pause, seek, reconfiguration, and missing files are handled outside the callback.

## Soundboard ownership

WAV/MP3 decode, channel conversion, 48 kHz resampling, disk reads, and large allocations happen on a managed background thread. The completed stereo float buffer crosses the C ABI once and is copied into immutable native clip storage outside the audio callback.

Playback control uses a fixed 256-command single-producer/single-consumer queue. The render worker mixes from a fixed 32-voice array. The callback performs no decode, file I/O, UI operation, first-use allocation, or blocking lock acquisition.

## Live DSP ownership

`LivePitchProcessor` retains the accepted v0.7 behavior: three prewarmed Signalsmith processors, live mode crossfades, smoothed Pitch/Formant targets, and latency-aligned bypass. Soundboard samples enter after this processor, so Voice Pitch/Formant never alters a Sound Pad.

`MixerDynamicsProcessor` remains preallocated and callback-safe. UI parameters enter as a fixed-size structure, are published atomically, and are consumed at render-block boundaries. Preset interpolation runs asynchronously in the control layer and publishes small parameter changes; it never restarts the engine.

## Version contract

- Product version: `1.0.0`
- Native ABI version: `8`
- Architecture: `x64`
- Processing/mix format: `48,000 Hz`, 32-bit float; mono Voice DSP and stereo master output
- Cached Sound Pad limit: WAV/MP3, mono or stereo, at most ten minutes per file
- Simultaneous Sound Pad voices: 32
- Pitch range: `-12` to `+12` semitones plus `-100` to `+100` cents
- Formant shift range: `-12` to `+12` semitones
- Default Voice state: Balanced, preservation enabled, Voice FX disabled (positive UI maps to native bypass)
