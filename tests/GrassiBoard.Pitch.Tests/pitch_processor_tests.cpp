#include "pitch_processor.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <limits>
#include <numbers>
#include <sstream>
#include <string>
#include <vector>

namespace {
constexpr std::uint32_t kSampleRate = 48'000U;
constexpr std::size_t kDurationSamples = kSampleRate * 3U;
constexpr float kInputFrequency = 220.0F;
constexpr float kInputAmplitude = 0.3F;

void WriteLittleEndian16(std::ofstream& stream, const std::uint16_t value)
{
    const std::array<char, 2> bytes{
        static_cast<char>(value & 0xFFU),
        static_cast<char>((value >> 8U) & 0xFFU)};
    stream.write(bytes.data(), static_cast<std::streamsize>(bytes.size()));
}

void WriteLittleEndian32(std::ofstream& stream, const std::uint32_t value)
{
    const std::array<char, 4> bytes{
        static_cast<char>(value & 0xFFU),
        static_cast<char>((value >> 8U) & 0xFFU),
        static_cast<char>((value >> 16U) & 0xFFU),
        static_cast<char>((value >> 24U) & 0xFFU)};
    stream.write(bytes.data(), static_cast<std::streamsize>(bytes.size()));
}

bool WriteWav(const std::filesystem::path& path, const std::vector<float>& samples)
{
    std::ofstream stream(path, std::ios::binary);
    if (!stream) {
        return false;
    }

    constexpr std::uint16_t channels = 1U;
    constexpr std::uint16_t bitsPerSample = 16U;
    const auto dataBytes = static_cast<std::uint32_t>(samples.size() * sizeof(std::int16_t));
    stream.write("RIFF", 4);
    WriteLittleEndian32(stream, 36U + dataBytes);
    stream.write("WAVEfmt ", 8);
    WriteLittleEndian32(stream, 16U);
    WriteLittleEndian16(stream, 1U);
    WriteLittleEndian16(stream, channels);
    WriteLittleEndian32(stream, kSampleRate);
    WriteLittleEndian32(stream, kSampleRate * channels * bitsPerSample / 8U);
    WriteLittleEndian16(stream, static_cast<std::uint16_t>(channels * bitsPerSample / 8U));
    WriteLittleEndian16(stream, bitsPerSample);
    stream.write("data", 4);
    WriteLittleEndian32(stream, dataBytes);

    for (const float sample : samples) {
        const float clamped = std::clamp(sample, -1.0F, 1.0F);
        const auto pcm = static_cast<std::int16_t>(std::lround(clamped * 32767.0F));
        WriteLittleEndian16(stream, static_cast<std::uint16_t>(pcm));
    }
    return stream.good();
}

std::vector<float> MakeInput()
{
    std::vector<float> samples(kDurationSamples);
    for (std::size_t index = 0; index < samples.size(); ++index) {
        const double phase = 2.0 * std::numbers::pi * static_cast<double>(kInputFrequency) *
            static_cast<double>(index) / static_cast<double>(kSampleRate);
        samples[index] = kInputAmplitude * static_cast<float>(std::sin(phase));
    }
    return samples;
}

std::vector<float> ProcessPitch(const std::vector<float>& input, const float semitones,
    const bool bypass, std::uint32_t& latency)
{
    grassiboard::SignalsmithPitchProcessor processor;
    processor.SetQualityMode(grassiboard::PitchQualityMode::Balanced);
    processor.SetPitchSemitones(semitones);
    processor.SetBypass(bypass);
    if (!processor.Prepare(kSampleRate, 1U, 512U)) {
        return {};
    }
    latency = processor.GetLatencySamples();

    std::vector<float> output(input.size(), 0.0F);
    constexpr std::array<std::uint32_t, 5> blockPattern{37U, 64U, 127U, 256U, 511U};
    std::size_t offset = 0U;
    std::size_t patternIndex = 0U;
    while (offset < input.size()) {
        const auto frames = static_cast<std::uint32_t>(std::min<std::size_t>(
            blockPattern[patternIndex % blockPattern.size()], input.size() - offset));
        processor.Process(input.data() + offset, output.data() + offset, frames);
        offset += frames;
        ++patternIndex;
    }
    return output;
}

float EstimateFrequency(const std::vector<float>& samples, const std::uint32_t latency)
{
    const std::size_t start = std::min<std::size_t>(
        static_cast<std::size_t>(latency) + kSampleRate, samples.size() / 2U);
    const std::size_t end = samples.size() - 1U;
    std::size_t crossings = 0U;
    for (std::size_t index = start + 1U; index < end; ++index) {
        if (samples[index - 1U] <= 0.0F && samples[index] > 0.0F) {
            ++crossings;
        }
    }
    const double seconds = static_cast<double>(end - start) / static_cast<double>(kSampleRate);
    return seconds > 0.0 ? static_cast<float>(static_cast<double>(crossings) / seconds) : 0.0F;
}

bool ValidateFiniteAndPeak(const std::vector<float>& samples)
{
    float peak = 0.0F;
    for (const float sample : samples) {
        if (!std::isfinite(sample)) {
            return false;
        }
        peak = std::max(peak, std::abs(sample));
    }
    return peak <= 1.0F && peak > 0.01F;
}

std::string FileLabel(const int semitones)
{
    if (semitones < 0) {
        return "minus-" + std::to_string(-semitones);
    }
    if (semitones > 0) {
        return "plus-" + std::to_string(semitones);
    }
    return "zero";
}
}

int main(const int argumentCount, const char* const arguments[])
{
    if (argumentCount != 2) {
        std::cerr << "Expected an output directory.\n";
        return 1;
    }

    const std::filesystem::path outputDirectory(arguments[1]);
    std::filesystem::create_directories(outputDirectory);
    const std::vector<float> input = MakeInput();
    if (!WriteWav(outputDirectory / "input-220hz.wav", input)) {
        std::cerr << "Could not write input WAV.\n";
        return 2;
    }

    constexpr std::array<int, 7> semitoneTests{-12, -6, -3, 0, 3, 6, 12};
    std::ostringstream report;
    report << "{\n  \"backend\": \"Signalsmith Stretch 1.3.2\",\n"
           << "  \"backendCommit\": \"57b93f4e9206a089a45387eaa39bdc9f310d3308\",\n"
           << "  \"sampleRate\": " << kSampleRate << ",\n  \"results\": [\n";

    std::uint32_t reportedLatency = 0U;
    for (std::size_t testIndex = 0U; testIndex < semitoneTests.size(); ++testIndex) {
        const int semitones = semitoneTests[testIndex];
        std::uint32_t latency = 0U;
        const std::vector<float> output = ProcessPitch(input, static_cast<float>(semitones), false, latency);
        if (output.size() != input.size() || !ValidateFiniteAndPeak(output)) {
            std::cerr << "Invalid output for " << semitones << " semitones.\n";
            return 3;
        }
        reportedLatency = latency;
        const float measured = EstimateFrequency(output, latency);
        const float expected = kInputFrequency * std::pow(2.0F, static_cast<float>(semitones) / 12.0F);
        const float relativeError = std::abs(measured - expected) / expected;
        if (relativeError > 0.08F) {
            std::cerr << "Frequency check failed for " << semitones << " semitones: expected "
                      << expected << ", measured " << measured << ".\n";
            return 4;
        }

        const std::string fileName = "pitch-" + FileLabel(semitones) + ".wav";
        if (!WriteWav(outputDirectory / fileName, output)) {
            return 5;
        }
        report << "    {\"semitones\": " << semitones
               << ", \"expectedHz\": " << std::fixed << std::setprecision(2) << expected
               << ", \"measuredHz\": " << measured << ", \"file\": \"" << fileName << "\"}"
               << (testIndex + 1U == semitoneTests.size() ? "\n" : ",\n");
    }

    std::uint32_t bypassLatency = 0U;
    const std::vector<float> bypassOutput = ProcessPitch(input, 7.0F, true, bypassLatency);
    for (std::size_t index = bypassLatency; index < bypassOutput.size(); ++index) {
        if (std::abs(bypassOutput[index] - input[index - bypassLatency]) > 1.0e-6F) {
            std::cerr << "Latency-aligned bypass test failed.\n";
            return 6;
        }
    }

    grassiboard::SignalsmithPitchProcessor automationProcessor;
    automationProcessor.SetBypass(false);
    if (!automationProcessor.Prepare(kSampleRate, 1U, 256U)) {
        return 7;
    }
    std::array<float, 256> automationOutput{};
    for (std::size_t offset = 0U; offset + automationOutput.size() <= input.size(); offset += automationOutput.size()) {
        const float pitch = static_cast<float>((offset / automationOutput.size()) % 25U) - 12.0F;
        automationProcessor.SetPitchSemitones(pitch);
        automationProcessor.Process(input.data() + offset, automationOutput.data(),
            static_cast<std::uint32_t>(automationOutput.size()));
        if (!std::all_of(automationOutput.begin(), automationOutput.end(),
                [](const float sample) { return std::isfinite(sample) && std::abs(sample) <= 1.0F; })) {
            std::cerr << "Rapid automation produced invalid samples.\n";
            return 8;
        }
    }

    report << "  ],\n  \"latencySamples\": " << reportedLatency
           << ",\n  \"latencyMilliseconds\": " << std::fixed << std::setprecision(2)
           << static_cast<double>(reportedLatency) * 1000.0 / static_cast<double>(kSampleRate)
           << "\n}\n";
    std::ofstream reportFile(outputDirectory / "pitch-test-report.json");
    reportFile << report.str();
    if (!reportFile.good()) {
        return 9;
    }

    std::cout << "Pitch file tests passed. Latency: " << reportedLatency << " samples.\n";
    return 0;
}
