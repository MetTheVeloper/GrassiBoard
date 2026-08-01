#include "grassiboard/audio_engine.h"

namespace {
constexpr std::uint32_t kApiVersion = 1;
constexpr char kEngineVersion[] = "0.1.0";
}

std::uint32_t GB_CALL gb_get_api_version() noexcept
{
    return kApiVersion;
}

const char* GB_CALL gb_get_version() noexcept
{
    return kEngineVersion;
}

std::uint32_t GB_CALL gb_engine_ping(const std::uint32_t value) noexcept
{
    return value ^ 0x47524244U;
}
