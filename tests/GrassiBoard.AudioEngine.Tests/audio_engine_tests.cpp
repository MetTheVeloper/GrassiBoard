#include "grassiboard/audio_engine.h"

#include <cstdint>
#include <cstring>
#include <limits>
#include <iostream>

int main()
{
    if (gb_get_api_version() != 4U) {
        std::cerr << "Unexpected native API version.\n";
        return 1;
    }

    if (std::strcmp(gb_get_version(), "0.6.1") != 0) {
        std::cerr << "Unexpected native engine version.\n";
        return 2;
    }

    constexpr std::uint32_t value = 0x12345678U;
    if (gb_engine_ping(value) != (value ^ 0x47524244U)) {
        std::cerr << "Native ping failed.\n";
        return 3;
    }

    gb_engine_handle engine = nullptr;
    if (gb_engine_create(4U, &engine) != GB_OK || engine == nullptr) {
        std::cerr << "Engine creation failed.\n";
        return 4;
    }

    gb_audio_statistics statistics{};
    if (gb_get_audio_statistics(engine, &statistics) != GB_OK ||
        statistics.struct_size != static_cast<std::uint32_t>(sizeof(gb_audio_statistics)) ||
        statistics.running != 0U ||
        statistics.sample_rate != 48'000U) {
        std::cerr << "Initial engine statistics are invalid.\n";
        gb_engine_destroy(engine);
        return 5;
    }

    if (gb_set_pitch_semitones(engine, 6.0F) != GB_OK ||
        gb_set_pitch_cents(engine, -25.0F) != GB_OK ||
        gb_set_pitch_bypass(engine, 0U) != GB_OK ||
        gb_set_pitch_bypass(engine, 2U) != GB_ERROR_INVALID_ARGUMENT ||
        gb_set_pitch_semitones(engine, std::numeric_limits<float>::quiet_NaN()) != GB_ERROR_INVALID_ARGUMENT ||
        gb_set_formant_semitones(engine, 3.0F) != GB_OK ||
        gb_set_formant_preservation(engine, 1U) != GB_OK ||
        gb_set_formant_preservation(engine, 2U) != GB_ERROR_INVALID_ARGUMENT ||
        gb_set_pitch_quality(engine, 0U) != GB_OK ||
        gb_set_pitch_quality(engine, 2U) != GB_OK ||
        gb_set_pitch_quality(engine, 3U) != GB_ERROR_INVALID_ARGUMENT) {
        std::cerr << "Pitch parameter contract failed.\n";
        gb_engine_destroy(engine);
        return 6;
    }

    if (gb_engine_stop(engine) != GB_OK) {
        std::cerr << "Stopping an idle engine failed.\n";
        gb_engine_destroy(engine);
        return 7;
    }
    gb_engine_destroy(engine);

    std::cout << "Native ABI smoke test passed.\n";
    return 0;
}
