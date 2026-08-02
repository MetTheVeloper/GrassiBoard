#pragma once

namespace GrassiBoardCableTransport
{
    constexpr ULONG SampleRate = 48000;
    constexpr ULONG RenderChannelCount = 2;
    constexpr ULONG CaptureChannelCount = 1;
    constexpr ULONG BitsPerSample = 16;
    constexpr ULONG RenderBlockAlign = RenderChannelCount * (BitsPerSample / 8);
    constexpr ULONG CaptureBlockAlign = CaptureChannelCount * (BitsPerSample / 8);
    constexpr ULONG CaptureBytesPerSecond = SampleRate * CaptureBlockAlign;

    void SetRenderActive(bool active);
    void SetCaptureActive(bool active);
    ULONG Write(_In_reads_bytes_(byteCount) const BYTE* source, ULONG byteCount);
    ULONG Read(_Out_writes_bytes_(byteCount) BYTE* destination, ULONG byteCount);
    ULONGLONG GetUnderrunCount();
    ULONGLONG GetOverrunCount();
    ULONG GetFillBytes();
}
