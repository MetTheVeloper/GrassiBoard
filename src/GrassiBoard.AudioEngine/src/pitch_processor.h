#pragma once

#include <signalsmith-stretch.h>

#include <atomic>
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
    virtual void SetFormant(float semitones) noexcept = 0;
    virtual void SetQualityMode(PitchQualityMode mode) noexcept = 0;
    virtual void SetBypass(bool bypass) noexcept = 0;
    virtual std::uint32_t GetLatencySamples() const noexcept = 0;
};

class SignalsmithPitchProcessor final : public IPitchProcessor {
public:
    SignalsmithPitchProcessor();

    bool Prepare(std::uint32_t sampleRate, std::uint32_t channels, std::uint32_t maximumBlockFrames) override;
    void Reset() override;
    void Process(const float* input, float* output, std::uint32_t frames) noexcept override;
    void SetPitchSemitones(float semitones) noexcept override;
    void SetFormant(float semitones) noexcept override;
    void SetQualityMode(PitchQualityMode mode) noexcept override;
    void SetBypass(bool bypass) noexcept override;
    std::uint32_t GetLatencySamples() const noexcept override;

private:
    signalsmith::stretch::SignalsmithStretch<float> stretch_;
    std::vector<float> dry_delay_;
    std::atomic<float> target_pitch_semitones_{0.0F};
    std::atomic<bool> bypass_{true};
    std::atomic<PitchQualityMode> quality_mode_{PitchQualityMode::Balanced};
    std::uint32_t sample_rate_ = 0;
    std::uint32_t latency_samples_ = 0;
    std::size_t dry_delay_index_ = 0;
    float current_pitch_semitones_ = 0.0F;
    float wet_mix_ = 0.0F;
    bool prepared_ = false;
};

}
