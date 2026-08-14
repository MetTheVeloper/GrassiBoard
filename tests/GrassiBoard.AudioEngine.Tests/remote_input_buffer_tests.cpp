#include "remote_input_buffer.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <iostream>
#include <thread>

namespace {
bool Near(const float a, const float b) noexcept
{
    return std::abs(a - b) < 0.0001F;
}
}

int main()
{
    grassiboard::RemoteInputBuffer ring(8U);
    const float first[]{0.1F, 0.2F, 0.3F, 0.4F};
    if (ring.Write(first, 4U) != 4U || ring.FillFrames() != 4U || ring.CapacityFrames() != 8U) {
        std::cerr << "Remote input initial write failed.\n";
        return 1;
    }

    float out[6]{};
    if (ring.Read(out, 3U) != 3U || !Near(out[0], 0.1F) || !Near(out[1], 0.2F) ||
        !Near(out[2], 0.3F) || ring.FillFrames() != 1U) {
        std::cerr << "Remote input ordered read failed.\n";
        return 2;
    }

    if (ring.Read(out, 4U) != 1U || !Near(out[0], 0.4F) ||
        !Near(out[1], 0.0F) || !Near(out[2], 0.0F) || !Near(out[3], 0.0F) ||
        ring.UnderrunFrames() != 3U) {
        std::cerr << "Remote input zero-fill/underrun contract failed.\n";
        return 3;
    }

    const float second[]{1.5F, -1.5F, 0.5F, 0.25F, -0.25F, 0.0F, 0.75F, -0.75F, 0.9F, -0.9F};
    if (ring.Write(second, 10U) != 8U || ring.OverrunFrames() != 2U || ring.FillFrames() != 8U) {
        std::cerr << "Remote input bounded overrun contract failed.\n";
        return 4;
    }

    float full[8]{};
    if (ring.Read(full, 8U) != 8U || !Near(full[0], 1.0F) || !Near(full[1], -1.0F) ||
        !Near(full[2], 0.5F) || ring.ConsumedFrames() != 12U || ring.PushedFrames() != 12U) {
        std::cerr << "Remote input clamp/statistics contract failed.\n";
        return 5;
    }

    ring.Reset();
    if (ring.FillFrames() != 0U || ring.PushedFrames() != 0U || ring.ConsumedFrames() != 0U ||
        ring.UnderrunFrames() != 0U || ring.OverrunFrames() != 0U) {
        std::cerr << "Remote input reset contract failed.\n";
        return 6;
    }

    // Concurrent SPSC stress: producer and consumer are the same ownership
    // pattern used by managed Phone Mic -> realtime render. Reset is purposely
    // excluded while the consumer is active.
    constexpr std::uint32_t stressFrames = 100'000U;
    grassiboard::RemoteInputBuffer stress(257U);
    std::atomic<bool> producerDone{false};
    std::atomic<bool> failed{false};

    auto valueFor = [](const std::uint32_t sequence) noexcept {
        return (static_cast<int>(sequence % 201U) - 100) / 100.0F;
    };

    std::thread producer([&] {
        std::uint32_t sequence = 0U;
        float block[31]{};
        while (sequence < stressFrames && !failed.load(std::memory_order_relaxed)) {
            const std::uint32_t requested = std::min<std::uint32_t>(31U, stressFrames - sequence);
            for (std::uint32_t index = 0U; index < requested; ++index) {
                block[index] = valueFor(sequence + index);
            }
            const std::uint32_t accepted = stress.Write(block, requested);
            sequence += accepted;
            if (accepted == 0U) std::this_thread::yield();
        }
        producerDone.store(true, std::memory_order_release);
    });

    std::uint32_t consumed = 0U;
    float block[29]{};
    while (consumed < stressFrames && !failed.load(std::memory_order_relaxed)) {
        if (stress.FillFrames() == 0U) {
            if (producerDone.load(std::memory_order_acquire) && consumed >= stressFrames) break;
            std::this_thread::yield();
            continue;
        }
        const std::uint32_t copied = stress.Read(block, std::min<std::uint32_t>(29U, stressFrames - consumed));
        for (std::uint32_t index = 0U; index < copied; ++index) {
            if (!Near(block[index], valueFor(consumed + index))) {
                failed.store(true, std::memory_order_relaxed);
                break;
            }
        }
        consumed += copied;
    }
    producer.join();

    if (failed.load(std::memory_order_relaxed) || consumed != stressFrames ||
        stress.PushedFrames() != stressFrames || stress.ConsumedFrames() != stressFrames) {
        std::cerr << "Remote input concurrent SPSC contract failed.\n";
        return 7;
    }

    std::cout << "Remote input SPSC ring test passed.\n";
    return 0;
}
