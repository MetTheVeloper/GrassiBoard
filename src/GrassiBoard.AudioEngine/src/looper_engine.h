#pragma once

#include "grassiboard/audio_engine.h"
#include "monitor_tap_buffer.h"

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <vector>

namespace grassiboard {

class LooperEngine final {
public:
    static constexpr std::uint32_t SampleRate = 48'000U;
    static constexpr std::uint32_t Channels = 2U;
    static constexpr std::uint32_t MaxSupportedLoopMinutes = 10U;
    static constexpr std::uint64_t MaxSupportedLoopFrames =
        static_cast<std::uint64_t>(SampleRate) * 60U * MaxSupportedLoopMinutes;

    LooperEngine();

    gb_result LoadMaster(const float* stereoSamples, std::uint64_t frameCount);
    void Clear() noexcept;
    gb_result SetTransport(std::uint32_t transport) noexcept;
    gb_result Seek(std::uint64_t frame) noexcept;
    void RenderFrame() noexcept;
    std::uint32_t ReadMonitor(float* stereoSamples, std::uint32_t capacityFrames) noexcept;
    void GetState(gb_looper_state& state) const noexcept;

    // Deterministic non-realtime test hook. It advances the same modulo clock
    // without producing monitor PCM, so long-soak clock arithmetic is testable
    // without iterating hundreds of millions of frames.
    void AdvanceForDiagnostics(std::uint64_t frameCount) noexcept;

    static constexpr std::uint64_t StereoFloatBytes(const std::uint64_t frameCount) noexcept
    {
        return frameCount * Channels * sizeof(float);
    }

private:
    void BeginMutation() noexcept;
    void EndMutation() noexcept;
    void AdvanceUnsafe(std::uint64_t frameCount) noexcept;

    MonitorTapBuffer monitor_tap_;
    std::vector<float> master_samples_;
    std::atomic<std::uint32_t> transport_{GB_LOOPER_STOPPED};
    std::atomic<std::uint64_t> loop_frames_{0U};
    std::atomic<std::uint64_t> playhead_frame_{0U};
    std::atomic<bool> mutation_requested_{false};
    std::atomic<bool> rendering_{false};
};

}