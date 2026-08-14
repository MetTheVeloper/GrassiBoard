#include "grassiboard/audio_engine.h"

#include <cstdint>
#include <cstring>
#include <limits>
#include <iostream>

int main()
{
    static_assert(sizeof(gb_audio_statistics) == 144U, "Native statistics ABI layout changed unexpectedly.");
#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
    constexpr std::uint32_t expectedApiVersion = 10U;
    constexpr const char* expectedEngineVersion = "1.3.0-gate2";
#else
    constexpr std::uint32_t expectedApiVersion = 8U;
    constexpr const char* expectedEngineVersion = "1.0.1";
#endif
    if (gb_get_api_version() != expectedApiVersion) {
        std::cerr << "Unexpected native API version.\n";
        return 1;
    }

    const char* const actualEngineVersion = gb_get_version();
    if (actualEngineVersion == nullptr || std::strcmp(actualEngineVersion, expectedEngineVersion) != 0) {
        std::cerr
            << "Unexpected native engine version. Expected '"
            << expectedEngineVersion
            << "', got '"
            << (actualEngineVersion != nullptr ? actualEngineVersion : "<null>")
            << "'.\n";
        return 2;
    }

    constexpr std::uint32_t value = 0x12345678U;
    if (gb_engine_ping(value) != (value ^ 0x47524244U)) {
        std::cerr << "Native ping failed.\n";
        return 3;
    }

    gb_engine_handle engine = nullptr;
    if (gb_engine_create(expectedApiVersion, &engine) != GB_OK || engine == nullptr) {
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
        gb_set_pitch_quality(engine, 3U) != GB_ERROR_INVALID_ARGUMENT ||
        gb_set_microphone_muted(engine, 1U) != GB_OK ||
        gb_set_microphone_muted(engine, 2U) != GB_ERROR_INVALID_ARGUMENT) {
        std::cerr << "Pitch parameter contract failed.\n";
        gb_engine_destroy(engine);
        return 6;
    }

    gb_mixer_settings mixerSettings{};
    mixerSettings.struct_size = static_cast<std::uint32_t>(sizeof(gb_mixer_settings));
    mixerSettings.gate_threshold_db = -55.0F;
    mixerSettings.compressor_threshold_db = -18.0F;
    mixerSettings.compressor_ratio = 3.0F;
    mixerSettings.limiter_ceiling_db = -1.0F;
    mixerSettings.ducking_amount_db = 9.0F;
    mixerSettings.pitch_wet_mix = 0.75F;
    mixerSettings.limiter_enabled = 1U;
    mixerSettings.clipping_protection_enabled = 1U;
    if (gb_set_mixer_settings(engine, &mixerSettings) != GB_OK) {
        std::cerr << "Mixer parameter contract failed.\n";
        gb_engine_destroy(engine);
        return 7;
    }
    mixerSettings.struct_size = 0U;
    if (gb_set_mixer_settings(engine, &mixerSettings) != GB_ERROR_INVALID_ARGUMENT) {
        std::cerr << "Mixer structure-size validation failed.\n";
        gb_engine_destroy(engine);
        return 8;
    }

    constexpr float clip[]{0.25F, -0.25F, 0.5F, -0.5F};
    if (gb_load_sound_clip(engine, 42U, clip, 2U) != GB_OK ||
        gb_load_sound_clip(engine, 0U, clip, 2U) != GB_ERROR_INVALID_ARGUMENT ||
        gb_play_sound_clip(engine, 42U, 1.0F, 0U, 1U) != GB_OK ||
        gb_stop_sound_clip(engine, 42U) != GB_OK ||
        gb_stop_all_sounds(engine) != GB_OK) {
        std::cerr << "Soundboard ABI contract failed.\n";
        gb_engine_destroy(engine);
        return 9;
    }

    std::uint32_t acceptedMediaFrames = 0U;
    if (gb_media_write(engine, clip, 2U, &acceptedMediaFrames) != GB_OK ||
        acceptedMediaFrames != 2U ||
        gb_media_set_monitor_latency(engine, 2'400U) != GB_OK ||
        gb_media_set_active(engine, 1U) != GB_OK ||
        gb_media_set_active(engine, 2U) != GB_ERROR_INVALID_ARGUMENT ||
        gb_get_audio_statistics(engine, &statistics) != GB_OK ||
        statistics.media_buffer_fill_frames != 2U ||
        statistics.media_buffer_capacity_frames < 2U ||
        statistics.media_active != 1U ||
        statistics.media_alignment_frames < 2'400U ||
        gb_media_clear(engine) != GB_OK ||
        gb_get_audio_statistics(engine, &statistics) != GB_OK ||
        statistics.media_buffer_fill_frames != 0U) {
        std::cerr << "Media ABI contract failed.\n";
        gb_engine_destroy(engine);
        return 10;
    }

#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
    static_assert(sizeof(gb_remote_input_statistics) == 56U, "Remote Input ABI statistics layout changed unexpectedly.");
    gb_monitor_tap_statistics tapStatistics{};
    float tapBuffer[8]{};
    std::uint32_t tapReadFrames = 99U;
    if (gb_monitor_tap_set_enabled(engine, 1U) != GB_OK ||
        gb_monitor_tap_set_enabled(engine, 2U) != GB_ERROR_INVALID_ARGUMENT ||
        gb_monitor_tap_read(engine, tapBuffer, 4U, &tapReadFrames) != GB_OK ||
        tapReadFrames != 0U ||
        gb_monitor_tap_get_statistics(engine, &tapStatistics) != GB_OK ||
        tapStatistics.struct_size != sizeof(gb_monitor_tap_statistics) ||
        tapStatistics.enabled != 1U ||
        tapStatistics.capacity_frames < 48'000U ||
        gb_monitor_tap_clear(engine) != GB_OK ||
        gb_monitor_tap_set_enabled(engine, 0U) != GB_OK ||
        gb_voice_monitor_tap_set_enabled(engine, 1U) != GB_OK ||
        gb_voice_monitor_tap_set_enabled(engine, 2U) != GB_ERROR_INVALID_ARGUMENT ||
        gb_voice_monitor_tap_read(engine, tapBuffer, 4U, &tapReadFrames) != GB_OK ||
        tapReadFrames != 0U ||
        gb_voice_monitor_tap_get_statistics(engine, &tapStatistics) != GB_OK ||
        tapStatistics.struct_size != sizeof(gb_monitor_tap_statistics) ||
        tapStatistics.enabled != 1U ||
        tapStatistics.capacity_frames < 48'000U ||
        gb_voice_monitor_tap_clear(engine) != GB_OK ||
        gb_voice_monitor_tap_set_enabled(engine, 0U) != GB_OK) {
        std::cerr << "Remote Monitor ABI-10 Soundboard/My Voice tap contract failed.\n";
        gb_engine_destroy(engine);
        return 11;
    }

    const float remoteInputSamples[]{0.1F, -0.2F, 0.3F, -0.4F};
    std::uint32_t acceptedRemoteFrames = 0U;
    gb_remote_input_statistics remoteStatistics{};
    if (gb_set_input_source_mode(engine, GB_INPUT_SOURCE_REMOTE) != GB_OK ||
        gb_set_input_source_mode(engine, 2U) != GB_ERROR_INVALID_ARGUMENT ||
        gb_remote_input_push(engine, remoteInputSamples, 4U, &acceptedRemoteFrames) != GB_OK ||
        acceptedRemoteFrames != 4U ||
        gb_get_remote_input_statistics(engine, &remoteStatistics) != GB_OK ||
        remoteStatistics.struct_size != sizeof(gb_remote_input_statistics) ||
        remoteStatistics.requested_source_mode != GB_INPUT_SOURCE_REMOTE ||
        remoteStatistics.active_source_mode != GB_INPUT_SOURCE_WINDOWS ||
        remoteStatistics.fill_frames != 4U ||
        remoteStatistics.capacity_frames < 4U ||
        remoteStatistics.pushed_frames != 4U ||
        gb_remote_input_reset(engine) != GB_OK ||
        gb_get_remote_input_statistics(engine, &remoteStatistics) != GB_OK ||
        remoteStatistics.fill_frames != 0U ||
        gb_set_input_source_mode(engine, GB_INPUT_SOURCE_WINDOWS) != GB_OK) {
        std::cerr << "Remote Phone Mic ABI-10 input contract failed.\n";
        gb_engine_destroy(engine);
        return 13;
    }
#endif

    if (gb_engine_stop(engine) != GB_OK) {
        std::cerr << "Stopping an idle engine failed.\n";
        gb_engine_destroy(engine);
        return 12;
    }
    gb_engine_destroy(engine);

    std::cout << "Native ABI smoke test passed.\n";
    return 0;
}
