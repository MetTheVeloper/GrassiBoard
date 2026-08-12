#pragma once

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <vector>

namespace grassiboard {

// Single-producer / single-consumer interleaved stereo float ring used only by
// the optional v1.2 Remote Monitor tap. The realtime render thread is the sole
// producer and the managed Remote Monitor worker is the sole consumer.
// Push/Pop are bounded and allocation-free after construction.
class MonitorTapBuffer final {
public:
    explicit MonitorTapBuffer(std::size_t capacityFrames);

    void Reset() noexcept;
    bool Push(float left, float right) noexcept;
    std::uint32_t Read(float* interleavedStereo, std::uint32_t capacityFrames) noexcept;
    std::uint32_t FillFrames() const noexcept;
    std::uint32_t CapacityFrames() const noexcept;
    std::uint64_t OverrunCount() const noexcept;

private:
    std::vector<float> samples_;
    const std::size_t capacity_frames_;
    std::atomic<std::size_t> read_index_{0U};
    std::atomic<std::size_t> write_index_{0U};
    std::atomic<std::uint64_t> overrun_count_{0U};
};

}
