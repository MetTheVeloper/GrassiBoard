#pragma once

#include "grassiboard/audio_engine.h"
#include "looper_engine.h"
#include "mixer_processor.h"
#include "media_stream.h"
#include "monitor_tap_buffer.h"
#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
#include "remote_input_buffer.h"
#endif
#include "pitch_processor.h"
#include "soundboard_mixer.h"

#include <Windows.h>

#include <atomic>
#include <condition_variable>
#include <cstddef>
#include <cstdint>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

namespace grassiboard {

class FloatRingBuffer final {
public:
    explicit FloatRingBuffer(std::size_t capacity);

    void Reset() noexcept;
    bool Push(float sample) noexcept;
    bool Pop(float& sample) noexcept;
    std::size_t Size() const noexcept;

private:
    std::vector<float> samples_;
    std::size_t read_index_ = 0;
    std::size_t write_index_ = 0;
    std::size_t size_ = 0;
};

class WasapiEngine final {
public:
    WasapiEngine();
    ~WasapiEngine();

    WasapiEngine(const WasapiEngine&) = delete;
    WasapiEngine& operator=(const WasapiEngine&) = delete;

    gb_result Start(const std::string& inputDeviceId, const std::string& monitorDeviceId);
    gb_result Stop();
    void SetPitchSemitones(float semitones) noexcept;
    void SetPitchCents(float cents) noexcept;
    void SetPitchBypass(bool bypass) noexcept;
    void SetFormantSemitones(float semitones) noexcept;
    void SetFormantPreservation(bool preserve) noexcept;
    void SetPitchQuality(PitchQualityMode mode) noexcept;

    gb_result LoadLooperMaster(const float* stereoSamples, std::uint64_t frameCount)
    {
        return looper_engine_.LoadMaster(stereoSamples, frameCount);
    }
    void ClearLooper() noexcept { looper_engine_.Clear(); }
    gb_result SetLooperTransport(std::uint32_t transport) noexcept
    {
        return looper_engine_.SetTransport(transport);
    }
    gb_result SeekLooper(std::uint64_t frame) noexcept { return looper_engine_.Seek(frame); }
    std::uint32_t ReadLooperMonitor(float* stereoSamples, std::uint32_t capacityFrames) noexcept
    {
        return looper_engine_.ReadMonitor(stereoSamples, capacityFrames);
    }
    void GetLooperState(gb_looper_state& state) const noexcept { looper_engine_.GetState(state); }
    gb_result SetLooperTrackAudio(std::uint32_t trackId, const float* monoSamples, std::uint64_t frameCount)
    {
        return looper_engine_.SetTrackAudio(trackId, monoSamples, frameCount);
    }
    gb_result RemoveLooperTrack(std::uint32_t trackId) noexcept { return looper_engine_.RemoveTrack(trackId); }
    gb_result SetLooperTrackMix(std::uint32_t trackId, float gain, float pan, bool muted, bool solo) noexcept
    {
        return looper_engine_.SetTrackMix(trackId, gain, pan, muted, solo);
    }

    gb_result StartLooperRecord() noexcept;
    void StopLooperRecord() noexcept;
    std::uint32_t ReadLooperRecord(float* stereoSamples, std::uint32_t capacityFrames) noexcept;
    void GetLooperRecordState(gb_looper_record_state& state) const noexcept;

    gb_result LoadSoundClip(std::uint64_t key, const float* stereoSamples, std::uint64_t frameCount);
    gb_result PlaySoundClip(std::uint64_t key, float volume, bool loop, bool restart) noexcept;
    gb_result StopSoundClip(std::uint64_t key) noexcept;
    gb_result StopAllSounds() noexcept;
    gb_result WriteMedia(
        const float* stereoSamples,
        std::uint32_t frameCount,
        std::uint32_t& acceptedFrames) noexcept;
    void SetMediaActive(bool active) noexcept;
    void ClearMedia() noexcept;
    void SetMediaMonitorLatency(std::uint32_t latencyFrames) noexcept;
    void SetMicrophoneMuted(bool muted) noexcept;
    void SetMixerSettings(const gb_mixer_settings& settings) noexcept;
    void GetStatistics(gb_audio_statistics& statistics) const noexcept;

#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
    void SetMonitorTapEnabled(bool enabled) noexcept;
    void ClearMonitorTap() noexcept;
    std::uint32_t ReadMonitorTap(float* stereoSamples, std::uint32_t capacityFrames) noexcept;
    void GetMonitorTapStatistics(gb_monitor_tap_statistics& statistics) const noexcept;
    void SetVoiceMonitorTapEnabled(bool enabled) noexcept;
    void ClearVoiceMonitorTap() noexcept;
    std::uint32_t ReadVoiceMonitorTap(float* stereoSamples, std::uint32_t capacityFrames) noexcept;
    void GetVoiceMonitorTapStatistics(gb_monitor_tap_statistics& statistics) const noexcept;
    void SetInputSourceMode(std::uint32_t sourceMode) noexcept;
    std::uint32_t WriteRemoteInput(const float* monoSamples, std::uint32_t frameCount) noexcept;
    void ResetRemoteInput() noexcept;
    void GetRemoteInputStatistics(gb_remote_input_statistics& statistics) const noexcept;
#endif
    std::string GetLastError() const;

private:
    void Worker() noexcept;
    void ResetStatistics() noexcept;
    void SignalStart(gb_result result, HRESULT detail) noexcept;
    void QuiesceLooperRecordWriter() noexcept;

    mutable std::mutex control_mutex_;
    std::mutex start_mutex_;
    std::condition_variable start_condition_;
    std::thread worker_;
    HANDLE stop_event_ = nullptr;
    std::wstring input_device_id_;
    std::wstring monitor_device_id_;
    bool start_complete_ = false;
    gb_result start_result_ = GB_ERROR_INTERNAL;

    FloatRingBuffer ring_buffer_;
    LivePitchProcessor pitch_processor_;
    LooperEngine looper_engine_;
    MixerDynamicsProcessor mixer_processor_{&looper_engine_};
    SoundboardMixer soundboard_mixer_;
    MediaStreamBuffer media_stream_;
    MonitorTapBuffer looper_record_tap_;
    std::atomic<bool> looper_record_enabled_{false};
    std::atomic<bool> looper_record_writing_{false};
    std::atomic<std::uint32_t> looper_record_source_mode_{GB_INPUT_SOURCE_WINDOWS};
    std::atomic<bool> looper_record_source_changed_{false};
#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
    MonitorTapBuffer monitor_tap_;
    MonitorTapBuffer voice_monitor_tap_;
    RemoteInputBuffer remote_input_;
    std::atomic<bool> monitor_tap_enabled_{false};
    std::atomic<bool> voice_monitor_tap_enabled_{false};
    std::atomic<std::uint32_t> input_source_mode_{GB_INPUT_SOURCE_WINDOWS};
    std::atomic<std::uint32_t> active_input_source_mode_{GB_INPUT_SOURCE_WINDOWS};
#endif
    std::vector<float> pitch_input_buffer_;
    std::vector<float> pitch_output_buffer_;
    std::atomic<float> pitch_semitones_{0.0F};
    std::atomic<float> pitch_cents_{0.0F};
    std::atomic<bool> running_{false};
    std::atomic<HRESULT> last_hresult_{S_OK};
    std::atomic<std::uint32_t> capture_buffer_frames_{0};
    std::atomic<std::uint32_t> render_buffer_frames_{0};
    std::atomic<std::uint32_t> ring_buffer_fill_frames_{0};
    std::atomic<std::uint64_t> captured_frames_{0};
    std::atomic<std::uint64_t> rendered_frames_{0};
    std::atomic<std::uint64_t> underrun_count_{0};
    std::atomic<std::uint64_t> overrun_count_{0};
    std::atomic<std::uint64_t> discontinuity_count_{0};
    std::atomic<float> input_peak_{0.0F};
    std::atomic<float> input_rms_{0.0F};
    std::atomic<float> output_peak_{0.0F};
    std::atomic<float> output_rms_{0.0F};
    std::atomic<float> soundboard_peak_{0.0F};
    std::atomic<float> soundboard_rms_{0.0F};
    std::atomic<float> master_peak_{0.0F};
    std::atomic<float> master_rms_{0.0F};
    std::atomic<std::uint64_t> media_underrun_count_{0U};
    std::atomic<float> media_peak_{0.0F};
    std::atomic<float> media_rms_{0.0F};
    std::atomic<std::uint32_t> media_monitor_latency_frames_{0U};
    std::atomic<std::uint32_t> media_alignment_frames_{0U};
    std::atomic<std::uint32_t> media_alignment_pitch_frames_{0U};
    std::atomic<bool> microphone_muted_{false};

    void UpdatePitchTarget() noexcept;
    void UpdateMediaAlignment() noexcept;
};

}
