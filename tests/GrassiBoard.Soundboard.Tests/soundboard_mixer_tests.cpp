#include "soundboard_mixer.h"

#include <array>
#include <cmath>
#include <cstdint>
#include <iostream>

namespace {
bool Near(const float actual, const float expected) noexcept
{
    return std::abs(actual - expected) < 0.0001F;
}
}

int main()
{
    grassiboard::SoundboardMixer mixer;
    constexpr std::uint64_t firstKey = 101U;
    constexpr std::uint64_t secondKey = 202U;
    constexpr std::array<float, 6> first{0.25F, -0.25F, 0.5F, -0.5F, 0.75F, -0.75F};
    constexpr std::array<float, 4> second{0.1F, 0.2F, 0.3F, 0.4F};

    if (mixer.LoadClip(firstKey, first.data(), 3U) != GB_OK ||
        mixer.LoadClip(secondKey, second.data(), 2U) != GB_OK ||
        mixer.LoadClip(0U, first.data(), 3U) != GB_ERROR_INVALID_ARGUMENT) {
        std::cerr << "Clip loading contract failed.\n";
        return 1;
    }

    if (mixer.Play(firstKey, 0.5F, false, true) != GB_OK) {
        std::cerr << "One-shot enqueue failed.\n";
        return 2;
    }
    float left = 0.0F;
    float right = 0.0F;
    mixer.MixFrame(left, right);
    if (!Near(left, 0.125F) || !Near(right, -0.125F) || mixer.ActiveVoiceCount() != 1U) {
        std::cerr << "One-shot first frame failed.\n";
        return 3;
    }
    mixer.MixFrame(left, right);
    mixer.MixFrame(left, right);
    mixer.MixFrame(left, right);
    if (!Near(left, 0.0F) || !Near(right, 0.0F) || mixer.ActiveVoiceCount() != 0U) {
        std::cerr << "One-shot completion failed.\n";
        return 4;
    }

    if (mixer.Play(firstKey, 1.0F, true, true) != GB_OK ||
        mixer.Play(secondKey, 1.0F, false, true) != GB_OK) {
        std::cerr << "Simultaneous enqueue failed.\n";
        return 5;
    }
    mixer.MixFrame(left, right);
    if (!Near(left, 0.35F) || !Near(right, -0.05F) || mixer.ActiveVoiceCount() != 2U) {
        std::cerr << "Simultaneous mix failed.\n";
        return 6;
    }

    if (mixer.Stop(firstKey) != GB_OK) {
        std::cerr << "Per-pad stop enqueue failed.\n";
        return 7;
    }
    mixer.MixFrame(left, right);
    if (!Near(left, 0.3F) || !Near(right, 0.4F) || mixer.ActiveVoiceCount() != 1U) {
        std::cerr << "Per-pad stop failed.\n";
        return 8;
    }

    if (mixer.StopAll() != GB_OK) {
        std::cerr << "Stop All enqueue failed.\n";
        return 9;
    }
    mixer.MixFrame(left, right);
    if (!Near(left, 0.0F) || !Near(right, 0.0F) || mixer.ActiveVoiceCount() != 0U) {
        std::cerr << "Stop All failed.\n";
        return 10;
    }

    std::cout << "Soundboard mixer contract passed.\n";
    return 0;
}
