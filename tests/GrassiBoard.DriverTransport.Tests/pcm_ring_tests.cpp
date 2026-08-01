#include <algorithm>
#include <array>
#include <cstdint>
#include <cstring>
#include <iostream>

#include "pcmring.h"

namespace
{
    struct TestOperations
    {
        static long long Load64(const volatile long long* value) { return *value; }
        static long Load32(const volatile long* value) { return *value; }
        static void Exchange64(volatile long long* value, long long replacement) { *value = replacement; }
        static void Exchange32(volatile long* value, long replacement) { *value = replacement; }
        static void Increment64(volatile long long* value) { ++(*value); }
        static void Increment32(volatile long* value) { ++(*value); }
        static void Copy(void* destination, const void* source, unsigned long byteCount)
        {
            std::memcpy(destination, source, byteCount);
        }
        static void Zero(void* destination, unsigned long byteCount)
        {
            std::memset(destination, 0, byteCount);
        }
    };

    using TestRing = GrassiBoardPcmRing<TestOperations>;

    bool Expect(bool condition, const char* message)
    {
        if (!condition)
        {
            std::cerr << "FAILED: " << message << '\n';
            return false;
        }
        return true;
    }

    bool TestInactiveAndPreRollSilence()
    {
        std::array<unsigned char, 16> storage{};
        TestRing ring{};
        ring.Initialize(storage.data(), static_cast<unsigned long>(storage.size()), 4, 2);

        const std::array<unsigned char, 4> source{1, 2, 3, 4};
        std::array<unsigned char, 4> output{9, 9, 9, 9};
        bool ok = Expect(ring.Write(source.data(), 2) == 0, "inactive producer must discard input");
        ok &= Expect(ring.Read(output.data(), 4) == 0, "inactive capture must return silence");
        ok &= Expect(std::all_of(output.begin(), output.end(), [](auto value) { return value == 0; }),
            "inactive capture bytes must be zero");

        ring.SetRenderActive(true);
        ring.SetCaptureActive(true);
        ok &= Expect(ring.Write(source.data(), 2) == 2, "producer must accept aligned input");
        output.fill(9);
        ok &= Expect(ring.Read(output.data(), 2) == 0, "capture must wait for pre-roll");
        ok &= Expect(output[0] == 0 && output[1] == 0, "pre-roll output must be silence");
        ok &= Expect(ring.Write(source.data() + 2, 2) == 2, "producer must complete pre-roll");
        ok &= Expect(ring.Read(output.data(), 4) == 4, "primed capture must read PCM");
        ok &= Expect(output == source, "PCM bytes must survive transport unchanged");
        return ok;
    }

    bool TestWrapAndOrder()
    {
        std::array<unsigned char, 12> storage{};
        TestRing ring{};
        ring.Initialize(storage.data(), static_cast<unsigned long>(storage.size()), 2, 2);
        ring.SetRenderActive(true);
        ring.SetCaptureActive(true);

        const std::array<unsigned char, 16> source{
            1, 2, 3, 4, 5, 6, 7, 8,
            9, 10, 11, 12, 13, 14, 15, 16};
        std::array<unsigned char, 8> first{};
        std::array<unsigned char, 8> second{};
        bool ok = Expect(ring.Write(source.data(), 10) == 10, "initial ring write failed");
        ok &= Expect(ring.Read(first.data(), 8) == 8, "initial ring read failed");
        ok &= Expect(std::equal(first.begin(), first.end(), source.begin()), "initial byte order changed");
        ok &= Expect(ring.Write(source.data() + 10, 6) == 6, "wrapped ring write failed");
        ok &= Expect(ring.Read(second.data(), 8) == 8, "wrapped ring read failed");
        ok &= Expect(std::equal(second.begin(), second.begin() + 2, source.begin() + 8),
            "wrapped read lost queued prefix");
        ok &= Expect(std::equal(second.begin() + 2, second.end(), source.begin() + 10),
            "wrapped read changed new PCM order");
        return ok;
    }

    bool TestUnderrunReturnsSilenceWithoutRepeating()
    {
        std::array<unsigned char, 16> storage{};
        TestRing ring{};
        ring.Initialize(storage.data(), static_cast<unsigned long>(storage.size()), 2, 2);
        ring.SetRenderActive(true);
        ring.SetCaptureActive(true);

        const std::array<unsigned char, 4> source{4, 3, 2, 1};
        std::array<unsigned char, 8> output{};
        bool ok = Expect(ring.Write(source.data(), 4) == 4, "underrun setup write failed");
        ok &= Expect(ring.Read(output.data(), 8) == 4, "partial read must report real byte count");
        ok &= Expect(std::equal(output.begin(), output.begin() + 4, source.begin()), "partial read changed PCM");
        ok &= Expect(std::all_of(output.begin() + 4, output.end(), [](auto value) { return value == 0; }),
            "underrun tail must be silence");
        output.fill(9);
        ok &= Expect(ring.Read(output.data(), 8) == 0, "empty ring must not repeat old PCM");
        ok &= Expect(std::all_of(output.begin(), output.end(), [](auto value) { return value == 0; }),
            "empty ring must remain silent");
        ok &= Expect(ring.Underruns() == 1, "underrun counter must increment once for partial consumption");
        return ok;
    }

    bool TestOverrunDropsNewestFrames()
    {
        std::array<unsigned char, 8> storage{};
        TestRing ring{};
        ring.Initialize(storage.data(), static_cast<unsigned long>(storage.size()), 2, 2);
        ring.SetRenderActive(true);
        ring.SetCaptureActive(true);

        const std::array<unsigned char, 12> source{1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12};
        std::array<unsigned char, 8> output{};
        bool ok = Expect(ring.Write(source.data(), 12) == 8, "overrun must keep only available aligned bytes");
        ok &= Expect(ring.Overruns() == 1, "overrun counter must increment");
        ok &= Expect(ring.Read(output.data(), 8) == 8, "retained PCM must remain readable");
        ok &= Expect(std::equal(output.begin(), output.end(), source.begin()), "overrun must not corrupt retained PCM");
        return ok;
    }

    bool TestStopFlushesStaleAudio()
    {
        std::array<unsigned char, 16> storage{};
        TestRing ring{};
        ring.Initialize(storage.data(), static_cast<unsigned long>(storage.size()), 2, 2);
        ring.SetRenderActive(true);
        ring.SetCaptureActive(true);

        const std::array<unsigned char, 4> source{7, 7, 7, 7};
        std::array<unsigned char, 4> output{9, 9, 9, 9};
        bool ok = Expect(ring.Write(source.data(), 4) == 4, "flush setup write failed");
        ring.SetRenderActive(false);
        ok &= Expect(ring.Read(output.data(), 4) == 0, "stopped renderer must produce no data");
        ok &= Expect(std::all_of(output.begin(), output.end(), [](auto value) { return value == 0; }),
            "stopped renderer must produce silence");
        ring.SetRenderActive(true);
        output.fill(9);
        ok &= Expect(ring.Read(output.data(), 4) == 0, "restart must not expose stale PCM");
        ok &= Expect(std::all_of(output.begin(), output.end(), [](auto value) { return value == 0; }),
            "restart before new playback must remain silent");
        return ok;
    }
}

int main()
{
    bool ok = true;
    ok &= TestInactiveAndPreRollSilence();
    ok &= TestWrapAndOrder();
    ok &= TestUnderrunReturnsSilenceWithoutRepeating();
    ok &= TestOverrunDropsNewestFrames();
    ok &= TestStopFlushesStaleAudio();
    if (ok)
    {
        std::cout << "GrassiBoard PCM transport policy tests passed.\n";
    }
    return ok ? 0 : 1;
}
