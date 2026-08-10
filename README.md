# GrassiBoard

GrassiBoard is a Windows x64 live voice-processing and Soundboard application. It captures a physical microphone, applies live Pitch/Formant processing, mixes Sound Pads, and sends the result to an independently installed external virtual audio cable.

## Current milestone

`v0.11.2` repairs the v0.11.1 startup XAML failure, adds automated StaticResource validation, and applies the official GrassiBoard application icon. It retains the v0.11.1 Local Media synchronization fixes and the manually accepted v0.9.0 Voice, Mixer, Soundboard, UI, and external-cable baseline.

- **Board** is the daily workspace: streaming Local Media Deck, reusable Sound Pads, and compact Voice FX.
- **Voice** contains full Pitch, Fine Pitch, Formant, preservation, quality, and latency controls.
- **Mixer** contains Mic/Soundboard/Master gain, dynamics, protection, Pitch Wet/Dry, built-in presets, and persistent user presets.
- **Routing** selects the physical microphone and external virtual-cable playback endpoint.
- **Settings** contains Profiles, tray/startup behavior, global hotkeys, latency diagnostics, and build information.
- The persistent top bar exposes Mic/Soundboard/Master meters, Mic Mute, and a global Stop All that stops Pads and the audio engine without resetting configuration.

Sound Pads support WAV and MP3, volume, Loop, per-pad stop, simultaneous playback, drag/drop, edit/delete, and JSON persistence. Files are referenced in their original locations. They are decoded and resampled to stereo 48 kHz float away from the real-time audio callback, then cached in the native engine.

The Local Media Deck streams long audio (and supported local video audio tracks) with bounded read-ahead instead of loading the whole file. Headphone monitoring stays direct, while virtual-microphone Media send is delayed by the active Pitch algorithm latency so singing over a monitored beat reaches the target application in sync. Microphone audio is never sent to the monitor route.

## Audio route

```text
Physical microphone -> Voice DSP -> Mic dynamics ---+
                                                      +-> Master gain/protection -> cable -> target app
Cached Sound Pads -> Board gain -> Ducking ----------+
Streaming Local Media --------------------------------+
```

Voice Pitch/Formant affects only the microphone branch. Mic Mute leaves Sound Pads and Media active. Global Stop All stops Pads, Media, and the engine while retaining configuration so the engine can be started again.

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
- [Profiles and presets](docs/profiles-and-presets.md)
- [Global hotkeys and tray](docs/hotkeys-and-tray.md)
- [Local Media Deck](docs/media-deck.md)
- [Latency benchmark policy](docs/latency-benchmark.md)
- [Manual test plan](docs/test-plan.md)

## License

See [LICENSE](LICENSE), [LICENSES.md](LICENSES.md), and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt). External cable software has its own publisher and license.
