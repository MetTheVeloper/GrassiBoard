#pragma once

#include <cstdint>

#if defined(_WIN32)
  #if defined(GRASSIBOARD_AUDIO_ENGINE_EXPORTS)
    #define GB_API __declspec(dllexport)
  #else
    #define GB_API __declspec(dllimport)
  #endif
  #define GB_CALL __cdecl
#else
  #define GB_API __attribute__((visibility("default")))
  #define GB_CALL
#endif

extern "C" {

GB_API std::uint32_t GB_CALL gb_get_api_version() noexcept;
GB_API const char* GB_CALL gb_get_version() noexcept;
GB_API std::uint32_t GB_CALL gb_engine_ping(std::uint32_t value) noexcept;

}
