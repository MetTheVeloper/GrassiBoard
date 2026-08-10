#pragma once

#include <signalsmith-stretch/signalsmith-stretch.h>

#include <array>
#include <atomic>
#include <cstddef>
#include <cstdint>
#include <vector>

namespace grassiboard {

enum class PitchQualityMode : std::uint32_t {
    LowLatency = 0,
    Balanced = 1,
    HighQuality = 2
};

class IPitchProcessor {
public:
    virtual ~IPitchProcessor() = default;

    virtual bool Prepare(std::uint32_t sampleRate, std::uint32_t channels, std::uint32_t maximumBlockFrames) = 0;
    virtual void Reset() = 0;
    virtual void Process(const float* input, float* output, std::uint32_t frames) noexcept = 0;
    virtual void SetPitchSemitones(float semitones) noexcept = 0;
    virtual void SetFormantSemitones(float semitones) noexcept = 0;
    virtual void SetFormantPreservation(bool preserve) noexcept = 0;
    virtual void SetQualityMode(PitchQualityMode mode) noexcept = 0;
    virtual void SetBypass(bool bypass) noexcept = 0;
    virtual void SetWetDryMix(float wetMix) noexcept = 0;
    virtual std::uint32_t GetLatencySamples() const noexcept = 0;
};

class SignalsmithPitchProcessor final : public IPitchProcessor {
public:
    SignalsmithPitchProcessor();

    bool Prepare(std::uint32_t sampleRate, std::uint32_t channels, std::uint32_t maximumBlockFrames) override;
    void Reset() override;
    void Process(const float* input, float* output, std::uint32_t frames) noexcept override;
    void SetPitchSemitones(float semitones) noexcept override;
    void SetFormantSemitones(float semitones) noexcept override;
    void SetFormantPreservation(bool preserve) noexcept override;
    void SetQualityMode(PitchQualityMode mode) noexcept override;
    void SetBypass(bool bypass) noexcept override;
    void SetWetDryMix(float wetMix) noexcept override;
    std::uint32_t GetLatencySamples() const noexcept override;

private:
    signalsmith::stretch::SignalsmithStretch<float> stretch_;
    std::vector<float> dry_delay_;
    std::atomic<float> target_pitch_semitones_{0.0F};
    std::atomic<float> target_formant_semitones_{0.0F};
    std::atomic<bool> preserve_formants_{true};
    std::atomic<bool> bypass_{true};
    std::atomic<float> target_wet_mix_{1.0F};
    std::atomic<PitchQualityMode> quality_mode_{PitchQualityMode::Balanced};
    std::uint32_t sample_rate_ = 0;
    std::uint32_t latency_samples_ = 0;
    std::size_t dry_delay_index_ = 0;
    float current_pitch_semitones_ = 0.0F;
    float current_formant_semitones_ = 0.0F;
    float current_preservation_mix_ = 1.0F;
    float wet_mix_ = 0.0F;
    bool prepared_ = false;
};

class LivePitchProcessor final : public IPitchProcessor {
public:
    bool Prepare(std::uint32_t sampleRate, std::uint32_t channels, std::uint32_t maximumBlockFrames) override;
    void Reset() override;
    void Process(const float* input, float* output, std::uint32_t frames) noexcept override;
    void SetPitchSemitones(float semitones) noexcept override;
    void SetFormantSemitones(float semitones) noexcept override;
    void SetFormantPreservation(bool preserve) noexcept override;
    void SetQualityMode(PitchQualityMode mode) noexcept override;
    void SetBypass(bool bypass) noexcept override;
    void SetWetDryMix(float wetMix) noexcept override;
    std::uint32_t GetLatencySamples() const noexcept override;

private:
    static constexpr std::size_t kModeCount = 3U;
    static std::size_t ModeIndex(PitchQualityMode mode) noexcept;
    static PitchQualityMode SanitizeMode(PitchQualityMode mode) noexcept;

    std::array<SignalsmithPitchProcessor, kModeCount> processors_;
    std::array<std::vector<float>, kModeCount> mode_outputs_;
    std::atomic<float> pitch_semitones_{0.0F};
    std::atomic<float> formant_semitones_{0.0F};
    std::atomic<bool> preserve_formants_{true};
    std::atomic<bool> bypass_{true};
    std::atomic<float> wet_mix_{1.0F};
    std::atomic<PitchQualityMode> requested_mode_{PitchQualityMode::Balanced};
    std::atomic<std::uint32_t> reported_latency_samples_{0U};
    std::uint32_t sample_rate_ = 0U;
    std::size_t active_mode_index_ = ModeIndex(PitchQualityMode::Balanced);
    std::size_t transition_source_index_ = ModeIndex(PitchQualityMode::Balanced);
    std::size_t transition_target_index_ = ModeIndex(PitchQualityMode::Balanced);
    float transition_mix_ = 1.0F;
    bool prepared_ = false;
};

}
