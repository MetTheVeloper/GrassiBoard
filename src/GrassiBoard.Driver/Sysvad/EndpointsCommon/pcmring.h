#pragma once

// The transport policy is kept in this platform-neutral template so the exact
// wrap, silence, stale-data, underrun, and overrun behavior can be unit tested
// outside the kernel. TOps supplies atomic and memory primitives.
template <typename TOps>
class GrassiBoardPcmRing
{
public:
    void Initialize(
        unsigned char* buffer,
        unsigned long capacityBytes,
        unsigned long preRollBytes,
        unsigned long blockAlign)
    {
        m_buffer = buffer;
        m_capacityBytes = capacityBytes;
        m_preRollBytes = preRollBytes;
        m_blockAlign = blockAlign;
        TOps::Zero(m_buffer, m_capacityBytes);
        TOps::Exchange64(&m_readPosition, 0);
        TOps::Exchange64(&m_writePosition, 0);
        TOps::Exchange64(&m_underruns, 0);
        TOps::Exchange64(&m_overruns, 0);
        TOps::Exchange32(&m_renderActive, 0);
        TOps::Exchange32(&m_captureActive, 0);
        TOps::Exchange32(&m_primed, 0);
        TOps::Exchange32(&m_generation, 0);
    }

    void SetRenderActive(bool active)
    {
        if (active)
        {
            Flush();
            TOps::Exchange32(&m_renderActive, 1);
        }
        else
        {
            TOps::Exchange32(&m_renderActive, 0);
            Flush();
        }
    }

    void SetCaptureActive(bool active)
    {
        if (active)
        {
            TOps::Exchange32(&m_captureActive, 1);
            Flush();
        }
        else
        {
            TOps::Exchange32(&m_captureActive, 0);
            Flush();
        }
    }

    unsigned long Write(const unsigned char* source, unsigned long byteCount)
    {
        if (source == nullptr || byteCount == 0 ||
            TOps::Load32(&m_renderActive) == 0 ||
            TOps::Load32(&m_captureActive) == 0)
        {
            return 0;
        }

        byteCount = AlignDown(byteCount);
        const unsigned long long readPosition = TOps::Load64(&m_readPosition);
        const unsigned long long writePosition = TOps::Load64(&m_writePosition);
        const unsigned long long used = writePosition - readPosition;
        if (used > m_capacityBytes)
        {
            TOps::Increment64(&m_overruns);
            return 0;
        }

        const unsigned long writable = static_cast<unsigned long>(m_capacityBytes - used);
        const unsigned long writeCount = byteCount < writable ? byteCount : AlignDown(writable);
        if (writeCount > 0)
        {
            CopyIntoRing(writePosition, source, writeCount);
            TOps::Exchange64(&m_writePosition, writePosition + writeCount);
        }

        if (writeCount != byteCount)
        {
            TOps::Increment64(&m_overruns);
        }
        return writeCount;
    }

    unsigned long Read(unsigned char* destination, unsigned long byteCount)
    {
        if (destination == nullptr || byteCount == 0)
        {
            return 0;
        }

        TOps::Zero(destination, byteCount);
        byteCount = AlignDown(byteCount);
        if (byteCount == 0 ||
            TOps::Load32(&m_renderActive) == 0 ||
            TOps::Load32(&m_captureActive) == 0)
        {
            return 0;
        }

        const long generation = TOps::Load32(&m_generation);
        const unsigned long long readPosition = TOps::Load64(&m_readPosition);
        const unsigned long long writePosition = TOps::Load64(&m_writePosition);
        const unsigned long long available64 = writePosition - readPosition;
        if (available64 > m_capacityBytes)
        {
            TOps::Increment64(&m_underruns);
            TOps::Exchange32(&m_primed, 0);
            return 0;
        }

        const unsigned long available = static_cast<unsigned long>(available64);
        if (TOps::Load32(&m_primed) == 0)
        {
            if (available < m_preRollBytes)
            {
                return 0;
            }
            TOps::Exchange32(&m_primed, 1);
        }

        const unsigned long readCount = byteCount < available ? byteCount : AlignDown(available);
        if (readCount > 0)
        {
            CopyFromRing(readPosition, destination, readCount);
        }

        // A state change invalidates any bytes copied concurrently. Returning
        // silence is safer than leaking audio from a previous render session.
        if (generation != TOps::Load32(&m_generation) ||
            TOps::Load32(&m_renderActive) == 0 ||
            TOps::Load32(&m_captureActive) == 0)
        {
            TOps::Zero(destination, byteCount);
            return 0;
        }

        TOps::Exchange64(&m_readPosition, readPosition + readCount);
        if (readCount != byteCount)
        {
            TOps::Increment64(&m_underruns);
            TOps::Exchange32(&m_primed, 0);
        }
        return readCount;
    }

    unsigned long long Underruns() const { return TOps::Load64(&m_underruns); }
    unsigned long long Overruns() const { return TOps::Load64(&m_overruns); }
    unsigned long FillBytes() const
    {
        const unsigned long long readPosition = TOps::Load64(&m_readPosition);
        const unsigned long long writePosition = TOps::Load64(&m_writePosition);
        const unsigned long long fill = writePosition - readPosition;
        return fill <= m_capacityBytes ? static_cast<unsigned long>(fill) : 0;
    }

private:
    unsigned long AlignDown(unsigned long value) const
    {
        return m_blockAlign == 0 ? 0 : value - (value % m_blockAlign);
    }

    void Flush()
    {
        const unsigned long long writePosition = TOps::Load64(&m_writePosition);
        TOps::Exchange64(&m_readPosition, writePosition);
        TOps::Exchange32(&m_primed, 0);
        TOps::Increment32(&m_generation);
    }

    void CopyIntoRing(
        unsigned long long position,
        const unsigned char* source,
        unsigned long byteCount)
    {
        const unsigned long offset = static_cast<unsigned long>(position % m_capacityBytes);
        const unsigned long first = byteCount < (m_capacityBytes - offset)
            ? byteCount
            : (m_capacityBytes - offset);
        TOps::Copy(m_buffer + offset, source, first);
        if (first < byteCount)
        {
            TOps::Copy(m_buffer, source + first, byteCount - first);
        }
    }

    void CopyFromRing(
        unsigned long long position,
        unsigned char* destination,
        unsigned long byteCount) const
    {
        const unsigned long offset = static_cast<unsigned long>(position % m_capacityBytes);
        const unsigned long first = byteCount < (m_capacityBytes - offset)
            ? byteCount
            : (m_capacityBytes - offset);
        TOps::Copy(destination, m_buffer + offset, first);
        if (first < byteCount)
        {
            TOps::Copy(destination + first, m_buffer, byteCount - first);
        }
    }

    unsigned char* m_buffer;
    unsigned long m_capacityBytes;
    unsigned long m_preRollBytes;
    unsigned long m_blockAlign;
    volatile long long m_readPosition;
    volatile long long m_writePosition;
    volatile long long m_underruns;
    volatile long long m_overruns;
    volatile long m_renderActive;
    volatile long m_captureActive;
    volatile long m_primed;
    volatile long m_generation;
};
