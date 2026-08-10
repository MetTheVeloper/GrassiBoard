#include "media_stream.h"

#include <cmath>
#include <iostream>
#include <limits>

int main()
{
    grassiboard::MediaStreamBuffer buffer(4U);
    const float first[]{0.1F, -0.1F, 0.2F, -0.2F, 0.3F, -0.3F};
    if (buffer.Write(first, 3U) != 3U || buffer.FillFrames() != 3U ||
        buffer.CapacityFrames() != 4U) {
        std::cerr << "Media write/fill contract failed.\n";
        return 1;
    }

    float left = 1.0F;
    float right = 1.0F;
    if (buffer.Pop(left, right) || left != 0.0F || right != 0.0F) {
        std::cerr << "Inactive media must be silent.\n";
        return 2;
    }

    buffer.SetActive(true);
    if (!buffer.Pop(left, right) || std::abs(left - 0.1F) > 0.0001F ||
        std::abs(right + 0.1F) > 0.0001F) {
        std::cerr << "Media FIFO order failed.\n";
        return 3;
    }

    const float overflow[]{
        0.4F, -0.4F, 0.5F, -0.5F, 0.6F, -0.6F,
        std::numeric_limits<float>::quiet_NaN(), std::numeric_limits<float>::infinity()};
    if (buffer.Write(overflow, 4U) != 2U || buffer.FillFrames() != 4U) {
        std::cerr << "Bounded media capacity failed.\n";
        return 4;
    }

    buffer.Clear();
    if (buffer.FillFrames() != 0U || buffer.Pop(left, right)) {
        std::cerr << "Media clear contract failed.\n";
        return 5;
    }

    const float invalid[]{
        std::numeric_limits<float>::quiet_NaN(),
        std::numeric_limits<float>::infinity()};
    if (buffer.Write(invalid, 1U) != 1U || !buffer.Pop(left, right) ||
        left != 0.0F || right != 0.0F) {
        std::cerr << "Media finite-sample protection failed.\n";
        return 6;
    }

    grassiboard::MediaStreamBuffer alignedBuffer(16U);
    const float aligned[]{
        0.1F, -0.1F, 0.2F, -0.2F, 0.3F, -0.3F,
        0.4F, -0.4F, 0.5F, -0.5F, 0.6F, -0.6F};
    if (alignedBuffer.Write(aligned, 6U) != 6U) {
        std::cerr << "Media alignment setup failed.\n";
        return 7;
    }
    alignedBuffer.SetActive(true);
    alignedBuffer.SynchronizeDelay(2U);
    if (!alignedBuffer.Pop(left, right) || left != 0.0F || right != 0.0F ||
        !alignedBuffer.Pop(left, right) || left != 0.0F || right != 0.0F ||
        !alignedBuffer.Pop(left, right) || std::abs(left - 0.1F) > 0.0001F) {
        std::cerr << "Media pitch-alignment delay contract failed.\n";
        return 8;
    }

    alignedBuffer.SynchronizeDelay(3U);
    if (!alignedBuffer.Pop(left, right) || left != 0.0F || right != 0.0F ||
        !alignedBuffer.Pop(left, right) || std::abs(left - 0.2F) > 0.0001F) {
        std::cerr << "Media live delay-increase contract failed.\n";
        return 9;
    }

    alignedBuffer.SynchronizeDelay(0U);
    if (!alignedBuffer.Pop(left, right) || std::abs(left - 0.6F) > 0.0001F) {
        std::cerr << "Media live delay-reduction contract failed.\n";
        return 10;
    }

    std::cout << "Media streaming and pitch-alignment contracts passed.\n";
    return 0;
}
