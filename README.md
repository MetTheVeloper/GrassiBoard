# GrassiBoard

GrassiBoard is a Windows x64 voice-processing and soundboard application. Development is milestone-based; each milestone is packaged and manually accepted before work begins on the next one.

## Current release

`v0.2.0` implements **Milestone 1 — Physical Microphone Passthrough**:

- WPF device selectors and non-blocking Start/Stop controls
- MMDevice enumeration for active capture and playback endpoints
- Native C++20 shared/event-driven WASAPI engine
- 48 kHz float microphone capture and stereo headset monitoring
- Preallocated ring buffer with underrun, overrun, and discontinuity counters
- Live input/output meters and buffer diagnostics
- Native C ABI version 2 consumed through P/Invoke

Pitch, formant processing, soundboard playback, and the virtual audio driver are not implemented yet.

## Build

Prerequisites for local native builds are Visual Studio 2022 with the Desktop development with C++ workload and .NET SDK `8.0.423`.

```powershell
cmake --preset windows-x64-release
cmake --build --preset windows-x64-release
ctest --preset windows-x64-release
dotnet run --project tests/GrassiBoard.App.SmokeTests/GrassiBoard.App.SmokeTests.csproj -c Release
dotnet build src/GrassiBoard.App/GrassiBoard.App.csproj -c Release -p:Platform=x64
```

GitHub Actions builds the downloadable, self-contained Windows package on every push to `main`.

## Safety

Start live monitoring with a low headset volume. Selecting speakers can create a feedback loop. Milestone 1 still contains no installable driver; driver development is deferred until Milestone 4.

## License

See [LICENSE](LICENSE) and [LICENSES.md](LICENSES.md).
