#include "monitor_tap_buffer.h"

#include <algorithm>

namespace grassiboard {

MonitorTapBuffer::MonitorTapBuffer(const std::size_t capacityFrames)
    : samples_(std::max<std::size_t>(capacityFrames, 2U) * 2U, 0.0F)
    , capacity_frames_(std::max<std::size_t>(capacityFrames, 2U))
{
}

void MonitorTapBuffer::Reset() noexcept
{
    const std::size_t write = write_index_.load(std::memory_order_acquire);
    read_index_.store(write, std::memory_order_release);
    overrun_count_.store(0U, std::memory_order_relaxed);
}

bool MonitorTapBuffer::Push(const float left, const float right) noexcept
{
    const std::size_t write = write_index_.load(std::memory_order_relaxed);
    const std::size_t next = (write + 1U) % capacity_frames_;
    if (next == read_index_.load(std::memory_order_acquire)) {
        overrun_count_.fetch_add(1U, std::memory_order_relaxed);
        return false;
    }

    samples_[write * 2U] = left;
    samples_[write * 2U + 1U] = right;
    write_index_.store(next, std::memory_order_release);
    return true;
}

std::uint32_t MonitorTapBuffer::Read(
    float* const interleavedStereo,
    const std::uint32_t capacityFrames) noexcept
{
    if (interleavedStereo == nullptr || capacityFrames == 0U) {
        return 0U;
    }

    std::size_t read = read_index_.load(std::memory_order_relaxed);
    const std::size_t write = write_index_.load(std::memory_order_acquire);
    std::uint32_t copied = 0U;
    while (read != write && copied < capacityFrames) {
        interleavedStereo[static_cast<std::size_t>(copied) * 2U] = samples_[read * 2U];
        interleavedStereo[static_cast<std::size_t>(copied) * 2U + 1U] = samples_[read * 2U + 1U];
        read = (read + 1U) % capacity_frames_;
        ++copied;
    }
    read_index_.store(read, std::memory_order_release);
    return copied;
}

std::uint32_t MonitorTapBuffer::FillFrames() const noexcept
{
    const std::size_t read = read_index_.load(std::memory_order_acquire);
    const std::size_t write = write_index_.load(std::memory_order_acquire);
    const std::size_t fill = write >= read ? write - read : capacity_frames_ - read + write;
    return static_cast<std::uint32_t>(fill);
}

std::uint32_t MonitorTapBuffer::CapacityFrames() const noexcept
{
    return static_cast<std::uint32_t>(capacity_frames_ - 1U);
}

std::uint64_t MonitorTapBuffer::OverrunCount() const noexcept
{
    return overrun_count_.load(std::memory_order_relaxed);
}

}
