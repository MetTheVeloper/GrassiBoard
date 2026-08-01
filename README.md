# GrassiBoard

GrassiBoard is a Windows x64 voice-processing and soundboard application. Development is milestone-based; each milestone is packaged and manually accepted before work begins on the next one.

## Current release

`v0.3.0` implements **Milestone 2 — Pitch Shift Prototype**:

- Physical USB microphone capture and headset monitoring from v0.2.0
- `IPitchProcessor` abstraction with a pinned Signalsmith Stretch backend
- Live pitch from −12 to +12 semitones and fine pitch from −100 to +100 cents
- Smoothed pitch automation without changing stream speed
- Latency-aligned, crossfaded bypass
- Algorithmic latency reporting in samples and milliseconds
- Offline WAV sample outputs and frequency/length/finite/peak/automation tests

Formant processing, backend comparison, soundboard playback, and the virtual audio driver are not implemented yet.

## Build

Prerequisites are Visual Studio 2022 with the Desktop development with C++ workload, Git submodules, and .NET SDK `8.0.423`.

```powershell
git submodule update --init --recursive
cmake --preset windows-x64-release
cmake --build --preset windows-x64-release
ctest --preset windows-x64-release
dotnet run --project tests/GrassiBoard.App.SmokeTests/GrassiBoard.App.SmokeTests.csproj -c Release
dotnet build src/GrassiBoard.App/GrassiBoard.App.csproj -c Release -p:Platform=x64
```

GitHub Actions builds the self-contained Windows package and pitch test samples on every push to `main`.

## Safety

Start live monitoring with a low headset volume. Selecting speakers can create a feedback loop. Milestone 2 contains no installable driver.

## License

See [LICENSE](LICENSE), [LICENSES.md](LICENSES.md), and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
