# GrassiBoard

GrassiBoard is a Windows x64 voice-processing and soundboard application. Development is milestone-based; each milestone is packaged and manually accepted before work begins on the next one.

## Current release

`v0.4.0` implements **Milestone 3 — Formant and quality comparison**:

- Live pitch and Fine Pitch from Milestone 2
- Formant preservation and independent formant shift from −12 to +12 semitones
- Low latency, Balanced, and High quality Signalsmith configurations
- Live 20 ms crossfades between already-prepared configurations without restarting WASAPI
- Smoothed Formant and preservation changes
- Per-mode CPU, latency, and pitch-frequency benchmarks
- Offline voice-like WAV outputs for preservation, formant shift, and live mode switching
- Balanced default selected by an explicit benchmark policy

Soundboard playback and the virtual audio driver are not implemented yet.

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

GitHub Actions builds the self-contained Windows package, comparison WAV files, and benchmark report on every push to `main`.

## Safety

Start live monitoring with a low headset volume. Selecting speakers can create a feedback loop. Milestone 3 contains no installable driver.

## Documentation

See [pitch prototype](docs/pitch-prototype.md), [pitch benchmark](docs/pitch-benchmark.md), and [test plan](docs/test-plan.md).

## License

See [LICENSE](LICENSE), [LICENSES.md](LICENSES.md), and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
