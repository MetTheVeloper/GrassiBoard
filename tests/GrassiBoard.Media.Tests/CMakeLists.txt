add_executable(GrassiBoard.Media.Tests
  media_stream_tests.cpp
  ../../src/GrassiBoard.AudioEngine/src/media_stream.cpp
)

target_compile_features(GrassiBoard.Media.Tests PRIVATE cxx_std_20)
target_include_directories(GrassiBoard.Media.Tests PRIVATE ../../src/GrassiBoard.AudioEngine/src)

if(MSVC)
  target_compile_options(GrassiBoard.Media.Tests PRIVATE /W4 /WX /permissive- /EHsc)
else()
  target_compile_options(GrassiBoard.Media.Tests PRIVATE -Wall -Wextra -Wpedantic -Werror)
endif()

add_test(NAME media-stream-contract COMMAND GrassiBoard.Media.Tests)
