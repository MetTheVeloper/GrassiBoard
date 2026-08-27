#include "mixer_processor.h"

#include "looper_engine.h"

#include <algorithm>
#include <cmath>

namespace grassiboard {
namespace {
constexpr float kMinimumLinear = 1.0e-9F;

float TimeCoefficient(const float seconds, const std::uint32_t sampleRate) noexcept
{
    return 1.0F - std::exp(-1.0F / std::max(1.0F, seconds * static_cast<float>(sampleRate)));
}

float FiniteOr(const float value, const float fallback) noexcept
{
    return std::isfinite(value) ? value : fallback;
}
}

void MixerDynamicsProcessor::Prepare(const std::uint32_t sampleRate) noexcept
{
    sample_rate_ = sampleRate == 0U ? 48'000U : sampleRate;
    gain_smoothing_ = TimeCoefficient(0.020F, sample_rate_);
    envelope_attack_ = TimeCoefficient(0.005F, sample_rate_);
    envelope_release_ = TimeCoefficient(0.120F, sample_rate_);
    dynamics_attack_ = TimeCoefficient(0.008F, sample_rate_);
    dynamics_release_ = TimeCoefficient(0.120F, sample_rate_);
    BeginBlock();
    Reset();
}

void MixerDynamicsProcessor::Reset() noexcept
{
    mic_gain_ = target_mic_gain_;
    board_gain_ = target_board_gain_;
    master_gain_ = target_master_gain_;
    microphone_envelope_ = 0.0F;
    gate_gain_ = 1.0F;
    compressor_gain_ = 1.0F;
    duck_gain_ = 1.0F;
    limiter_gain_ = 1.0F;
}

void MixerDynamicsProcessor::BeginBlock() noexcept
{
    target_mic_gain_ = DbToLinear(mic_gain_db_.load(std::memory_order_relaxed));
    target_board_gain_ = DbToLinear(soundboard_gain_db_.load(std::memory_order_relaxed));
    target_master_gain_ = DbToLinear(master_gain_db_.load(std::memory_order_relaxed));
    gate_threshold_ = DbToLinear(gate_threshold_db_.load(std::memory_order_relaxed));
    compressor_threshold_db_block_ = compressor_threshold_db_.load(std::memory_order_relaxed);
    compressor_ratio_block_ = compressor_ratio_.load(std::memory_order_relaxed);
    limiter_ceiling_ = DbToLinear(limiter_ceiling_db_.load(std::memory_order_relaxed));
    ducking_gain_ = DbToLinear(-ducking_amount_db_.load(std::memory_order_relaxed));
    gate_enabled_block_ = gate_enabled_.load(std::memory_order_relaxed);
    compressor_enabled_block_ = compressor_enabled_.load(std::memory_order_relaxed);
    limiter_enabled_block_ = limiter_enabled_.load(std::memory_order_relaxed);
    ducking_enabled_block_ = ducking_enabled_.load(std::memory_order_relaxed);
    clipping_protection_enabled_block_ = clipping_protection_enabled_.load(std::memory_order_relaxed);
}

MixerFrame MixerDynamicsProcessor::ProcessFrame(
    const float microphone,
    const float boardLeft,
    const float boardRight,
    const float mediaLeft,
    const float mediaRight) noexcept
{
    // This is the one per-render-frame hook for GrassiLooper. It advances the
    // native shared sample clock and writes only to the dedicated Looper monitor
    // ring; no Looper sample is ever added to Program/VB-CABLE here.
    if (looper_clock_ != nullptr) looper_clock_->RenderFrame();

    mic_gain_ = Smooth(mic_gain_, target_mic_gain_, gain_smoothing_);
    board_gain_ = Smooth(board_gain_, target_board_gain_, gain_smoothing_);
    master_gain_ = Smooth(master_gain_, target_master_gain_, gain_smoothing_);

    float mic = FiniteOr(microphone, 0.0F) * mic_gain_;
    const float absoluteMic = std::abs(mic);
    microphone_envelope_ = Smooth(
        microphone_envelope_, absoluteMic,
        absoluteMic > microphone_envelope_ ? envelope_attack_ : envelope_release_);

    const float targetGate = !gate_enabled_block_ || microphone_envelope_ >= gate_threshold_ ? 1.0F : 0.0F;
    gate_gain_ = Smooth(
        gate_gain_, targetGate, targetGate > gate_gain_ ? dynamics_attack_ : dynamics_release_);
    mic *= gate_gain_;

    float targetCompressor = 1.0F;
    if (compressor_enabled_block_ && microphone_envelope_ > kMinimumLinear) {
        const float inputDb = 20.0F * std::log10(microphone_envelope_);
        if (inputDb > compressor_threshold_db_block_) {
            const float outputDb = compressor_threshold_db_block_ +
                (inputDb - compressor_threshold_db_block_) / compressor_ratio_block_;
            targetCompressor = DbToLinear(outputDb - inputDb);
        }
    }
    compressor_gain_ = Smooth(
        compressor_gain_, targetCompressor,
        targetCompressor < compressor_gain_ ? dynamics_attack_ : dynamics_release_);
    mic *= compressor_gain_;

    const float duckThreshold = DbToLinear(-42.0F);
    const float targetDuck = ducking_enabled_block_ && microphone_envelope_ >= duckThreshold
        ? ducking_gain_
        : 1.0F;
    duck_gain_ = Smooth(
        duck_gain_, targetDuck,
        targetDuck < duck_gain_ ? dynamics_attack_ : dynamics_release_);

    const float processedBoardLeft = FiniteOr(boardLeft, 0.0F) * board_gain_ * duck_gain_;
    const float processedBoardRight = FiniteOr(boardRight, 0.0F) * board_gain_ * duck_gain_;
    const float processedMediaLeft = FiniteOr(mediaLeft, 0.0F);
    const float processedMediaRight = FiniteOr(mediaRight, 0.0F);
    float left = (mic + processedBoardLeft + processedMediaLeft) * master_gain_;
    float right = (mic + processedBoardRight + processedMediaRight) * master_gain_;

    const float peak = std::max(std::abs(left), std::abs(right));
    const float targetLimiter = limiter_enabled_block_ && peak > limiter_ceiling_
        ? limiter_ceiling_ / std::max(peak, kMinimumLinear)
        : 1.0F;
    limiter_gain_ = targetLimiter < limiter_gain_
        ? targetLimiter
        : Smooth(limiter_gain_, targetLimiter, dynamics_release_);
    left *= limiter_gain_;
    right *= limiter_gain_;

    if (clipping_protection_enabled_block_) {
        left = SoftProtect(left);
        right = SoftProtect(right);
    }

    return {mic, processedBoardLeft, processedBoardRight,
        processedMediaLeft, processedMediaRight, left, right};
}

void MixerDynamicsProcessor::SetMicGainDb(const float value) noexcept
{
    mic_gain_db_.store(std::clamp(FiniteOr(value, 0.0F), -24.0F, 24.0F), std::memory_order_release);
}

void MixerDynamicsProcessor::SetSoundboardGainDb(const float value) noexcept
{
    soundboard_gain_db_.store(std::clamp(FiniteOr(value, 0.0F), -24.0F, 24.0F), std::memory_order_release);
}

void MixerDynamicsProcessor::SetMasterGainDb(const float value) noexcept
{
    master_gain_db_.store(std::clamp(FiniteOr(value, 0.0F), -24.0F, 12.0F), std::memory_order_release);
}

void MixerDynamicsProcessor::SetNoiseGate(const bool enabled, const float thresholdDb) noexcept
{
    gate_enabled_.store(enabled, std::memory_order_release);
    gate_threshold_db_.store(std::clamp(FiniteOr(thresholdDb, -55.0F), -80.0F, -20.0F), std::memory_order_release);
}

void MixerDynamicsProcessor::SetCompressor(
    const bool enabled,
    const float thresholdDb,
    const float ratio) noexcept
{
    compressor_enabled_.store(enabled, std::memory_order_release);
    compressor_threshold_db_.store(
        std::clamp(FiniteOr(thresholdDb, -18.0F), -40.0F, -3.0F), std::memory_order_release);
    compressor_ratio_.store(std::clamp(FiniteOr(ratio, 3.0F), 1.0F, 20.0F), std::memory_order_release);
}

void MixerDynamicsProcessor::SetLimiter(const bool enabled, const float ceilingDb) noexcept
{
    limiter_enabled_.store(enabled, std::memory_order_release);
    limiter_ceiling_db_.store(std::clamp(FiniteOr(ceilingDb, -1.0F), -12.0F, 0.0F), std::memory_order_release);
}

void MixerDynamicsProcessor::SetDucking(const bool enabled, const float amountDb) noexcept
{
    ducking_enabled_.store(enabled, std::memory_order_release);
    ducking_amount_db_.store(std::clamp(FiniteOr(amountDb, 9.0F), 0.0F, 30.0F), std::memory_order_release);
}

void MixerDynamicsProcessor::SetClippingProtection(const bool enabled) noexcept
{
    clipping_protection_enabled_.store(enabled, std::memory_order_release);
}

float MixerDynamicsProcessor::DbToLinear(const float db) noexcept
{
    return std::pow(10.0F, FiniteOr(db, 0.0F) / 20.0F);
}

float MixerDynamicsProcessor::Smooth(
    const float current,
    const float target,
    const float coefficient) noexcept
{
    return current + (target - current) * coefficient;
}

float MixerDynamicsProcessor::SoftProtect(const float sample) noexcept
{
    const float safe = FiniteOr(sample, 0.0F);
    const float magnitude = std::abs(safe);
    if (magnitude <= 0.85F) {
        return safe;
    }
    const float protectedMagnitude = 0.85F + 0.15F * std::tanh((magnitude - 0.85F) / 0.15F);
    return std::copysign(protectedMagnitude, safe);
}

}
