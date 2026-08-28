#include "grassiboard/audio_engine.h"

#include "wasapi_engine.h"

namespace {
grassiboard::WasapiEngine* AsEngine(const gb_engine_handle engine) noexcept
{
    return static_cast<grassiboard::WasapiEngine*>(engine);
}
}

gb_result GB_CALL gb_looper_load_master(
    const gb_engine_handle engine,
    const float* const interleaved_stereo_samples,
    const std::uint64_t frame_count) noexcept
{
    if (engine == nullptr || interleaved_stereo_samples == nullptr || frame_count == 0U) return GB_ERROR_INVALID_ARGUMENT;
    try { return AsEngine(engine)->LoadLooperMaster(interleaved_stereo_samples, frame_count); }
    catch (...) { return GB_ERROR_INTERNAL; }
}

gb_result GB_CALL gb_looper_clear(const gb_engine_handle engine) noexcept
{
    if (engine == nullptr) return GB_ERROR_INVALID_ARGUMENT;
    AsEngine(engine)->ClearLooper();
    return GB_OK;
}

gb_result GB_CALL gb_looper_set_transport(const gb_engine_handle engine, const std::uint32_t transport) noexcept
{
    return engine == nullptr ? GB_ERROR_INVALID_ARGUMENT : AsEngine(engine)->SetLooperTransport(transport);
}

gb_result GB_CALL gb_looper_seek(const gb_engine_handle engine, const std::uint64_t frame) noexcept
{
    return engine == nullptr ? GB_ERROR_INVALID_ARGUMENT : AsEngine(engine)->SeekLooper(frame);
}

gb_result GB_CALL gb_looper_get_state(const gb_engine_handle engine, gb_looper_state* const state) noexcept
{
    if (engine == nullptr || state == nullptr) return GB_ERROR_INVALID_ARGUMENT;
    AsEngine(engine)->GetLooperState(*state);
    return GB_OK;
}

gb_result GB_CALL gb_looper_monitor_read(
    const gb_engine_handle engine,
    float* const interleaved_stereo_samples,
    const std::uint32_t capacity_frames,
    std::uint32_t* const read_frames) noexcept
{
    if (engine == nullptr || interleaved_stereo_samples == nullptr || capacity_frames == 0U || read_frames == nullptr) return GB_ERROR_INVALID_ARGUMENT;
    *read_frames = AsEngine(engine)->ReadLooperMonitor(interleaved_stereo_samples, capacity_frames);
    return GB_OK;
}

gb_result GB_CALL gb_looper_track_set_audio(
    const gb_engine_handle engine,
    const std::uint32_t track_id,
    const float* const mono_samples,
    const std::uint64_t frame_count) noexcept
{
    if (engine == nullptr || track_id == 0U || mono_samples == nullptr || frame_count == 0U) return GB_ERROR_INVALID_ARGUMENT;
    try { return AsEngine(engine)->SetLooperTrackAudio(track_id, mono_samples, frame_count); }
    catch (...) { return GB_ERROR_INTERNAL; }
}

gb_result GB_CALL gb_looper_track_remove(
    const gb_engine_handle engine,
    const std::uint32_t track_id) noexcept
{
    return engine == nullptr ? GB_ERROR_INVALID_ARGUMENT : AsEngine(engine)->RemoveLooperTrack(track_id);
}

gb_result GB_CALL gb_looper_track_set_mix(
    const gb_engine_handle engine,
    const std::uint32_t track_id,
    const float gain,
    const float pan,
    const std::uint32_t muted,
    const std::uint32_t solo) noexcept
{
    if (engine == nullptr || muted > 1U || solo > 1U) return GB_ERROR_INVALID_ARGUMENT;
    return AsEngine(engine)->SetLooperTrackMix(track_id, gain, pan, muted != 0U, solo != 0U);
}

gb_result GB_CALL gb_looper_record_start(const gb_engine_handle engine) noexcept
{
    return engine == nullptr ? GB_ERROR_INVALID_ARGUMENT : AsEngine(engine)->StartLooperRecord();
}

gb_result GB_CALL gb_looper_record_stop(const gb_engine_handle engine) noexcept
{
    if (engine == nullptr) return GB_ERROR_INVALID_ARGUMENT;
    AsEngine(engine)->StopLooperRecord();
    return GB_OK;
}

gb_result GB_CALL gb_looper_record_read(
    const gb_engine_handle engine,
    float* const interleaved_stereo_samples,
    const std::uint32_t capacity_frames,
    std::uint32_t* const read_frames) noexcept
{
    if (engine == nullptr || interleaved_stereo_samples == nullptr || capacity_frames == 0U || read_frames == nullptr) return GB_ERROR_INVALID_ARGUMENT;
    *read_frames = AsEngine(engine)->ReadLooperRecord(interleaved_stereo_samples, capacity_frames);
    return GB_OK;
}

gb_result GB_CALL gb_looper_record_get_state(
    const gb_engine_handle engine,
    gb_looper_record_state* const state) noexcept
{
    if (engine == nullptr || state == nullptr) return GB_ERROR_INVALID_ARGUMENT;
    AsEngine(engine)->GetLooperRecordState(*state);
    return GB_OK;
}
