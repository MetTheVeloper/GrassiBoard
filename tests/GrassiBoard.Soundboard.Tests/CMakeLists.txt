add_executable(GrassiBoard.Soundboard.Tests
  soundboard_mixer_tests.cpp
  ../../src/GrassiBoard.AudioEngine/src/soundboard_mixer.cpp
  ../../src/GrassiBoard.AudioEngine/src/soundboard_mixer.h
)

target_compile_features(GrassiBoard.Soundboard.Tests PRIVATE cxx_std_20)
target_include_directories(GrassiBoard.Soundboard.Tests PRIVATE
  ../../src/GrassiBoard.AudioEngine/include
  ../../src/GrassiBoard.AudioEngine/src
)

if(MSVC)
  target_compile_options(GrassiBoard.Soundboard.Tests PRIVATE /W4 /WX /permissive- /EHsc)
else()
  target_compile_options(GrassiBoard.Soundboard.Tests PRIVATE -Wall -Wextra -Wpedantic -Werror)
endif()

add_test(NAME soundboard-mixer-contract COMMAND GrassiBoard.Soundboard.Tests)
