# GrassiBoard

GrassiBoard is a Windows x64 live voice-processing and soundboard application.

## Current milestone

`v0.7.0` routes the processed physical microphone into an **external virtual audio cable**. The target application sees the cable's recording endpoint as its microphone, so the same GrassiBoard output can be used by Voice Recorder, OBS, Discord, browsers, and other standard Windows audio clients.

The app:

- keeps the accepted live Pitch, Fine Pitch, Formant, Bypass, and quality modes;
- enumerates active Windows render and capture endpoints;
- identifies paired virtual-cable endpoints by Windows Container ID with a name-based fallback;
- prefers an installed external cable automatically;
- shows the exact recording endpoint to select in the target application;
- ignores the retired test-signed GrassiBoard driver;
- does not bundle, install, or redistribute a third-party driver.

The previous custom SysVAD driver remains in the repository as experimental research, but it is no longer built or shipped by the product workflow. It installed correctly but did not satisfy the Windows 10 WASAPI capture contract.

## External cable

Any Windows virtual cable exposing a paired playback and recording endpoint can work. VB-CABLE is the documented reference because it supports Windows 10/11 and standard Windows audio APIs. Download and license it directly from its publisher; it is not part of GrassiBoard.

See [external virtual cable setup](docs/external-virtual-cable.md). The portable package includes the same guide as `EXTERNAL-CABLE-SETUP.md`.

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

## Safety

Do not select the cable's recording endpoint as GrassiBoard's physical input; that would create a routing loop. Local headphone monitoring is intentionally not part of this first external-cable build.

## License

See [LICENSE](LICENSE), [LICENSES.md](LICENSES.md), and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt). External cable software has its own publisher and license.
