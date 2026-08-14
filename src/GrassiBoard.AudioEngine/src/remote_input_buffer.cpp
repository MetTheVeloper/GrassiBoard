#include "remote_input_buffer.h"

#include <algorithm>
#include <cmath>

namespace grassiboard {
namespace {
float SafeRemoteSample(const float sample) noexcept
{
    return std::isfinite(sample) ? std::clamp(sample, -1.0F, 1.0F) : 0.0F;
}
}

RemoteInputBuffer::RemoteInputBuffer(const std::size_t capacityFrames)
    : samples_(std::max<std::size_t>(capacityFrames, 2U), 0.0F)
    , capacity_frames_(static_cast<std::uint64_t>(std::max<std::size_t>(capacityFrames, 2U)))
{
}

void RemoteInputBuffer::Reset() noexcept
{
    const std::uint64_t write = write_sequence_.load(std::memory_order_acquire);
    read_sequence_.store(write, std::memory_order_release);
    pushed_frames_.store(0U, std::memory_order_relaxed);
    consumed_frames_.store(0U, std::memory_order_relaxed);
    underrun_frames_.store(0U, std::memory_order_relaxed);
    overrun_frames_.store(0U, std::memory_order_relaxed);
}

std::uint32_t RemoteInputBuffer::Write(
    const float* const monoSamples,
    const std::uint32_t frameCount) noexcept
{
    if (monoSamples == nullptr || frameCount == 0U) {
        return 0U;
    }

    const std::uint64_t write = write_sequence_.load(std::memory_order_relaxed);
    const std::uint64_t read = read_sequence_.load(std::memory_order_acquire);
    const std::uint64_t fill = write - read;
    const std::uint64_t freeFrames = fill >= capacity_frames_ ? 0U : capacity_frames_ - fill;
    const std::uint32_t accepted = static_cast<std::uint32_t>(
        std::min<std::uint64_t>(frameCount, freeFrames));

    for (std::uint32_t frame = 0U; frame < accepted; ++frame) {
        const std::uint64_t sequence = write + frame;
        samples_[static_cast<std::size_t>(sequence % capacity_frames_)] = SafeRemoteSample(monoSamples[frame]);
    }

    if (accepted > 0U) {
        write_sequence_.store(write + accepted, std::memory_order_release);
        pushed_frames_.fetch_add(accepted, std::memory_order_relaxed);
    }
    if (accepted < frameCount) {
        overrun_frames_.fetch_add(frameCount - accepted, std::memory_order_relaxed);
    }
    return accepted;
}

std::uint32_t RemoteInputBuffer::Read(
    float* const monoSamples,
    const std::uint32_t frameCount) noexcept
{
    if (monoSamples == nullptr || frameCount == 0U) {
        return 0U;
    }

    const std::uint64_t read = read_sequence_.load(std::memory_order_relaxed);
    const std::uint64_t write = write_sequence_.load(std::memory_order_acquire);
    const std::uint64_t availableFrames = write - read;
    const std::uint32_t copied = static_cast<std::uint32_t>(
        std::min<std::uint64_t>(frameCount, availableFrames));

    for (std::uint32_t frame = 0U; frame < copied; ++frame) {
        const std::uint64_t sequence = read + frame;
        monoSamples[frame] = samples_[static_cast<std::size_t>(sequence % capacity_frames_)];
    }
    std::fill(monoSamples + copied, monoSamples + frameCount, 0.0F);

    if (copied > 0U) {
        std::uint64_t expectedRead = read;
        if (!read_sequence_.compare_exchange_strong(
                expectedRead, read + copied,
                std::memory_order_acq_rel, std::memory_order_acquire)) {
            // Reset won the race. Do not expose samples from the discarded
            // generation and, critically, never move read_sequence_ backwards.
            std::fill(monoSamples, monoSamples + frameCount, 0.0F);
            underrun_frames_.fetch_add(frameCount, std::memory_order_relaxed);
            return 0U;
        }
        consumed_frames_.fetch_add(copied, std::memory_order_relaxed);
    }
    if (copied < frameCount) {
        underrun_frames_.fetch_add(frameCount - copied, std::memory_order_relaxed);
    }
    return copied;
}

std::uint32_t RemoteInputBuffer::FillFrames() const noexcept
{
    const std::uint64_t read = read_sequence_.load(std::memory_order_acquire);
    const std::uint64_t write = write_sequence_.load(std::memory_order_acquire);
    return static_cast<std::uint32_t>(std::min<std::uint64_t>(write - read, capacity_frames_));
}

std::uint32_t RemoteInputBuffer::CapacityFrames() const noexcept
{
    return static_cast<std::uint32_t>(capacity_frames_);
}

std::uint64_t RemoteInputBuffer::PushedFrames() const noexcept
{
    return pushed_frames_.load(std::memory_order_relaxed);
}

std::uint64_t RemoteInputBuffer::ConsumedFrames() const noexcept
{
    return consumed_frames_.load(std::memory_order_relaxed);
}

std::uint64_t RemoteInputBuffer::UnderrunFrames() const noexcept
{
    return underrun_frames_.load(std::memory_order_relaxed);
}

std::uint64_t RemoteInputBuffer::OverrunFrames() const noexcept
{
    return overrun_frames_.load(std::memory_order_relaxed);
}

}
