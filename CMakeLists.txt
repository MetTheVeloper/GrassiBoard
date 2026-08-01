cmake_minimum_required(VERSION 3.25)

project(GrassiBoard VERSION 0.1.0 LANGUAGES CXX)

include(CTest)
add_subdirectory(src/GrassiBoard.AudioEngine)

if(BUILD_TESTING)
  add_subdirectory(tests/GrassiBoard.AudioEngine.Tests)
endif()
