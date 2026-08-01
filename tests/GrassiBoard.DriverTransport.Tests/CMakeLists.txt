add_executable(GrassiBoard.DriverTransport.Tests pcm_ring_tests.cpp)
target_compile_features(GrassiBoard.DriverTransport.Tests PRIVATE cxx_std_20)
target_include_directories(GrassiBoard.DriverTransport.Tests PRIVATE
  ${PROJECT_SOURCE_DIR}/src/GrassiBoard.Driver/Sysvad/EndpointsCommon
)

if(MSVC)
  target_compile_options(GrassiBoard.DriverTransport.Tests PRIVATE /W4 /WX /permissive- /EHsc)
else()
  target_compile_options(GrassiBoard.DriverTransport.Tests PRIVATE -Wall -Wextra -Wpedantic -Werror)
endif()

add_test(NAME GrassiBoard.Driver.PcmTransportPolicy COMMAND GrassiBoard.DriverTransport.Tests)
