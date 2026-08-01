add_library(GrassiBoard.AudioEngine SHARED
  include/grassiboard/audio_engine.h
  src/audio_engine.cpp
)

target_compile_features(GrassiBoard.AudioEngine PUBLIC cxx_std_20)
target_include_directories(GrassiBoard.AudioEngine
  PUBLIC
    "$<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>"
)

target_compile_definitions(GrassiBoard.AudioEngine PRIVATE GRASSIBOARD_AUDIO_ENGINE_EXPORTS)

if(MSVC)
  target_compile_options(GrassiBoard.AudioEngine PRIVATE /W4 /WX /permissive- /EHsc)
else()
  target_compile_options(GrassiBoard.AudioEngine PRIVATE -Wall -Wextra -Wpedantic -Werror)
endif()

set_target_properties(GrassiBoard.AudioEngine PROPERTIES
  OUTPUT_NAME "GrassiBoard.AudioEngine"
  VERSION "0.1.0"
)

install(TARGETS GrassiBoard.AudioEngine
  RUNTIME DESTINATION .
  LIBRARY DESTINATION .
  ARCHIVE DESTINATION lib
)
