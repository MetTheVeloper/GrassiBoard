# GrassiBoard

GrassiBoard is a Windows x64 voice-processing and soundboard application. Development is deliberately milestone-based; each milestone is packaged and manually accepted before work begins on the next one.

## Current release

`v0.1.0` implements **Milestone 0** only:

- WPF application shell targeting `net8.0-windows`
- Native C++20 DLL with a versioned C ABI
- WPF-to-native P/Invoke smoke check
- Explicitly non-installable driver placeholder
- Reproducible x64 CI packaging and build metadata

Audio capture, DSP, soundboard playback, monitoring, and the virtual driver are not implemented yet.

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

Milestone 0 contains no installable driver. Driver development is deferred until Milestone 4 and will include separate installation, removal, diagnostics, and recovery guidance.

## License

See [LICENSE](LICENSE) and [LICENSES.md](LICENSES.md).
