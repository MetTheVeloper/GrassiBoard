add_executable(GrassiBoard.Mixer.Tests
  mixer_processor_tests.cpp
  ../../src/GrassiBoard.AudioEngine/src/mixer_processor.cpp
  ../../src/GrassiBoard.AudioEngine/src/mixer_processor.h
  ../../src/GrassiBoard.AudioEngine/src/looper_engine.cpp
  ../../src/GrassiBoard.AudioEngine/src/looper_engine.h
  ../../src/GrassiBoard.AudioEngine/src/monitor_tap_buffer.cpp
  ../../src/GrassiBoard.AudioEngine/src/monitor_tap_buffer.h
)

target_compile_features(GrassiBoard.Mixer.Tests PRIVATE cxx_std_20)
target_include_directories(GrassiBoard.Mixer.Tests PRIVATE
  ../../src/GrassiBoard.AudioEngine/include
  ../../src/GrassiBoard.AudioEngine/src
)

if(MSVC)
  target_compile_options(GrassiBoard.Mixer.Tests PRIVATE /W4 /WX /permissive- /EHsc)
else()
  target_compile_options(GrassiBoard.Mixer.Tests PRIVATE -Wall -Wextra -Wpedantic -Werror)
endif()

add_test(NAME mixer-dynamics-contract COMMAND GrassiBoard.Mixer.Tests)
