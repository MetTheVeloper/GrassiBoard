# Test plan

## Automated Milestone 1 checks

- Configure and build the native x64 Release DLL with warnings treated as errors.
- Verify native ABI version 2, engine version, engine lifecycle, idle statistics, and exported ping function with CTest.
- Parse and validate v0.2.0 BuildInfo with the managed smoke-test executable.
- Build and self-contained-publish the WPF x64 application.
- Build and validate the explicitly non-installable driver placeholder.
- Verify package contents and create portable, driver-placeholder, symbols, and test-result ZIP files.

GitHub-hosted runners cannot validate a physical USB headset, audible monitoring, or real endpoint timing. Those checks are manual.

## Manual acceptance

1. Select the USB headset microphone and matching headset output.
2. Lower headset volume, start the engine, and speak for at least 30 seconds.
3. Confirm that input/output meters move and the microphone is audible.
4. Stop and start the engine three times.
5. Confirm the UI remains responsive and record buffer/ring/dropout diagnostics.
6. Stop the engine and close the application normally.
