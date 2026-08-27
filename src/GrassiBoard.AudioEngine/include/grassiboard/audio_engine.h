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
    GB_ERROR_INTERNAL = 9,
    GB_ERROR_QUEUE_FULL = 10
};

enum gb_input_source_mode : std::uint32_t {
    GB_INPUT_SOURCE_WINDOWS = 0,
    GB_INPUT_SOURCE_REMOTE = 1
};

enum gb_looper_transport : std::uint32_t {
    GB_LOOPER_STOPPED = 0,
    GB_LOOPER_PAUSED = 1,
    GB_LOOPER_PLAYING = 2
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
    float soundboard_peak;
    float soundboard_rms;
    float master_peak;
    float master_rms;
    std::uint32_t active_sound_count;
    std::uint32_t microphone_muted;
    std::uint32_t media_buffer_fill_frames;
    std::uint32_t media_buffer_capacity_frames;
    std::uint64_t media_underrun_count;
    float media_peak;
    float media_rms;
    std::uint32_t media_active;
    std::uint32_t media_alignment_frames;
};

struct gb_looper_state {
    std::uint32_t struct_size;
    std::uint32_t transport;
    std::uint32_t sample_rate;
    std::uint32_t channels;
    std::uint64_t loop_frames;
    std::uint64_t playhead_frame;
    std::uint32_t monitor_fill_frames;
    std::uint32_t monitor_capacity_frames;
    std::uint64_t monitor_overrun_count;
};

struct gb_looper_record_state {
    std::uint32_t struct_size;
    std::uint32_t active;
    std::uint32_t source_mode;
    std::uint32_t source_changed;
    std::uint32_t fill_frames;
    std::uint32_t capacity_frames;
    std::uint64_t captured_frames;
    std::uint64_t overrun_frames;
};

#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
struct gb_monitor_tap_statistics {
    std::uint32_t struct_size;
    std::uint32_t enabled;
    std::uint32_t fill_frames;
    std::uint32_t capacity_frames;
    std::uint64_t overrun_count;
};

struct gb_remote_input_statistics {
    std::uint32_t struct_size;
    std::uint32_t requested_source_mode;
    std::uint32_t active_source_mode;
    std::uint32_t fill_frames;
    std::uint32_t capacity_frames;
    std::uint64_t pushed_frames;
    std::uint64_t consumed_frames;
    std::uint64_t underrun_frames;
    std::uint64_t overrun_frames;
};
#endif

struct gb_mixer_settings {
    std::uint32_t struct_size;
    float mic_gain_db;
    float soundboard_gain_db;
    float master_gain_db;
    float gate_threshold_db;
    float compressor_threshold_db;
    float compressor_ratio;
    float limiter_ceiling_db;
    float ducking_amount_db;
    float pitch_wet_mix;
    std::uint32_t gate_enabled;
    std::uint32_t compressor_enabled;
    std::uint32_t limiter_enabled;
    std::uint32_t ducking_enabled;
    std::uint32_t clipping_protection_enabled;
};

GB_API std::uint32_t GB_CALL gb_get_api_version() noexcept;
GB_API const char* GB_CALL gb_get_version() noexcept;
GB_API std::uint32_t GB_CALL gb_engine_ping(std::uint32_t value) noexcept;
GB_API gb_result GB_CALL gb_enumerate_input_devices(char* buffer, std::uint32_t capacity, std::uint32_t* required) noexcept;
GB_API gb_result GB_CALL gb_enumerate_output_devices(char* buffer, std::uint32_t capacity, std::uint32_t* required) noexcept;
GB_API gb_result GB_CALL gb_engine_create(std::uint32_t requested_api_version, gb_engine_handle* engine) noexcept;
GB_API void GB_CALL gb_engine_destroy(gb_engine_handle engine) noexcept;
GB_API gb_result GB_CALL gb_engine_start(gb_engine_handle engine, const char* input_device_id_utf8, const char* monitor_device_id_utf8) noexcept;
GB_API gb_result GB_CALL gb_engine_stop(gb_engine_handle engine) noexcept;
GB_API gb_result GB_CALL gb_set_pitch_semitones(gb_engine_handle engine, float semitones) noexcept;
GB_API gb_result GB_CALL gb_set_pitch_cents(gb_engine_handle engine, float cents) noexcept;
GB_API gb_result GB_CALL gb_set_pitch_bypass(gb_engine_handle engine, std::uint32_t bypass) noexcept;
GB_API gb_result GB_CALL gb_set_formant_semitones(gb_engine_handle engine, float semitones) noexcept;
GB_API gb_result GB_CALL gb_set_formant_preservation(gb_engine_handle engine, std::uint32_t preserve) noexcept;
GB_API gb_result GB_CALL gb_set_pitch_quality(gb_engine_handle engine, std::uint32_t quality_mode) noexcept;

GB_API gb_result GB_CALL gb_looper_load_master(gb_engine_handle engine, const float* interleaved_stereo_samples, std::uint64_t frame_count) noexcept;
GB_API gb_result GB_CALL gb_looper_clear(gb_engine_handle engine) noexcept;
GB_API gb_result GB_CALL gb_looper_set_transport(gb_engine_handle engine, std::uint32_t transport) noexcept;
GB_API gb_result GB_CALL gb_looper_seek(gb_engine_handle engine, std::uint64_t frame) noexcept;
GB_API gb_result GB_CALL gb_looper_get_state(gb_engine_handle engine, gb_looper_state* state) noexcept;
GB_API gb_result GB_CALL gb_looper_monitor_read(gb_engine_handle engine, float* interleaved_stereo_samples, std::uint32_t capacity_frames, std::uint32_t* read_frames) noexcept;

// Gate 3 dedicated processed-Voice record tap. The realtime worker writes the
// selected microphone after Pitch/Fine Pitch/Formant/Voice FX/Dry-Wet but before
// Program Mic Mute, Mic Gain, Gate and Compressor. PCM is duplicated to stereo
// for the managed Master editor; it never enters the Program/VB-CABLE mix.
GB_API gb_result GB_CALL gb_looper_record_start(gb_engine_handle engine) noexcept;
GB_API gb_result GB_CALL gb_looper_record_stop(gb_engine_handle engine) noexcept;
GB_API gb_result GB_CALL gb_looper_record_read(gb_engine_handle engine, float* interleaved_stereo_samples, std::uint32_t capacity_frames, std::uint32_t* read_frames) noexcept;
GB_API gb_result GB_CALL gb_looper_record_get_state(gb_engine_handle engine, gb_looper_record_state* state) noexcept;

GB_API gb_result GB_CALL gb_load_sound_clip(gb_engine_handle engine, std::uint64_t clip_key, const float* interleaved_stereo_samples, std::uint64_t frame_count) noexcept;
GB_API gb_result GB_CALL gb_play_sound_clip(gb_engine_handle engine, std::uint64_t clip_key, float volume, std::uint32_t loop, std::uint32_t restart) noexcept;
GB_API gb_result GB_CALL gb_stop_sound_clip(gb_engine_handle engine, std::uint64_t clip_key) noexcept;
GB_API gb_result GB_CALL gb_stop_all_sounds(gb_engine_handle engine) noexcept;
GB_API gb_result GB_CALL gb_media_write(gb_engine_handle engine, const float* interleaved_stereo_samples, std::uint32_t frame_count, std::uint32_t* accepted_frames) noexcept;
GB_API gb_result GB_CALL gb_media_set_active(gb_engine_handle engine, std::uint32_t active) noexcept;
GB_API gb_result GB_CALL gb_media_clear(gb_engine_handle engine) noexcept;
GB_API gb_result GB_CALL gb_media_set_monitor_latency(gb_engine_handle engine, std::uint32_t latency_frames) noexcept;
GB_API gb_result GB_CALL gb_set_microphone_muted(gb_engine_handle engine, std::uint32_t muted) noexcept;
GB_API gb_result GB_CALL gb_set_mixer_settings(gb_engine_handle engine, const gb_mixer_settings* settings) noexcept;
GB_API gb_result GB_CALL gb_get_audio_statistics(gb_engine_handle engine, gb_audio_statistics* statistics) noexcept;

#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
GB_API gb_result GB_CALL gb_monitor_tap_set_enabled(gb_engine_handle engine, std::uint32_t enabled) noexcept;
GB_API gb_result GB_CALL gb_monitor_tap_clear(gb_engine_handle engine) noexcept;
GB_API gb_result GB_CALL gb_monitor_tap_read(gb_engine_handle engine, float* interleaved_stereo_samples, std::uint32_t capacity_frames, std::uint32_t* read_frames) noexcept;
GB_API gb_result GB_CALL gb_monitor_tap_get_statistics(gb_engine_handle engine, gb_monitor_tap_statistics* statistics) noexcept;
GB_API gb_result GB_CALL gb_voice_monitor_tap_set_enabled(gb_engine_handle engine, std::uint32_t enabled) noexcept;
GB_API gb_result GB_CALL gb_voice_monitor_tap_clear(gb_engine_handle engine) noexcept;
GB_API gb_result GB_CALL gb_voice_monitor_tap_read(gb_engine_handle engine, float* interleaved_stereo_samples, std::uint32_t capacity_frames, std::uint32_t* read_frames) noexcept;
GB_API gb_result GB_CALL gb_voice_monitor_tap_get_statistics(gb_engine_handle engine, gb_monitor_tap_statistics* statistics) noexcept;
GB_API gb_result GB_CALL gb_set_input_source_mode(gb_engine_handle engine, std::uint32_t source_mode) noexcept;
GB_API gb_result GB_CALL gb_remote_input_push(gb_engine_handle engine, const float* mono_samples, std::uint32_t frame_count, std::uint32_t* accepted_frames) noexcept;
GB_API gb_result GB_CALL gb_remote_input_reset(gb_engine_handle engine) noexcept;
GB_API gb_result GB_CALL gb_get_remote_input_statistics(gb_engine_handle engine, gb_remote_input_statistics* statistics) noexcept;
#endif

GB_API gb_result GB_CALL gb_get_last_error(gb_engine_handle engine, char* buffer, std::uint32_t capacity, std::uint32_t* required) noexcept;

}
