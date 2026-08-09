#pragma once

#include "grassiboard/audio_engine.h"

#include <array>
#include <atomic>
#include <cstddef>
#include <cstdint>
#include <memory>
#include <mutex>
#include <unordered_map>
#include <vector>

namespace grassiboard {

class SoundboardMixer final {
public:
    static constexpr std::size_t MaxVoices = 32U;

    gb_result LoadClip(std::uint64_t key, const float* stereoSamples, std::uint64_t frameCount);
    gb_result Play(std::uint64_t key, float volume, bool loop, bool restart) noexcept;
    gb_result Stop(std::uint64_t key) noexcept;
    gb_result StopAll() noexcept;

    void MixFrame(float& left, float& right) noexcept;
    void ResetPlayback() noexcept;
    std::uint32_t ActiveVoiceCount() const noexcept;

private:
    struct Clip {
        std::uint64_t key = 0U;
        std::vector<float> samples;
        std::uint64_t frame_count = 0U;
    };

    struct Voice {
        const Clip* clip = nullptr;
        std::uint64_t position = 0U;
        float volume = 1.0F;
        bool loop = false;
    };

    enum class CommandType : std::uint8_t {
        Play,
        Stop,
        StopAll
    };

    struct Command {
        CommandType type = CommandType::StopAll;
        const Clip* clip = nullptr;
        std::uint64_t key = 0U;
        float volume = 1.0F;
        bool loop = false;
        bool restart = true;
    };

    static constexpr std::size_t CommandCapacity = 256U;
    static constexpr std::uint64_t MaxClipFrames = 48'000ULL * 60ULL * 10ULL;

    bool Enqueue(const Command& command) noexcept;
    void DrainCommands() noexcept;
    void ApplyCommand(const Command& command) noexcept;
    void RefreshActiveVoiceCount() noexcept;

    std::mutex clip_mutex_;
    std::vector<std::unique_ptr<Clip>> clip_storage_;
    std::unordered_map<std::uint64_t, const Clip*> current_clips_;

    std::array<Command, CommandCapacity> commands_{};
    std::atomic<std::size_t> command_read_{0U};
    std::atomic<std::size_t> command_write_{0U};
    std::array<Voice, MaxVoices> voices_{};
    std::atomic<std::uint32_t> active_voice_count_{0U};
};

}
