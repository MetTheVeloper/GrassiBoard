#include "looper_engine.h"

#include <array>
#include <cmath>
#include <cstdint>
#include <iostream>

namespace {
bool Close(const float left, const float right) noexcept
{
    return std::abs(left - right) < 0.00001F;
}

int Fail(const char* message)
{
    std::cerr << message << '\n';
    return 1;
}
}

int main()
{
    using grassiboard::LooperEngine;

    static_assert(sizeof(gb_looper_state) == 48U, "Looper state ABI layout must remain 48 bytes.");
    static_assert(
        LooperEngine::StereoFloatBytes(LooperEngine::MaxSupportedLoopFrames) == 230'400'000ULL,
        "Ten-minute 48 kHz stereo-float memory benchmark contract failed.");

    LooperEngine looper;
    const std::array<float, 8> master{
        0.10F, -0.10F,
        0.20F, -0.20F,
        0.30F, -0.30F,
        0.40F, -0.40F
    };

    if (looper.LoadMaster(master.data(), 4U) != GB_OK) return Fail("Could not load deterministic Master.");
    if (looper.SetTransport(GB_LOOPER_PLAYING) != GB_OK) return Fail("Play should start with a loaded Master.");

    for (int index = 0; index < 6; ++index) looper.RenderFrame();
    std::array<float, 12> rendered{};
    if (looper.ReadMonitor(rendered.data(), 6U) != 6U) return Fail("Expected six monitor frames.");

    const std::array<float, 12> expected{
        0.10F, -0.10F,
        0.20F, -0.20F,
        0.30F, -0.30F,
        0.40F, -0.40F,
        0.10F, -0.10F,
        0.20F, -0.20F
    };
    for (std::size_t index = 0; index < expected.size(); ++index) {
        if (!Close(rendered[index], expected[index])) return Fail("Master modulo wrap output mismatch.");
    }

    gb_looper_state state{};
    looper.GetState(state);
    if (state.transport != GB_LOOPER_PLAYING || state.loop_frames != 4U || state.playhead_frame != 2U) {
        return Fail("Playhead must wrap modulo Master frame count.");
    }

    if (looper.SetTransport(GB_LOOPER_PAUSED) != GB_OK) return Fail("Pause failed.");
    const std::uint64_t pausedFrame = state.playhead_frame;
    for (int index = 0; index < 12; ++index) looper.RenderFrame();
    looper.GetState(state);
    if (state.transport != GB_LOOPER_PAUSED || state.playhead_frame != pausedFrame) {
        return Fail("Pause must freeze the exact playhead.");
    }
    if (looper.ReadMonitor(rendered.data(), 6U) != 0U) return Fail("Pause must drain stale monitor PCM.");

    if (looper.Seek(1U) != GB_OK) return Fail("Paused seek failed.");
    looper.GetState(state);
    if (state.transport != GB_LOOPER_PAUSED || state.playhead_frame != 1U || state.monitor_fill_frames != 0U) {
        return Fail("Seek must move the exact playhead and clear stale monitor PCM without changing Pause state.");
    }
    if (looper.Seek(4U) != GB_ERROR_INVALID_ARGUMENT) return Fail("Seek must reject frame == loop length.");

    if (looper.SetTransport(GB_LOOPER_PLAYING) != GB_OK) return Fail("Resume failed.");
    looper.RenderFrame();
    looper.GetState(state);
    if (state.playhead_frame != 2U) return Fail("Resume after seek must continue from the sought frame.");

    if (looper.SetTransport(GB_LOOPER_STOPPED) != GB_OK) return Fail("Stop failed.");
    looper.GetState(state);
    if (state.transport != GB_LOOPER_STOPPED || state.playhead_frame != 0U) {
        return Fail("Stop must return playhead exactly to frame zero.");
    }

    if (looper.SetTransport(GB_LOOPER_PLAYING) != GB_OK) return Fail("Diagnostic play failed.");
    constexpr std::uint64_t oneHourFrames = 60ULL * 60ULL * LooperEngine::SampleRate;
    looper.AdvanceForDiagnostics(oneHourFrames + 3U);
    looper.GetState(state);
    if (state.playhead_frame != 3U) return Fail("Long-run modulo clock drift contract failed.");

    const std::array<float, 2> tiny{0.0F, 0.0F};
    if (looper.LoadMaster(tiny.data(), LooperEngine::MaxSupportedLoopFrames + 1U) != GB_ERROR_INVALID_ARGUMENT) {
        return Fail("Oversized Master must be rejected before reading source memory.");
    }

    looper.Clear();
    looper.GetState(state);
    if (state.loop_frames != 0U || state.playhead_frame != 0U || state.transport != GB_LOOPER_STOPPED) {
        return Fail("Clear must remove the Master and reset transport.");
    }

    std::cout << "GrassiLooper Gate 2 native transport tests passed.\n";
    return 0;
}