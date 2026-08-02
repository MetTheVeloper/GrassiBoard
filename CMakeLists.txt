cmake_minimum_required(VERSION 3.25)

project(GrassiBoard VERSION 0.6.4 LANGUAGES CXX)

include(CTest)
add_subdirectory(third_party/signalsmith-linear EXCLUDE_FROM_ALL SYSTEM)
add_subdirectory(third_party/signalsmith-stretch EXCLUDE_FROM_ALL SYSTEM)
add_subdirectory(src/GrassiBoard.AudioEngine)

if(BUILD_TESTING)
  add_subdirectory(tests/GrassiBoard.AudioEngine.Tests)
  add_subdirectory(tests/GrassiBoard.Pitch.Tests)
  add_subdirectory(tests/GrassiBoard.DriverTransport.Tests)
endif()
