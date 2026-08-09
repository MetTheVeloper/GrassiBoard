#include "soundboard_mixer.h"

#include <algorithm>
#include <cmath>
#include <limits>
#include <new>

namespace grassiboard {

gb_result SoundboardMixer::LoadClip(
    const std::uint64_t key,
    const float* const stereoSamples,
    const std::uint64_t frameCount)
{
    if (key == 0U || stereoSamples == nullptr || frameCount == 0U || frameCount > MaxClipFrames ||
        frameCount > std::numeric_limits<std::size_t>::max() / 2U) {
        return GB_ERROR_INVALID_ARGUMENT;
    }

    try {
        auto clip = std::make_unique<Clip>();
        clip->key = key;
        clip->frame_count = frameCount;
        const std::size_t sampleCount = static_cast<std::size_t>(frameCount * 2U);
        clip->samples.assign(stereoSamples, stereoSamples + sampleCount);
        for (float& sample : clip->samples) {
            if (!std::isfinite(sample)) {
                sample = 0.0F;
            }
        }

        const Clip* const current = clip.get();
        std::scoped_lock lock(clip_mutex_);
        clip_storage_.push_back(std::move(clip));
        current_clips_[key] = current;
        return GB_OK;
    }
    catch (const std::bad_alloc&) {
        return GB_ERROR_OUT_OF_MEMORY;
    }
    catch (...) {
        return GB_ERROR_INTERNAL;
    }
}

gb_result SoundboardMixer::Play(
    const std::uint64_t key,
    const float volume,
    const bool loop,
    const bool restart) noexcept
{
    if (key == 0U || !std::isfinite(volume) || volume < 0.0F || volume > 2.0F) {
        return GB_ERROR_INVALID_ARGUMENT;
    }

    const Clip* clip = nullptr;
    {
        std::scoped_lock lock(clip_mutex_);
        const auto match = current_clips_.find(key);
        if (match == current_clips_.end()) {
            return GB_ERROR_DEVICE_NOT_FOUND;
        }
        clip = match->second;
    }

    const Command command{CommandType::Play, clip, key, volume, loop, restart};
    return Enqueue(command) ? GB_OK : GB_ERROR_QUEUE_FULL;
}

gb_result SoundboardMixer::Stop(const std::uint64_t key) noexcept
{
    if (key == 0U) {
        return GB_ERROR_INVALID_ARGUMENT;
    }
    const Command command{CommandType::Stop, nullptr, key, 1.0F, false, true};
    return Enqueue(command) ? GB_OK : GB_ERROR_QUEUE_FULL;
}

gb_result SoundboardMixer::StopAll() noexcept
{
    const Command command{CommandType::StopAll, nullptr, 0U, 1.0F, false, true};
    return Enqueue(command) ? GB_OK : GB_ERROR_QUEUE_FULL;
}

bool SoundboardMixer::Enqueue(const Command& command) noexcept
{
    const std::size_t write = command_write_.load(std::memory_order_relaxed);
    const std::size_t next = (write + 1U) % CommandCapacity;
    if (next == command_read_.load(std::memory_order_acquire)) {
        return false;
    }
    commands_[write] = command;
    command_write_.store(next, std::memory_order_release);
    return true;
}

void SoundboardMixer::DrainCommands() noexcept
{
    std::size_t read = command_read_.load(std::memory_order_relaxed);
    const std::size_t write = command_write_.load(std::memory_order_acquire);
    while (read != write) {
        ApplyCommand(commands_[read]);
        read = (read + 1U) % CommandCapacity;
    }
    command_read_.store(read, std::memory_order_release);
}

void SoundboardMixer::ApplyCommand(const Command& command) noexcept
{
    if (command.type == CommandType::StopAll) {
        for (Voice& voice : voices_) {
            voice = {};
        }
        RefreshActiveVoiceCount();
        return;
    }

    if (command.type == CommandType::Stop || command.restart) {
        for (Voice& voice : voices_) {
            if (voice.clip != nullptr && voice.clip->key == command.key) {
                voice = {};
            }
        }
    }
    if (command.type == CommandType::Stop) {
        RefreshActiveVoiceCount();
        return;
    }

    const auto available = std::find_if(voices_.begin(), voices_.end(), [](const Voice& voice) {
        return voice.clip == nullptr;
    });
    if (available != voices_.end()) {
        *available = Voice{command.clip, 0U, command.volume, command.loop};
    }
    RefreshActiveVoiceCount();
}

void SoundboardMixer::MixFrame(float& left, float& right) noexcept
{
    DrainCommands();
    left = 0.0F;
    right = 0.0F;
    bool voiceEnded = false;

    for (Voice& voice : voices_) {
        if (voice.clip == nullptr) {
            continue;
        }

        if (voice.position >= voice.clip->frame_count) {
            if (voice.loop) {
                voice.position = 0U;
            }
            else {
                voice = {};
                voiceEnded = true;
                continue;
            }
        }

        const std::size_t sampleIndex = static_cast<std::size_t>(voice.position * 2U);
        left += voice.clip->samples[sampleIndex] * voice.volume;
        right += voice.clip->samples[sampleIndex + 1U] * voice.volume;
        ++voice.position;
    }

    if (voiceEnded) {
        RefreshActiveVoiceCount();
    }
}

void SoundboardMixer::ResetPlayback() noexcept
{
    for (Voice& voice : voices_) {
        voice = {};
    }
    const std::size_t write = command_write_.load(std::memory_order_acquire);
    command_read_.store(write, std::memory_order_release);
    active_voice_count_.store(0U, std::memory_order_release);
}

std::uint32_t SoundboardMixer::ActiveVoiceCount() const noexcept
{
    return active_voice_count_.load(std::memory_order_relaxed);
}

void SoundboardMixer::RefreshActiveVoiceCount() noexcept
{
    const auto count = static_cast<std::uint32_t>(std::count_if(
        voices_.begin(), voices_.end(), [](const Voice& voice) { return voice.clip != nullptr; }));
    active_voice_count_.store(count, std::memory_order_relaxed);
}

}
