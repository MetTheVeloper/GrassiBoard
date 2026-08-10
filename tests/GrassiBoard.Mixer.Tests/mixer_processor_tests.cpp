#include "mixer_processor.h"

#include <algorithm>
#include <cmath>
#include <iostream>

namespace {
float PeakAfter(
    grassiboard::MixerDynamicsProcessor& processor,
    const float mic,
    const float board,
    const std::uint32_t frames = 48'000U)
{
    processor.BeginBlock();
    float peak = 0.0F;
    for (std::uint32_t frame = 0U; frame < frames; ++frame) {
        const auto result = processor.ProcessFrame(mic, board, board);
        peak = std::max({peak, std::abs(result.left), std::abs(result.right)});
    }
    return peak;
}

float LastAfter(
    grassiboard::MixerDynamicsProcessor& processor,
    const float mic,
    const float board,
    const std::uint32_t frames = 48'000U)
{
    processor.BeginBlock();
    grassiboard::MixerFrame result{};
    for (std::uint32_t frame = 0U; frame < frames; ++frame) {
        result = processor.ProcessFrame(mic, board, board);
    }
    return std::max(std::abs(result.left), std::abs(result.right));
}
}

int main()
{
    grassiboard::MixerDynamicsProcessor processor;
    processor.Prepare(48'000U);

    const float unity = PeakAfter(processor, 0.2F, 0.1F, 2'000U);
    if (unity < 0.25F || unity > 0.31F) {
        std::cerr << "Unity mix failed: " << unity << '\n';
        return 1;
    }

    processor.SetMicGainDb(6.0F);
    const float gained = PeakAfter(processor, 0.2F, 0.0F, 4'000U);
    if (gained < 0.36F) {
        std::cerr << "Mic gain failed: " << gained << '\n';
        return 2;
    }

    processor.SetMicGainDb(0.0F);
    processor.SetNoiseGate(true, -30.0F);
    processor.Reset();
    const float gated = LastAfter(processor, 0.005F, 0.0F);
    if (gated > 0.001F) {
        std::cerr << "Noise gate failed: " << gated << '\n';
        return 3;
    }

    processor.SetNoiseGate(false, -55.0F);
    processor.SetDucking(true, 12.0F);
    processor.Reset();
    const float ducked = LastAfter(processor, 0.5F, 0.5F);
    if (ducked >= 0.85F) {
        std::cerr << "Ducking failed: " << ducked << '\n';
        return 4;
    }

    processor.SetDucking(false, 0.0F);
    processor.SetCompressor(true, -18.0F, 4.0F);
    processor.SetLimiter(true, -3.0F);
    processor.SetMasterGainDb(12.0F);
    processor.Reset();
    const float limited = PeakAfter(processor, 0.9F, 0.9F);
    const float ceiling = std::pow(10.0F, -3.0F / 20.0F) + 0.01F;
    if (!std::isfinite(limited) || limited > ceiling) {
        std::cerr << "Limiter failed: " << limited << '\n';
        return 5;
    }

    processor.SetLimiter(false, 0.0F);
    processor.SetClippingProtection(true);
    processor.Reset();
    const float protectedPeak = PeakAfter(processor, 10.0F, 10.0F, 64U);
    if (!std::isfinite(protectedPeak) || protectedPeak > 1.0F) {
        std::cerr << "Clipping protection failed: " << protectedPeak << '\n';
        return 6;
    }

    std::cout << "Mixer dynamics contract passed.\n";
    return 0;
}
