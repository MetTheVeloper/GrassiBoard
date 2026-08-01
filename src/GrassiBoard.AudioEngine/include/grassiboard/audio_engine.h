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

using gb_engine_handle = void*;

enum gb_result : std::int32_t {
    GB_OK = 0,
    GB_ERROR_INVALID_ARGUMENT = 1,
    GB_ERROR_OUT_OF_MEMORY = 2,
    GB_ERROR_COM = 3,
    GB_ERROR_DEVICE_NOT_FOUND = 4,
    GB_ERROR_AUDIO_CLIENT = 5,
    GB_ERROR_ALREADY_RUNNING = 6,
    GB_ERROR_NOT_RUNNING = 7,
    GB_ERROR_BUFFER_TOO_SMALL = 8,
    GB_ERROR_INTERNAL = 9
};

struct gb_audio_statistics {
    std::uint32_t struct_size;
    std::uint32_t running;
    std::uint32_t sample_rate;
    std::uint32_t capture_buffer_frames;
    std::uint32_t render_buffer_frames;
    std::uint32_t ring_buffer_fill_frames;
    std::uint32_t pitch_latency_samples;
    std::uint64_t captured_frames;
    std::uint64_t rendered_frames;
    std::uint64_t underrun_count;
    std::uint64_t overrun_count;
    std::uint64_t discontinuity_count;
    float input_peak;
    float input_rms;
    float output_peak;
    float output_rms;
};

GB_API std::uint32_t GB_CALL gb_get_api_version() noexcept;
GB_API const char* GB_CALL gb_get_version() noexcept;
GB_API std::uint32_t GB_CALL gb_engine_ping(std::uint32_t value) noexcept;

// Device lists are returned as UTF-8 JSON. Call with a null buffer to obtain
// the required byte count (including the null terminator).
GB_API gb_result GB_CALL gb_enumerate_input_devices(
    char* buffer,
    std::uint32_t capacity,
    std::uint32_t* required) noexcept;
GB_API gb_result GB_CALL gb_enumerate_output_devices(
    char* buffer,
    std::uint32_t capacity,
    std::uint32_t* required) noexcept;

GB_API gb_result GB_CALL gb_engine_create(
    std::uint32_t requested_api_version,
    gb_engine_handle* engine) noexcept;
GB_API void GB_CALL gb_engine_destroy(gb_engine_handle engine) noexcept;
GB_API gb_result GB_CALL gb_engine_start(
    gb_engine_handle engine,
    const char* input_device_id_utf8,
    const char* monitor_device_id_utf8) noexcept;
GB_API gb_result GB_CALL gb_engine_stop(gb_engine_handle engine) noexcept;
GB_API gb_result GB_CALL gb_set_pitch_semitones(gb_engine_handle engine, float semitones) noexcept;
GB_API gb_result GB_CALL gb_set_pitch_cents(gb_engine_handle engine, float cents) noexcept;
GB_API gb_result GB_CALL gb_set_pitch_bypass(gb_engine_handle engine, std::uint32_t bypass) noexcept;
GB_API gb_result GB_CALL gb_set_formant_semitones(gb_engine_handle engine, float semitones) noexcept;
GB_API gb_result GB_CALL gb_set_formant_preservation(gb_engine_handle engine, std::uint32_t preserve) noexcept;
GB_API gb_result GB_CALL gb_set_pitch_quality(gb_engine_handle engine, std::uint32_t quality_mode) noexcept;
GB_API gb_result GB_CALL gb_get_audio_statistics(
    gb_engine_handle engine,
    gb_audio_statistics* statistics) noexcept;
GB_API gb_result GB_CALL gb_get_last_error(
    gb_engine_handle engine,
    char* buffer,
    std::uint32_t capacity,
    std::uint32_t* required) noexcept;

}
