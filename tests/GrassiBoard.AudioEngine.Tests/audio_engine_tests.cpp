#include "grassiboard/audio_engine.h"

#include <cstdint>
#include <cstring>
#include <iostream>

int main()
{
    if (gb_get_api_version() != 1U) {
        std::cerr << "Unexpected native API version.\n";
        return 1;
    }

    if (std::strcmp(gb_get_version(), "0.1.0") != 0) {
        std::cerr << "Unexpected native engine version.\n";
        return 2;
    }

    constexpr std::uint32_t value = 0x12345678U;
    if (gb_engine_ping(value) != (value ^ 0x47524244U)) {
        std::cerr << "Native ping failed.\n";
        return 3;
    }

    std::cout << "Native ABI smoke test passed.\n";
    return 0;
}
