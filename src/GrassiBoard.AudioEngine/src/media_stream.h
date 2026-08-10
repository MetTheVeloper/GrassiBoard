#pragma once

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <vector>

namespace grassiboard {

class MediaStreamBuffer final {
public:
    explicit MediaStreamBuffer(std::size_t capacityFrames);

    std::uint32_t Write(
        const float* interleavedStereoSamples,
        std::uint32_t frameCount) noexcept;
    bool Pop(float& left, float& right) noexcept;
    void Clear() noexcept;
    void SetActive(bool active) noexcept;
    bool IsActive() const noexcept;
    std::uint32_t FillFrames() const noexcept;
    std::uint32_t CapacityFrames() const noexcept;

private:
    static float Safe(float sample) noexcept;

    std::vector<float> samples_;
    const std::uint64_t capacity_frames_;
    alignas(64) std::atomic<std::uint64_t> read_frame_{0U};
    alignas(64) std::atomic<std::uint64_t> write_frame_{0U};
    std::atomic<bool> active_{false};
};

}
