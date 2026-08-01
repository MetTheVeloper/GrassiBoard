# GrassiBoard

GrassiBoard is a Windows x64 voice-processing and soundboard application. Development is milestone-based; each milestone is packaged and manually accepted before work begins on the next one.

## Current release

`v0.6.1` is **Milestone 5 — Virtual Cable PCM Transport** with the Windows Audio shared-capture compatibility fix, while retaining the accepted app/DSP and driver lifecycle:

- Live pitch and Fine Pitch from Milestone 2
- Formant preservation and independent formant shift from −12 to +12 semitones
- Low latency, Balanced, and High quality Signalsmith configurations
- Live 20 ms crossfades between already-prepared configurations without restarting WASAPI
- Smoothed Formant and preservation changes
- Per-mode CPU, latency, and pitch-frequency benchmarks
- Offline voice-like WAV outputs for preservation, formant shift, and live mode switching
- Balanced default selected by an explicit benchmark policy
- A separate test-signed x64 driver package with `GrassiBoard Virtual Cable Input` and `GrassiBoard Virtual Microphone`
- Controlled install, removal, TESTSIGNING, and diagnostic scripts
- Real PCM transport from the virtual render endpoint to the virtual microphone
- Fixed 48 kHz / 16-bit / stereo transport with pre-roll, silence, stale-data flushing, and underrun/overrun accounting
- A deterministic WAV generator and automated transport-policy regression tests

The cable can be tested without opening GrassiBoard. Sending the processed app output to the virtual cable begins in Milestone 6; Soundboard playback is not implemented yet.

## Build

Prerequisites are Visual Studio 2022 with Desktop development with C++, Git submodules, .NET SDK `8.0.423`, and NuGet CLI. The kernel build restores pinned WDK/SDK NuGet packages from `packages.config`.

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

Start live monitoring with a low headset volume. Selecting speakers can create a feedback loop. The v0.6.1 driver is test-signed and distributed separately; read `DRIVER-TESTING.md` inside that package before changing TESTSIGNING.

## Documentation

See [driver design](docs/driver-design.md), [pitch benchmark](docs/pitch-benchmark.md), and [test plan](docs/test-plan.md).

## License

See [LICENSE](LICENSE), [LICENSES.md](LICENSES.md), and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
