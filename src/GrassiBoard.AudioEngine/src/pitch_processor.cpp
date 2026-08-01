#include "pitch_processor.h"

#include <algorithm>
#include <cmath>
#include <cstring>

namespace grassiboard {
namespace {
constexpr std::uint32_t kAutomationChunkFrames = 64U;
constexpr float kPitchSmoothingSeconds = 0.025F;
constexpr float kBypassCrossfadeSeconds = 0.010F;
}

SignalsmithPitchProcessor::SignalsmithPitchProcessor()
    : stretch_(0L)
{
}

bool SignalsmithPitchProcessor::Prepare(
    const std::uint32_t sampleRate,
    const std::uint32_t channels,
    const std::uint32_t maximumBlockFrames)
{
    if (sampleRate == 0U || channels != 1U || maximumBlockFrames == 0U) {
        return false;
    }

    sample_rate_ = sampleRate;
    switch (quality_mode_.load(std::memory_order_acquire)) {
    case PitchQualityMode::LowLatency:
        stretch_.configure(
            1,
            static_cast<int>(sampleRate * 0.021333333F),
            static_cast<int>(sampleRate * 0.005333333F),
            true);
        break;
    case PitchQualityMode::HighQuality:
        stretch_.presetDefault(1, static_cast<float>(sampleRate), false);
        break;
    case PitchQualityMode::Balanced:
    default:
        stretch_.configure(
            1,
            static_cast<int>(sampleRate * 0.042666667F),
            static_cast<int>(sampleRate * 0.010666667F),
            true);
        break;
    }

    const int totalLatency = stretch_.inputLatency() + stretch_.outputLatency();
    if (totalLatency < 0) {
        return false;
    }
    latency_samples_ = static_cast<std::uint32_t>(totalLatency);
    dry_delay_.assign(std::max<std::size_t>(latency_samples_, 1U), 0.0F);
    prepared_ = true;
    Reset();
    return true;
}

void SignalsmithPitchProcessor::Reset()
{
    if (!prepared_) {
        return;
    }
    stretch_.reset();
    std::fill(dry_delay_.begin(), dry_delay_.end(), 0.0F);
    dry_delay_index_ = 0U;
    current_pitch_semitones_ = target_pitch_semitones_.load(std::memory_order_acquire);
    wet_mix_ = bypass_.load(std::memory_order_acquire) ? 0.0F : 1.0F;
    stretch_.setTransposeSemitones(current_pitch_semitones_);
}

void SignalsmithPitchProcessor::Process(
    const float* const input,
    float* const output,
    const std::uint32_t frames) noexcept
{
    if (input == nullptr || output == nullptr || frames == 0U) {
        return;
    }
    if (!prepared_) {
        std::memcpy(output, input, static_cast<std::size_t>(frames) * sizeof(float));
        return;
    }

    std::uint32_t offset = 0U;
    while (offset < frames) {
        const std::uint32_t chunkFrames = std::min(kAutomationChunkFrames, frames - offset);
        const float targetPitch = target_pitch_semitones_.load(std::memory_order_relaxed);
        const float smoothingSamples = std::max(1.0F, kPitchSmoothingSeconds * static_cast<float>(sample_rate_));
        const float alpha = 1.0F - std::exp(-static_cast<float>(chunkFrames) / smoothingSamples);
        current_pitch_semitones_ += (targetPitch - current_pitch_semitones_) * alpha;
        stretch_.setTransposeSemitones(current_pitch_semitones_);

        const float* inputChannels[1]{input + offset};
        float* outputChannels[1]{output + offset};
        stretch_.process(inputChannels, static_cast<int>(chunkFrames), outputChannels, static_cast<int>(chunkFrames));

        const float targetWet = bypass_.load(std::memory_order_relaxed) ? 0.0F : 1.0F;
        const float wetStep = 1.0F /
            std::max(1.0F, kBypassCrossfadeSeconds * static_cast<float>(sample_rate_));
        for (std::uint32_t frame = 0U; frame < chunkFrames; ++frame) {
            float drySample = input[offset + frame];
            if (latency_samples_ > 0U) {
                drySample = dry_delay_[dry_delay_index_];
                dry_delay_[dry_delay_index_] = input[offset + frame];
                dry_delay_index_ = (dry_delay_index_ + 1U) % latency_samples_;
            }

            if (wet_mix_ < targetWet) {
                wet_mix_ = std::min(targetWet, wet_mix_ + wetStep);
            }
            else if (wet_mix_ > targetWet) {
                wet_mix_ = std::max(targetWet, wet_mix_ - wetStep);
            }
            output[offset + frame] = output[offset + frame] * wet_mix_ + drySample * (1.0F - wet_mix_);
        }

        offset += chunkFrames;
    }
}

void SignalsmithPitchProcessor::SetPitchSemitones(const float semitones) noexcept
{
    const float safeSemitones = std::isfinite(semitones) ? semitones : 0.0F;
    target_pitch_semitones_.store(std::clamp(safeSemitones, -12.0F, 12.0F), std::memory_order_release);
}

void SignalsmithPitchProcessor::SetFormant(const float semitones) noexcept
{
    static_cast<void>(semitones);
    // Formant processing belongs to Milestone 3.
}

void SignalsmithPitchProcessor::SetQualityMode(const PitchQualityMode mode) noexcept
{
    quality_mode_.store(mode, std::memory_order_release);
}

void SignalsmithPitchProcessor::SetBypass(const bool bypass) noexcept
{
    bypass_.store(bypass, std::memory_order_release);
}

std::uint32_t SignalsmithPitchProcessor::GetLatencySamples() const noexcept
{
    return latency_samples_;
}

}
