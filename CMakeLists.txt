cmake_minimum_required(VERSION 3.25)

project(GrassiBoard VERSION 0.11.2 LANGUAGES CXX)

include(CTest)

option(GRASSIBOARD_REMOTE_MONITOR_TAP "Enable Remote Monitor taps plus v1.3 ABI-10 Remote Phone Mic input" ON)
add_subdirectory(third_party/signalsmith-linear EXCLUDE_FROM_ALL SYSTEM)
add_subdirectory(third_party/signalsmith-stretch EXCLUDE_FROM_ALL SYSTEM)
add_subdirectory(src/GrassiBoard.AudioEngine)

if(BUILD_TESTING)
  add_subdirectory(tests/GrassiBoard.AudioEngine.Tests)
  add_subdirectory(tests/GrassiBoard.Pitch.Tests)
  add_subdirectory(tests/GrassiBoard.Soundboard.Tests)
  add_subdirectory(tests/GrassiBoard.Mixer.Tests)
  add_subdirectory(tests/GrassiBoard.Media.Tests)
  add_subdirectory(tests/GrassiBoard.DriverTransport.Tests)
endif()
