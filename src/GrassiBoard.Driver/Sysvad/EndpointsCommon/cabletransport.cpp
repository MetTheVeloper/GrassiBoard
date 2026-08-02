#include <sysvad.h>
#include "cabletransport.h"
#include "pcmconvert.h"
#include "pcmring.h"

namespace
{
    constexpr ULONG TransportCapacityBytes = GrassiBoardCableTransport::CaptureBytesPerSecond / 4; // 250 ms
    constexpr ULONG TransportPreRollBytes = GrassiBoardCableTransport::CaptureBytesPerSecond / 100; // 10 ms
    constexpr ULONG DownmixFramesPerChunk = 256;

    struct KernelPcmRingOperations
    {
        static long long Load64(const volatile long long* value)
        {
            return InterlockedCompareExchange64(
                reinterpret_cast<volatile LONG64*>(const_cast<volatile long long*>(value)), 0, 0);
        }

        static long Load32(const volatile long* value)
        {
            return InterlockedCompareExchange(
                reinterpret_cast<volatile LONG*>(const_cast<volatile long*>(value)), 0, 0);
        }

        static void Exchange64(volatile long long* value, long long replacement)
        {
            InterlockedExchange64(
                reinterpret_cast<volatile LONG64*>(value), replacement);
        }

        static void Exchange32(volatile long* value, long replacement)
        {
            InterlockedExchange(
                reinterpret_cast<volatile LONG*>(value), replacement);
        }

        static void Increment64(volatile long long* value)
        {
            InterlockedIncrement64(reinterpret_cast<volatile LONG64*>(value));
        }

        static void Increment32(volatile long* value)
        {
            InterlockedIncrement(reinterpret_cast<volatile LONG*>(value));
        }

        static void Copy(void* destination, const void* source, unsigned long byteCount)
        {
            RtlCopyMemory(destination, source, byteCount);
        }

        static void Zero(void* destination, unsigned long byteCount)
        {
            RtlZeroMemory(destination, byteCount);
        }
    };

    alignas(64) BYTE g_TransportBuffer[TransportCapacityBytes] = {};
    GrassiBoardPcmRing<KernelPcmRingOperations> g_Transport = {};
    volatile LONG g_InitializeState = 0;

    void EnsureInitialized()
    {
        if (InterlockedCompareExchange(&g_InitializeState, 1, 0) == 0)
        {
            g_Transport.Initialize(
                g_TransportBuffer,
                TransportCapacityBytes,
                TransportPreRollBytes,
                GrassiBoardCableTransport::CaptureBlockAlign);
            InterlockedExchange(&g_InitializeState, 2);
            return;
        }

        while (InterlockedCompareExchange(&g_InitializeState, 2, 2) != 2)
        {
            YieldProcessor();
        }
    }
}

void GrassiBoardCableTransport::SetRenderActive(bool active)
{
    EnsureInitialized();
    g_Transport.SetRenderActive(active);
}

void GrassiBoardCableTransport::SetCaptureActive(bool active)
{
    EnsureInitialized();
    g_Transport.SetCaptureActive(active);
}

ULONG GrassiBoardCableTransport::Write(const BYTE* source, ULONG byteCount)
{
    EnsureInitialized();
    if (source == nullptr || byteCount < RenderBlockAlign)
    {
        return 0;
    }

    const ULONG alignedBytes = byteCount - (byteCount % RenderBlockAlign);
    ULONG consumedBytes = 0;
    SHORT mono[DownmixFramesPerChunk] = {};

    while (consumedBytes < alignedBytes)
    {
        const ULONG remainingFrames = (alignedBytes - consumedBytes) / RenderBlockAlign;
        const ULONG chunkFrames = min(remainingFrames, DownmixFramesPerChunk);
        const SHORT* stereo = reinterpret_cast<const SHORT*>(source + consumedBytes);
        GrassiBoardDownmixStereo16ToMono16(stereo, mono, chunkFrames);

        const ULONG monoBytes = chunkFrames * CaptureBlockAlign;
        const ULONG writtenBytes = g_Transport.Write(
            reinterpret_cast<const BYTE*>(mono),
            monoBytes);
        consumedBytes += (writtenBytes / CaptureBlockAlign) * RenderBlockAlign;
        if (writtenBytes != monoBytes)
        {
            break;
        }
    }

    return consumedBytes;
}

ULONG GrassiBoardCableTransport::Read(BYTE* destination, ULONG byteCount)
{
    EnsureInitialized();
    return g_Transport.Read(destination, byteCount);
}

ULONGLONG GrassiBoardCableTransport::GetUnderrunCount()
{
    EnsureInitialized();
    return g_Transport.Underruns();
}

ULONGLONG GrassiBoardCableTransport::GetOverrunCount()
{
    EnsureInitialized();
    return g_Transport.Overruns();
}

ULONG GrassiBoardCableTransport::GetFillBytes()
{
    EnsureInitialized();
    return g_Transport.FillBytes();
}
