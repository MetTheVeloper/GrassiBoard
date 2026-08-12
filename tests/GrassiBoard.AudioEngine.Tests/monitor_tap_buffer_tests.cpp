#include "monitor_tap_buffer.h"

#include <cmath>
#include <cstdint>
#include <iostream>

int main()
{
    grassiboard::MonitorTapBuffer ring(5U); // usable capacity is 4 frames.
    if (ring.CapacityFrames() != 4U || ring.FillFrames() != 0U) {
        std::cerr << "Unexpected monitor tap capacity.\n";
        return 1;
    }

    if (!ring.Push(0.1F, -0.1F) || !ring.Push(0.2F, -0.2F) || ring.FillFrames() != 2U) {
        std::cerr << "Monitor tap push failed.\n";
        return 2;
    }

    float output[4]{};
    const std::uint32_t read = ring.Read(output, 2U);
    if (read != 2U || std::abs(output[0] - 0.1F) > 0.0001F ||
        std::abs(output[1] + 0.1F) > 0.0001F ||
        std::abs(output[2] - 0.2F) > 0.0001F ||
        std::abs(output[3] + 0.2F) > 0.0001F || ring.FillFrames() != 0U) {
        std::cerr << "Monitor tap read/order failed.\n";
        return 3;
    }

    ring.Reset();
    if (!ring.Push(1.0F, 1.0F) || !ring.Push(2.0F, 2.0F) ||
        !ring.Push(3.0F, 3.0F) || !ring.Push(4.0F, 4.0F)) {
        std::cerr << "Monitor tap fill failed.\n";
        return 4;
    }
    if (ring.Push(5.0F, 5.0F) || ring.OverrunCount() != 1U) {
        std::cerr << "Monitor tap bounded-overrun contract failed.\n";
        return 5;
    }

    ring.Reset();
    if (ring.FillFrames() != 0U || ring.OverrunCount() != 0U) {
        std::cerr << "Monitor tap reset failed.\n";
        return 6;
    }

    std::cout << "Monitor tap SPSC ring test passed.\n";
    return 0;
}
