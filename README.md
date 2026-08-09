# GrassiBoard

GrassiBoard is a Windows x64 live voice-processing and Soundboard application. It captures a physical microphone, applies live Pitch/Formant processing, mixes Sound Pads, and sends the result to an independently installed external virtual audio cable.

## Current milestone

`v0.8.1` adds the production application shell and the first real Soundboard while preserving the manually accepted `v0.7.0` microphone route. It includes the Windows 10 startup binding hotfix discovered during initial v0.8.0 manual testing.

- **Board** is the daily workspace: reusable Sound Pads plus compact Voice FX.
- **Voice** contains full Pitch, Fine Pitch, Formant, preservation, quality, and latency controls.
- **Routing** selects the physical microphone and external virtual-cable playback endpoint.
- **Settings** contains copy-safe diagnostics and build information.
- The persistent top bar exposes Mic/Soundboard/Master meters, Mic Mute, and Soundboard-only Stop All.

Sound Pads support WAV and MP3, volume, Loop, per-pad stop, simultaneous playback, drag/drop, edit/delete, and JSON persistence. Files are referenced in their original locations. They are decoded and resampled to stereo 48 kHz float away from the real-time audio callback, then cached in the native engine.

## Audio route

```text
Physical microphone -> Voice DSP ----┐
                                     ├-> Master mix -> external cable playback endpoint
Cached Sound Pads -------------------┘                    -> target app microphone
```

Voice Pitch/Formant affects only the microphone branch. Mic Mute leaves Sound Pads active; Stop All stops Sound Pads without stopping the microphone or engine.

## External cable

GrassiBoard does not bundle or install a virtual driver. Any Windows virtual cable exposing a paired playback and recording endpoint can work. VB-CABLE is the documented reference. Download and license it directly from its publisher.

See [external virtual cable setup](docs/external-virtual-cable.md). Do not select the cable's recording endpoint as GrassiBoard's physical input, because that creates a routing loop.

## Build

Prerequisites are Visual Studio 2022 with Desktop development with C++, CMake, Git submodules, and .NET SDK `8.0.423`.

```powershell
git submodule update --init --recursive
cmake --preset windows-x64-release
cmake --build --preset windows-x64-release
ctest --preset windows-x64-release
dotnet run --project tests/GrassiBoard.App.SmokeTests/GrassiBoard.App.SmokeTests.csproj -c Release
dotnet build src/GrassiBoard.App/GrassiBoard.App.csproj -c Release -p:Platform=x64
```

GitHub Actions publishes a self-contained portable Windows package, symbols, and test results. A user does not need local build tools to test the milestone artifact.

## Documentation

- [Current status](docs/current-status.md)
- [Architecture](docs/architecture.md)
- [Audio pipeline](docs/audio-pipeline.md)
- [UI architecture](docs/ui-architecture.md)
- [Soundboard behavior](docs/soundboard.md)
- [Manual test plan](docs/test-plan.md)

## License

See [LICENSE](LICENSE), [LICENSES.md](LICENSES.md), and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt). External cable software has its own publisher and license.
