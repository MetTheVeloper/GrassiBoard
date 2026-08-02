#pragma once

// The virtual render endpoint remains stereo while the SysVAD MicIn contract
// remains mono. Convert each PCM16 frame before it enters the mono cable ring.
inline void GrassiBoardDownmixStereo16ToMono16(
    const short* source,
    short* destination,
    unsigned long frameCount)
{
    for (unsigned long frame = 0; frame < frameCount; ++frame)
    {
        const long left = static_cast<long>(source[frame * 2]);
        const long right = static_cast<long>(source[(frame * 2) + 1]);
        destination[frame] = static_cast<short>((left + right) / 2);
    }
}
