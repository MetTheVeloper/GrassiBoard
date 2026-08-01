add_executable(GrassiBoard.AudioEngine.Tests audio_engine_tests.cpp)
target_compile_features(GrassiBoard.AudioEngine.Tests PRIVATE cxx_std_20)
target_link_libraries(GrassiBoard.AudioEngine.Tests PRIVATE GrassiBoard.AudioEngine)

if(MSVC)
  target_compile_options(GrassiBoard.AudioEngine.Tests PRIVATE /W4 /WX /permissive- /EHsc)
else()
  target_compile_options(GrassiBoard.AudioEngine.Tests PRIVATE -Wall -Wextra -Wpedantic -Werror)
endif()

add_test(NAME GrassiBoard.AudioEngine.AbiSmokeTest COMMAND GrassiBoard.AudioEngine.Tests)
