#include "pitch_processor.h"

#include <algorithm>
#include <cmath>
#include <cstring>

namespace grassiboard {
namespace {
constexpr std::uint32_t kAutomationChunkFrames = 64U;
constexpr float kParameterSmoothingSeconds = 0.025F;
constexpr float kBypassCrossfadeSeconds = 0.010F;
constexpr float kQualityCrossfadeSeconds = 0.020F;
constexpr float kFormantBaseHertz = 120.0F;

int ScaleFrames(const std::uint32_t sampleRate, const std::uint32_t framesAt48Khz) noexcept
{
    const std::uint64_t scaled = static_cast<std::uint64_t>(sampleRate) * framesAt48Khz;
    return static_cast<int>(std::max<std::uint64_t>(1U, (scaled + 24'000U) / 48'000U));
}
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
        stretch_.configure(1, ScaleFrames(sampleRate, 1'024U), ScaleFrames(sampleRate, 256U), true);
        break;
    case PitchQualityMode::HighQuality:
        stretch_.presetDefault(1, static_cast<float>(sampleRate), true);
        break;
    case PitchQualityMode::Balanced:
    default:
        stretch_.configure(1, ScaleFrames(sampleRate, 2'048U), ScaleFrames(sampleRate, 512U), true);
        break;
    }

    stretch_.setFormantBase(kFormantBaseHertz / static_cast<float>(sampleRate));
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
    current_formant_semitones_ = target_formant_semitones_.load(std::memory_order_acquire);
    current_preservation_mix_ = preserve_formants_.load(std::memory_order_acquire) ? 1.0F : 0.0F;
    wet_mix_ = bypass_.load(std::memory_order_acquire) ? 0.0F : 1.0F;
    stretch_.setTransposeSemitones(current_pitch_semitones_);
    stretch_.setFormantSemitones(
        current_formant_semitones_ - current_pitch_semitones_ * current_preservation_mix_, false);
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
        const float smoothingSamples = std::max(
            1.0F, kParameterSmoothingSeconds * static_cast<float>(sample_rate_));
        const float alpha = 1.0F - std::exp(-static_cast<float>(chunkFrames) / smoothingSamples);
        const float targetPitch = target_pitch_semitones_.load(std::memory_order_relaxed);
        const float targetFormant = target_formant_semitones_.load(std::memory_order_relaxed);
        const float targetPreservation = preserve_formants_.load(std::memory_order_relaxed) ? 1.0F : 0.0F;
        current_pitch_semitones_ += (targetPitch - current_pitch_semitones_) * alpha;
        current_formant_semitones_ += (targetFormant - current_formant_semitones_) * alpha;
        current_preservation_mix_ += (targetPreservation - current_preservation_mix_) * alpha;
        stretch_.setTransposeSemitones(current_pitch_semitones_);
        stretch_.setFormantSemitones(
            current_formant_semitones_ - current_pitch_semitones_ * current_preservation_mix_, false);

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

void SignalsmithPitchProcessor::SetFormantSemitones(const float semitones) noexcept
{
    const float safeSemitones = std::isfinite(semitones) ? semitones : 0.0F;
    target_formant_semitones_.store(std::clamp(safeSemitones, -12.0F, 12.0F), std::memory_order_release);
}

void SignalsmithPitchProcessor::SetFormantPreservation(const bool preserve) noexcept
{
    preserve_formants_.store(preserve, std::memory_order_release);
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

std::size_t LivePitchProcessor::ModeIndex(const PitchQualityMode mode) noexcept
{
    return static_cast<std::size_t>(SanitizeMode(mode));
}

PitchQualityMode LivePitchProcessor::SanitizeMode(const PitchQualityMode mode) noexcept
{
    switch (mode) {
    case PitchQualityMode::LowLatency:
    case PitchQualityMode::Balanced:
    case PitchQualityMode::HighQuality:
        return mode;
    default:
        return PitchQualityMode::Balanced;
    }
}

bool LivePitchProcessor::Prepare(
    const std::uint32_t sampleRate,
    const std::uint32_t channels,
    const std::uint32_t maximumBlockFrames)
{
    if (sampleRate == 0U || channels != 1U || maximumBlockFrames == 0U) {
        return false;
    }

    sample_rate_ = sampleRate;
    for (std::size_t index = 0U; index < kModeCount; ++index) {
        auto& processor = processors_[index];
        processor.SetQualityMode(static_cast<PitchQualityMode>(index));
        processor.SetPitchSemitones(pitch_semitones_.load(std::memory_order_acquire));
        processor.SetFormantSemitones(formant_semitones_.load(std::memory_order_acquire));
        processor.SetFormantPreservation(preserve_formants_.load(std::memory_order_acquire));
        processor.SetBypass(bypass_.load(std::memory_order_acquire));
        if (!processor.Prepare(sampleRate, channels, maximumBlockFrames)) {
            return false;
        }
        mode_outputs_[index].assign(maximumBlockFrames, 0.0F);
    }

    prepared_ = true;
    Reset();
    return true;
}

void LivePitchProcessor::Reset()
{
    if (!prepared_) {
        return;
    }
    for (auto& processor : processors_) {
        processor.Reset();
    }
    active_mode_index_ = ModeIndex(requested_mode_.load(std::memory_order_acquire));
    transition_source_index_ = active_mode_index_;
    transition_target_index_ = active_mode_index_;
    transition_mix_ = 1.0F;
    reported_latency_samples_.store(
        processors_[active_mode_index_].GetLatencySamples(), std::memory_order_release);
}

void LivePitchProcessor::Process(
    const float* const input,
    float* const output,
    const std::uint32_t frames) noexcept
{
    if (input == nullptr || output == nullptr || frames == 0U) {
        return;
    }
    if (!prepared_ || frames > mode_outputs_[0].size()) {
        std::memcpy(output, input, static_cast<std::size_t>(frames) * sizeof(float));
        return;
    }

    for (std::size_t index = 0U; index < kModeCount; ++index) {
        processors_[index].Process(input, mode_outputs_[index].data(), frames);
    }

    const std::size_t requestedIndex = ModeIndex(requested_mode_.load(std::memory_order_relaxed));
    if (transition_mix_ >= 1.0F && requestedIndex != active_mode_index_) {
        transition_source_index_ = active_mode_index_;
        transition_target_index_ = requestedIndex;
        transition_mix_ = 0.0F;
    }
    else if (transition_mix_ < 1.0F && requestedIndex != transition_target_index_) {
        transition_source_index_ = transition_mix_ >= 0.5F
            ? transition_target_index_
            : transition_source_index_;
        transition_target_index_ = requestedIndex;
        transition_mix_ = 0.0F;
    }

    const float transitionStep = 1.0F /
        std::max(1.0F, kQualityCrossfadeSeconds * static_cast<float>(sample_rate_));
    for (std::uint32_t frame = 0U; frame < frames; ++frame) {
        if (transition_mix_ < 1.0F) {
            transition_mix_ = std::min(1.0F, transition_mix_ + transitionStep);
            const float source = mode_outputs_[transition_source_index_][frame];
            const float target = mode_outputs_[transition_target_index_][frame];
            output[frame] = source + (target - source) * transition_mix_;
        }
        else {
            output[frame] = mode_outputs_[transition_target_index_][frame];
        }
    }

    if (transition_mix_ >= 1.0F && active_mode_index_ != transition_target_index_) {
        active_mode_index_ = transition_target_index_;
        transition_source_index_ = active_mode_index_;
        reported_latency_samples_.store(
            processors_[active_mode_index_].GetLatencySamples(), std::memory_order_release);
    }
}

void LivePitchProcessor::SetPitchSemitones(const float semitones) noexcept
{
    const float safeSemitones = std::isfinite(semitones) ? semitones : 0.0F;
    const float clamped = std::clamp(safeSemitones, -12.0F, 12.0F);
    pitch_semitones_.store(clamped, std::memory_order_release);
    for (auto& processor : processors_) {
        processor.SetPitchSemitones(clamped);
    }
}

void LivePitchProcessor::SetFormantSemitones(const float semitones) noexcept
{
    const float safeSemitones = std::isfinite(semitones) ? semitones : 0.0F;
    const float clamped = std::clamp(safeSemitones, -12.0F, 12.0F);
    formant_semitones_.store(clamped, std::memory_order_release);
    for (auto& processor : processors_) {
        processor.SetFormantSemitones(clamped);
    }
}

void LivePitchProcessor::SetFormantPreservation(const bool preserve) noexcept
{
    preserve_formants_.store(preserve, std::memory_order_release);
    for (auto& processor : processors_) {
        processor.SetFormantPreservation(preserve);
    }
}

void LivePitchProcessor::SetQualityMode(const PitchQualityMode mode) noexcept
{
    requested_mode_.store(SanitizeMode(mode), std::memory_order_release);
}

void LivePitchProcessor::SetBypass(const bool bypass) noexcept
{
    bypass_.store(bypass, std::memory_order_release);
    for (auto& processor : processors_) {
        processor.SetBypass(bypass);
    }
}

std::uint32_t LivePitchProcessor::GetLatencySamples() const noexcept
{
    return reported_latency_samples_.load(std::memory_order_acquire);
}

}
