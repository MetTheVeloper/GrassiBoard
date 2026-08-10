#include "media_stream.h"

#include <algorithm>
#include <cmath>

namespace grassiboard {

MediaStreamBuffer::MediaStreamBuffer(const std::size_t capacityFrames)
    : samples_(std::max<std::size_t>(capacityFrames, 1U) * 2U, 0.0F)
    , capacity_frames_(std::max<std::size_t>(capacityFrames, 1U))
{
}

std::uint32_t MediaStreamBuffer::Write(
    const float* const interleavedStereoSamples,
    const std::uint32_t frameCount) noexcept
{
    if (interleavedStereoSamples == nullptr || frameCount == 0U) {
        return 0U;
    }

    const std::uint64_t write = write_frame_.load(std::memory_order_relaxed);
    const std::uint64_t read = read_frame_.load(std::memory_order_acquire);
    const std::uint64_t used = std::min(write - read, capacity_frames_);
    const std::uint32_t accepted = static_cast<std::uint32_t>(
        std::min<std::uint64_t>(frameCount, capacity_frames_ - used));
    for (std::uint32_t frame = 0U; frame < accepted; ++frame) {
        const std::uint64_t slot = (write + frame) % capacity_frames_;
        samples_[slot * 2U] = Safe(interleavedStereoSamples[frame * 2U]);
        samples_[slot * 2U + 1U] = Safe(interleavedStereoSamples[frame * 2U + 1U]);
    }
    write_frame_.store(write + accepted, std::memory_order_release);
    return accepted;
}

bool MediaStreamBuffer::Pop(float& left, float& right) noexcept
{
    if (!active_.load(std::memory_order_relaxed)) {
        left = 0.0F;
        right = 0.0F;
        return false;
    }

    if (silence_frames_remaining_ > 0U) {
        --silence_frames_remaining_;
        left = 0.0F;
        right = 0.0F;
        return true;
    }

    const std::uint64_t read = read_frame_.load(std::memory_order_relaxed);
    const std::uint64_t write = write_frame_.load(std::memory_order_acquire);
    if (read >= write) {
        left = 0.0F;
        right = 0.0F;
        return false;
    }
    const std::uint64_t slot = read % capacity_frames_;
    left = samples_[slot * 2U];
    right = samples_[slot * 2U + 1U];
    read_frame_.store(read + 1U, std::memory_order_release);
    return true;
}

void MediaStreamBuffer::SynchronizeDelay(const std::uint32_t delayFrames) noexcept
{
    const std::uint64_t activation = activation_sequence_.load(std::memory_order_acquire);
    if (activation != observed_activation_sequence_) {
        observed_activation_sequence_ = activation;
        applied_delay_frames_ = delayFrames;
        silence_frames_remaining_ = delayFrames;
        return;
    }

    if (!active_.load(std::memory_order_relaxed) || delayFrames == applied_delay_frames_) {
        return;
    }

    if (delayFrames > applied_delay_frames_) {
        silence_frames_remaining_ += delayFrames - applied_delay_frames_;
    }
    else {
        std::uint32_t reduction = applied_delay_frames_ - delayFrames;
        const std::uint32_t pendingReduction = std::min(reduction, silence_frames_remaining_);
        silence_frames_remaining_ -= pendingReduction;
        reduction -= pendingReduction;

        if (reduction > 0U) {
            const std::uint64_t read = read_frame_.load(std::memory_order_relaxed);
            const std::uint64_t write = write_frame_.load(std::memory_order_acquire);
            const std::uint64_t available = std::min(write - read, capacity_frames_);
            read_frame_.store(read + std::min<std::uint64_t>(reduction, available), std::memory_order_release);
        }
    }
    applied_delay_frames_ = delayFrames;
}

void MediaStreamBuffer::Clear() noexcept
{
    const std::uint64_t write = write_frame_.load(std::memory_order_acquire);
    read_frame_.store(write, std::memory_order_release);
}

void MediaStreamBuffer::SetActive(const bool active) noexcept
{
    const bool wasActive = active_.exchange(active, std::memory_order_acq_rel);
    if (active && !wasActive) {
        activation_sequence_.fetch_add(1U, std::memory_order_release);
    }
}

bool MediaStreamBuffer::IsActive() const noexcept
{
    return active_.load(std::memory_order_acquire);
}

std::uint32_t MediaStreamBuffer::FillFrames() const noexcept
{
    const std::uint64_t write = write_frame_.load(std::memory_order_acquire);
    const std::uint64_t read = read_frame_.load(std::memory_order_acquire);
    return static_cast<std::uint32_t>(std::min(write - read, capacity_frames_));
}

std::uint32_t MediaStreamBuffer::CapacityFrames() const noexcept
{
    return static_cast<std::uint32_t>(capacity_frames_);
}

float MediaStreamBuffer::Safe(const float sample) noexcept
{
    return std::isfinite(sample) ? std::clamp(sample, -4.0F, 4.0F) : 0.0F;
}

}
