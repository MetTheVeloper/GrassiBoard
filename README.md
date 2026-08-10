# GrassiBoard

GrassiBoard is a Windows x64 live voice-processing and Soundboard application. It captures a physical microphone, applies live Pitch/Formant processing, mixes Sound Pads, and sends the result to an independently installed external virtual audio cable.

## Current milestone

`v0.9.0` adds Mixer & Dynamic Processing on top of the accepted v0.8.3 UI and audio baseline. It preserves the external-cable route and Soundboard behavior while adding live bus gain, voice dynamics, ducking, output protection, Pitch Wet/Dry, and audio presets.

- **Board** is the daily workspace: reusable Sound Pads plus compact Voice FX.
- **Voice** contains full Pitch, Fine Pitch, Formant, preservation, quality, and latency controls.
- **Mixer** contains Mic/Soundboard/Master gain, Noise Gate, Compressor, Limiter, Ducking, Clipping Protection, Pitch Wet/Dry, and built-in presets.
- **Routing** selects the physical microphone and external virtual-cable playback endpoint.
- **Settings** contains copy-safe diagnostics and build information.
- The persistent top bar exposes Mic/Soundboard/Master meters, Mic Mute, and a global Stop All that stops Pads and the audio engine without resetting configuration.

Sound Pads support WAV and MP3, volume, Loop, per-pad stop, simultaneous playback, drag/drop, edit/delete, and JSON persistence. Files are referenced in their original locations. They are decoded and resampled to stereo 48 kHz float away from the real-time audio callback, then cached in the native engine.

## Audio route

```text
Physical microphone -> Voice DSP -> Mic dynamics ---+
                                                      +-> Master gain/protection -> cable -> target app
Cached Sound Pads -> Board gain -> Ducking ----------+
```

Voice Pitch/Formant affects only the microphone branch. Mic Mute leaves Sound Pads active. Global Stop All stops Pads and the engine while retaining routing, Voice, Mixer, presets, and Board configuration so the engine can be started again.

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
