#pragma once

#include <atomic>
#include <cstdint>

namespace grassiboard {

class LooperEngine;

struct MixerFrame final {
    float microphone = 0.0F;
    float board_left = 0.0F;
    float board_right = 0.0F;
    float media_left = 0.0F;
    float media_right = 0.0F;
    float left = 0.0F;
    float right = 0.0F;
};

class MixerDynamicsProcessor final {
public:
    explicit MixerDynamicsProcessor(LooperEngine* looperClock = nullptr) noexcept
        : looper_clock_(looperClock) {}

    void Prepare(std::uint32_t sampleRate) noexcept;
    void Reset() noexcept;
    void BeginBlock() noexcept;
    MixerFrame ProcessFrame(
        float microphone,
        float boardLeft,
        float boardRight,
        float mediaLeft = 0.0F,
        float mediaRight = 0.0F) noexcept;

    void SetMicGainDb(float value) noexcept;
    void SetSoundboardGainDb(float value) noexcept;
    void SetMasterGainDb(float value) noexcept;
    void SetNoiseGate(bool enabled, float thresholdDb) noexcept;
    void SetCompressor(bool enabled, float thresholdDb, float ratio) noexcept;
    void SetLimiter(bool enabled, float ceilingDb) noexcept;
    void SetDucking(bool enabled, float amountDb) noexcept;
    void SetClippingProtection(bool enabled) noexcept;

private:
    static float DbToLinear(float db) noexcept;
    static float Smooth(float current, float target, float coefficient) noexcept;
    static float SoftProtect(float sample) noexcept;

    // Non-owning Gate-2 clock hook. ProcessFrame is invoked exactly once for every
    // Program render frame. LooperEngine only advances its own clock/tap here; its
    // PCM is never added to this Program mixer output.
    LooperEngine* looper_clock_ = nullptr;

    std::atomic<float> mic_gain_db_{0.0F};
    std::atomic<float> soundboard_gain_db_{0.0F};
    std::atomic<float> master_gain_db_{0.0F};
    std::atomic<float> gate_threshold_db_{-55.0F};
    std::atomic<float> compressor_threshold_db_{-18.0F};
    std::atomic<float> compressor_ratio_{3.0F};
    std::atomic<float> limiter_ceiling_db_{-1.0F};
    std::atomic<float> ducking_amount_db_{9.0F};
    std::atomic<bool> gate_enabled_{false};
    std::atomic<bool> compressor_enabled_{false};
    std::atomic<bool> limiter_enabled_{true};
    std::atomic<bool> ducking_enabled_{false};
    std::atomic<bool> clipping_protection_enabled_{true};

    std::uint32_t sample_rate_ = 48'000U;
    float gain_smoothing_ = 0.0F;
    float envelope_attack_ = 0.0F;
    float envelope_release_ = 0.0F;
    float dynamics_attack_ = 0.0F;
    float dynamics_release_ = 0.0F;

    float target_mic_gain_ = 1.0F;
    float target_board_gain_ = 1.0F;
    float target_master_gain_ = 1.0F;
    float gate_threshold_ = DbToLinear(-55.0F);
    float compressor_threshold_db_block_ = -18.0F;
    float compressor_ratio_block_ = 3.0F;
    float limiter_ceiling_ = DbToLinear(-1.0F);
    float ducking_gain_ = DbToLinear(-9.0F);
    bool gate_enabled_block_ = false;
    bool compressor_enabled_block_ = false;
    bool limiter_enabled_block_ = true;
    bool ducking_enabled_block_ = false;
    bool clipping_protection_enabled_block_ = true;

    float mic_gain_ = 1.0F;
    float board_gain_ = 1.0F;
    float master_gain_ = 1.0F;
    float microphone_envelope_ = 0.0F;
    float gate_gain_ = 1.0F;
    float compressor_gain_ = 1.0F;
    float duck_gain_ = 1.0F;
    float limiter_gain_ = 1.0F;
};

}
