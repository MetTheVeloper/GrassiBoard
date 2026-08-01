#pragma once

#include "grassiboard/audio_engine.h"

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
    void GetStatistics(gb_audio_statistics& statistics) const noexcept;
    std::string GetLastError() const;

private:
    void Worker() noexcept;
    void ResetStatistics() noexcept;
    void SignalStart(gb_result result, HRESULT detail) noexcept;

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
};

}
