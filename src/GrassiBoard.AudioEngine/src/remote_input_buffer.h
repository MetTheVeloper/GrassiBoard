#pragma once

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <vector>

namespace grassiboard {

// ABI-10 single-producer/single-consumer mono 48 kHz float ring.
// Managed code is the sole producer; the realtime render thread is the sole
// consumer. No allocation, mutex, networking, codec work, or logging occurs in
// Write/Read after construction. The managed bridge resets only while the realtime
// consumer is confirmed on Windows Mic. Read still advances with compare/exchange
// defensively so an overlapping control-plane Reset can never move the consumer
// sequence backwards or resurrect discarded audio.
class RemoteInputBuffer final {
public:
    explicit RemoteInputBuffer(std::size_t capacityFrames);

    void Reset() noexcept;
    std::uint32_t Write(const float* monoSamples, std::uint32_t frameCount) noexcept;
    std::uint32_t Read(float* monoSamples, std::uint32_t frameCount) noexcept;
    std::uint32_t FillFrames() const noexcept;
    std::uint32_t CapacityFrames() const noexcept;
    std::uint64_t PushedFrames() const noexcept;
    std::uint64_t ConsumedFrames() const noexcept;
    std::uint64_t UnderrunFrames() const noexcept;
    std::uint64_t OverrunFrames() const noexcept;

private:
    std::vector<float> samples_;
    const std::uint64_t capacity_frames_;
    std::atomic<std::uint64_t> read_sequence_{0U};
    std::atomic<std::uint64_t> write_sequence_{0U};
    std::atomic<std::uint64_t> pushed_frames_{0U};
    std::atomic<std::uint64_t> consumed_frames_{0U};
    std::atomic<std::uint64_t> underrun_frames_{0U};
    std::atomic<std::uint64_t> overrun_frames_{0U};
};

}
