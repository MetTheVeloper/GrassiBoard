add_executable(GrassiBoard.Pitch.Tests
  pitch_processor_tests.cpp
  ${PROJECT_SOURCE_DIR}/src/GrassiBoard.AudioEngine/src/pitch_processor.cpp
)

target_compile_features(GrassiBoard.Pitch.Tests PRIVATE cxx_std_20)
target_include_directories(GrassiBoard.Pitch.Tests PRIVATE
  ${PROJECT_SOURCE_DIR}/src/GrassiBoard.AudioEngine/src
)
target_link_libraries(GrassiBoard.Pitch.Tests PRIVATE signalsmith-stretch)
target_compile_definitions(GrassiBoard.Pitch.Tests PRIVATE NOMINMAX)

if(MSVC)
  target_compile_options(GrassiBoard.Pitch.Tests PRIVATE /W4 /WX /permissive- /EHsc)
else()
  target_compile_options(GrassiBoard.Pitch.Tests PRIVATE -Wall -Wextra -Wpedantic -Werror)
endif()

add_test(
  NAME GrassiBoard.Pitch.OfflineFileTests
  COMMAND GrassiBoard.Pitch.Tests ${CMAKE_BINARY_DIR}/pitch-test-output
)
