#include "looper_engine.h"

#include <algorithm>
#include <cmath>
#include <new>
#include <thread>

namespace grassiboard {
namespace {
constexpr std::size_t kMonitorCapacityFrames = LooperEngine::SampleRate / 2U;
constexpr std::uint64_t kSeamFadeFrames = 32U;

float SeamGain(const std::uint64_t playhead, const std::uint64_t loopFrames) noexcept
{
    if (loopFrames < kSeamFadeFrames * 4U) return 1.0F;
    if (playhead < kSeamFadeFrames) {
        return std::clamp((playhead + 1U) / static_cast<float>(kSeamFadeFrames), 0.0F, 1.0F);
    }
    const std::uint64_t framesFromEnd = loopFrames - playhead;
    if (framesFromEnd <= kSeamFadeFrames) {
        return std::clamp(framesFromEnd / static_cast<float>(kSeamFadeFrames), 0.0F, 1.0F);
    }
    return 1.0F;
}
}

LooperEngine::LooperEngine()
    : monitor_tap_(kMonitorCapacityFrames)
{
}

gb_result LooperEngine::LoadMaster(
    const float* const stereoSamples,
    const std::uint64_t frameCount)
{
    if (stereoSamples == nullptr || frameCount == 0U || frameCount > MaxSupportedLoopFrames) {
        return GB_ERROR_INVALID_ARGUMENT;
    }

    transport_.store(GB_LOOPER_STOPPED, std::memory_order_release);
    BeginMutation();
    monitor_tap_.Reset();
    try {
        const std::size_t sampleCount = static_cast<std::size_t>(frameCount * Channels);
        master_samples_.assign(stereoSamples, stereoSamples + sampleCount);
        for (float& sample : master_samples_) {
            if (!std::isfinite(sample)) sample = 0.0F;
        }
        loop_frames_.store(frameCount, std::memory_order_release);
        playhead_frame_.store(0U, std::memory_order_release);
    }
    catch (const std::bad_alloc&) {
        master_samples_.clear();
        loop_frames_.store(0U, std::memory_order_release);
        playhead_frame_.store(0U, std::memory_order_release);
        EndMutation();
        return GB_ERROR_OUT_OF_MEMORY;
    }
    catch (...) {
        master_samples_.clear();
        loop_frames_.store(0U, std::memory_order_release);
        playhead_frame_.store(0U, std::memory_order_release);
        EndMutation();
        return GB_ERROR_INTERNAL;
    }
    EndMutation();
    return GB_OK;
}

void LooperEngine::Clear() noexcept
{
    transport_.store(GB_LOOPER_STOPPED, std::memory_order_release);
    BeginMutation();
    monitor_tap_.Reset();
    // Retain capacity so redefining a Master does not force another large allocation.
    master_samples_.clear();
    loop_frames_.store(0U, std::memory_order_release);
    playhead_frame_.store(0U, std::memory_order_release);
    EndMutation();
}

gb_result LooperEngine::SetTransport(const std::uint32_t transport) noexcept
{
    if (transport > GB_LOOPER_PLAYING) return GB_ERROR_INVALID_ARGUMENT;
    if (transport == GB_LOOPER_PLAYING && loop_frames_.load(std::memory_order_acquire) == 0U) {
        return GB_ERROR_NOT_RUNNING;
    }

    if (transport == GB_LOOPER_PLAYING) {
        transport_.store(GB_LOOPER_PLAYING, std::memory_order_release);
        return GB_OK;
    }

    // Publish Pause/Stop first, then wait out any in-flight realtime frame before
    // finalizing the control state. This guarantees Stop returns exactly to frame 0
    // and Pause cannot be followed by a late playhead increment.
    transport_.store(transport, std::memory_order_release);
    BeginMutation();
    if (transport == GB_LOOPER_STOPPED) {
        playhead_frame_.store(0U, std::memory_order_release);
    }
    monitor_tap_.Reset();
    EndMutation();
    return GB_OK;
}

gb_result LooperEngine::Seek(const std::uint64_t frame) noexcept
{
    const std::uint64_t loopFrames = loop_frames_.load(std::memory_order_acquire);
    if (loopFrames == 0U || frame >= loopFrames) return GB_ERROR_INVALID_ARGUMENT;

    BeginMutation();
    monitor_tap_.Reset();
    playhead_frame_.store(frame, std::memory_order_release);
    EndMutation();
    return GB_OK;
}

void LooperEngine::RenderFrame() noexcept
{
    if (mutation_requested_.load(std::memory_order_acquire)) return;

    rendering_.store(true, std::memory_order_release);
    if (mutation_requested_.load(std::memory_order_acquire)) {
        rendering_.store(false, std::memory_order_release);
        return;
    }

    if (transport_.load(std::memory_order_acquire) == GB_LOOPER_PLAYING) {
        const std::uint64_t frameCount = loop_frames_.load(std::memory_order_acquire);
        const std::uint64_t playhead = playhead_frame_.load(std::memory_order_relaxed);
        if (frameCount > 0U && playhead < frameCount) {
            const std::size_t sample = static_cast<std::size_t>(playhead * Channels);
            if (sample + 1U < master_samples_.size()) {
                const float gain = SeamGain(playhead, frameCount);
                monitor_tap_.Push(master_samples_[sample] * gain, master_samples_[sample + 1U] * gain);
                playhead_frame_.store(
                    playhead + 1U == frameCount ? 0U : playhead + 1U,
                    std::memory_order_release);
            }
        }
    }

    rendering_.store(false, std::memory_order_release);
}

std::uint32_t LooperEngine::ReadMonitor(
    float* const stereoSamples,
    const std::uint32_t capacityFrames) noexcept
{
    return monitor_tap_.Read(stereoSamples, capacityFrames);
}

void LooperEngine::GetState(gb_looper_state& state) const noexcept
{
    state = {};
    state.struct_size = sizeof(gb_looper_state);
    state.transport = transport_.load(std::memory_order_acquire);
    state.sample_rate = SampleRate;
    state.channels = Channels;
    state.loop_frames = loop_frames_.load(std::memory_order_acquire);
    state.playhead_frame = playhead_frame_.load(std::memory_order_acquire);
    state.monitor_fill_frames = monitor_tap_.FillFrames();
    state.monitor_capacity_frames = monitor_tap_.CapacityFrames();
    state.monitor_overrun_count = monitor_tap_.OverrunCount();
}

void LooperEngine::AdvanceForDiagnostics(const std::uint64_t frameCount) noexcept
{
    if (transport_.load(std::memory_order_acquire) != GB_LOOPER_PLAYING) return;
    AdvanceUnsafe(frameCount);
}

void LooperEngine::BeginMutation() noexcept
{
    mutation_requested_.store(true, std::memory_order_release);
    while (rendering_.load(std::memory_order_acquire)) {
        std::this_thread::yield();
    }
}

void LooperEngine::EndMutation() noexcept
{
    mutation_requested_.store(false, std::memory_order_release);
}

void LooperEngine::AdvanceUnsafe(const std::uint64_t frameCount) noexcept
{
    const std::uint64_t loopFrames = loop_frames_.load(std::memory_order_acquire);
    if (loopFrames == 0U || frameCount == 0U) return;
    const std::uint64_t playhead = playhead_frame_.load(std::memory_order_relaxed);
    playhead_frame_.store(
        (playhead + (frameCount % loopFrames)) % loopFrames,
        std::memory_order_release);
}

}