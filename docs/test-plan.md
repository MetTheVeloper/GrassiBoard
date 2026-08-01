# Test plan

## Automated Milestone 0 checks

- Configure and build the native x64 Release DLL.
- Verify the native ABI version, engine version, and exported ping function with CTest.
- Parse and validate BuildInfo with the managed smoke-test executable.
- Build and self-contained-publish the WPF x64 application.
- Build and validate the explicit driver placeholder.
- Verify package contents and create portable, driver-placeholder, symbols, and test-result ZIP files.

## Manual acceptance

The user extracts the portable artifact, opens `GrassiBoard.exe`, checks version and commit values, confirms the native DLL status is green, then closes the app. No audio or driver test belongs to this milestone.
