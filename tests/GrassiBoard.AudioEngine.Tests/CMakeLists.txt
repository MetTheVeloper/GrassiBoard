add_executable(GrassiBoard.AudioEngine.Tests audio_engine_tests.cpp)
target_compile_features(GrassiBoard.AudioEngine.Tests PRIVATE cxx_std_20)
target_link_libraries(GrassiBoard.AudioEngine.Tests PRIVATE GrassiBoard.AudioEngine)
if(GRASSIBOARD_REMOTE_MONITOR_TAP)
  target_compile_definitions(GrassiBoard.AudioEngine.Tests PRIVATE GRASSIBOARD_REMOTE_MONITOR_TAP=1)
endif()

# CTest starts the executable from its own output directory. Keep the runtime
# DLL beside it so Windows can resolve the native dependency without PATH edits.
add_custom_command(TARGET GrassiBoard.AudioEngine.Tests POST_BUILD
  COMMAND ${CMAKE_COMMAND} -E copy_if_different
    $<TARGET_FILE:GrassiBoard.AudioEngine>
    $<TARGET_FILE_DIR:GrassiBoard.AudioEngine.Tests>
)

if(MSVC)
  target_compile_options(GrassiBoard.AudioEngine.Tests PRIVATE /W4 /WX /permissive- /EHsc)
else()
  target_compile_options(GrassiBoard.AudioEngine.Tests PRIVATE -Wall -Wextra -Wpedantic -Werror)
endif()

add_test(NAME GrassiBoard.AudioEngine.AbiSmokeTest COMMAND GrassiBoard.AudioEngine.Tests)

add_executable(GrassiBoard.MonitorTap.Tests
  monitor_tap_buffer_tests.cpp
  ${CMAKE_SOURCE_DIR}/src/GrassiBoard.AudioEngine/src/monitor_tap_buffer.cpp
)
target_compile_features(GrassiBoard.MonitorTap.Tests PRIVATE cxx_std_20)
target_include_directories(GrassiBoard.MonitorTap.Tests PRIVATE
  ${CMAKE_SOURCE_DIR}/src/GrassiBoard.AudioEngine/src
)
if(MSVC)
  target_compile_options(GrassiBoard.MonitorTap.Tests PRIVATE /W4 /WX /permissive- /EHsc)
else()
  target_compile_options(GrassiBoard.MonitorTap.Tests PRIVATE -Wall -Wextra -Wpedantic -Werror)
endif()
add_test(NAME GrassiBoard.AudioEngine.MonitorTapRingTest COMMAND GrassiBoard.MonitorTap.Tests)
