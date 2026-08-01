add_library(GrassiBoard.AudioEngine SHARED
  include/grassiboard/audio_engine.h
  src/audio_engine.cpp
  src/device_enumerator.cpp
  src/device_enumerator.h
  src/pitch_processor.cpp
  src/pitch_processor.h
  src/wasapi_engine.cpp
  src/wasapi_engine.h
)

target_compile_features(GrassiBoard.AudioEngine PUBLIC cxx_std_20)
target_include_directories(GrassiBoard.AudioEngine
  PUBLIC
    "$<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>"
)
target_link_libraries(GrassiBoard.AudioEngine PRIVATE signalsmith-stretch)

target_compile_definitions(GrassiBoard.AudioEngine PRIVATE GRASSIBOARD_AUDIO_ENGINE_EXPORTS)
target_compile_definitions(GrassiBoard.AudioEngine PRIVATE _WIN32_WINNT=0x0A00 WIN32_LEAN_AND_MEAN NOMINMAX)
target_link_libraries(GrassiBoard.AudioEngine PRIVATE avrt ole32)

if(MSVC)
  target_compile_options(GrassiBoard.AudioEngine PRIVATE /W4 /WX /permissive- /EHsc)
else()
  target_compile_options(GrassiBoard.AudioEngine PRIVATE -Wall -Wextra -Wpedantic -Werror)
endif()

set_target_properties(GrassiBoard.AudioEngine PROPERTIES
  OUTPUT_NAME "GrassiBoard.AudioEngine"
  VERSION "0.6.0"
)

install(TARGETS GrassiBoard.AudioEngine
  RUNTIME DESTINATION .
  LIBRARY DESTINATION .
  ARCHIVE DESTINATION lib
)
