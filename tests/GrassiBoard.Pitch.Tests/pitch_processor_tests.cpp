#include "pitch_processor.h"

#include <algorithm>
#include <array>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <numbers>
#include <sstream>
#include <string>
#include <vector>

namespace {
constexpr std::uint32_t kSampleRate = 48'000U;
constexpr std::size_t kDurationSamples = kSampleRate * 3U;
constexpr float kInputFrequency = 220.0F;
constexpr float kInputAmplitude = 0.3F;
constexpr std::array<std::uint32_t, 5> kBlockPattern{37U, 64U, 127U, 256U, 511U};

struct BenchmarkResult {
    grassiboard::PitchQualityMode mode;
    std::uint32_t latencySamples;
    double processingMilliseconds;
    double singleCorePercent;
    float measuredHertz;
    float frequencyErrorPercent;
};

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

std::vector<float> MakeSineInput()
{
    std::vector<float> samples(kDurationSamples);
    for (std::size_t index = 0U; index < samples.size(); ++index) {
        const double phase = 2.0 * std::numbers::pi * static_cast<double>(kInputFrequency) *
            static_cast<double>(index) / static_cast<double>(kSampleRate);
        samples[index] = kInputAmplitude * static_cast<float>(std::sin(phase));
    }
    return samples;
}

std::vector<float> MakeVoiceLikeInput()
{
    constexpr float fundamental = 125.0F;
    std::vector<float> samples(kDurationSamples, 0.0F);
    for (std::size_t index = 0U; index < samples.size(); ++index) {
        const double time = static_cast<double>(index) / static_cast<double>(kSampleRate);
        double value = 0.0;
        for (std::uint32_t harmonic = 1U; harmonic <= 40U; ++harmonic) {
            const double frequency = static_cast<double>(fundamental) * harmonic;
            const auto resonance = [frequency](const double center, const double width) {
                const double distance = (frequency - center) / width;
                return std::exp(-0.5 * distance * distance);
            };
            const double envelope = 0.04 +
                1.0 * resonance(700.0, 120.0) +
                0.75 * resonance(1'220.0, 170.0) +
                0.45 * resonance(2'500.0, 260.0);
            value += envelope / static_cast<double>(harmonic) *
                std::sin(2.0 * std::numbers::pi * frequency * time);
        }
        samples[index] = static_cast<float>(value);
    }

    float peak = 0.0F;
    for (const float sample : samples) {
        peak = std::max(peak, std::abs(sample));
    }
    if (peak > 0.0F) {
        for (float& sample : samples) {
            sample *= 0.35F / peak;
        }
    }
    return samples;
}

void ProcessBlocks(
    grassiboard::IPitchProcessor& processor,
    const std::vector<float>& input,
    std::vector<float>& output)
{
    std::size_t offset = 0U;
    std::size_t patternIndex = 0U;
    while (offset < input.size()) {
        const auto frames = static_cast<std::uint32_t>(std::min<std::size_t>(
            kBlockPattern[patternIndex % kBlockPattern.size()], input.size() - offset));
        processor.Process(input.data() + offset, output.data() + offset, frames);
        offset += frames;
        ++patternIndex;
    }
}

std::vector<float> ProcessPitch(
    const std::vector<float>& input,
    const float semitones,
    const grassiboard::PitchQualityMode mode,
    const bool bypass,
    const bool preserveFormants,
    const float formantSemitones,
    std::uint32_t& latency)
{
    grassiboard::SignalsmithPitchProcessor processor;
    processor.SetQualityMode(mode);
    processor.SetPitchSemitones(semitones);
    processor.SetFormantPreservation(preserveFormants);
    processor.SetFormantSemitones(formantSemitones);
    processor.SetBypass(bypass);
    if (!processor.Prepare(kSampleRate, 1U, 512U)) {
        return {};
    }
    latency = processor.GetLatencySamples();

    std::vector<float> output(input.size(), 0.0F);
    ProcessBlocks(processor, input, output);
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

double RmsDifference(
    const std::vector<float>& left,
    const std::vector<float>& right,
    const std::size_t start)
{
    if (left.size() != right.size() || start >= left.size()) {
        return 0.0;
    }
    double squareSum = 0.0;
    for (std::size_t index = start; index < left.size(); ++index) {
        const double difference = static_cast<double>(left[index]) - static_cast<double>(right[index]);
        squareSum += difference * difference;
    }
    return std::sqrt(squareSum / static_cast<double>(left.size() - start));
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

const char* ModeName(const grassiboard::PitchQualityMode mode)
{
    switch (mode) {
    case grassiboard::PitchQualityMode::LowLatency:
        return "Low latency";
    case grassiboard::PitchQualityMode::HighQuality:
        return "High quality";
    case grassiboard::PitchQualityMode::Balanced:
    default:
        return "Balanced";
    }
}

BenchmarkResult BenchmarkMode(
    const std::vector<float>& input,
    const grassiboard::PitchQualityMode mode)
{
    grassiboard::SignalsmithPitchProcessor processor;
    processor.SetQualityMode(mode);
    processor.SetPitchSemitones(7.0F);
    processor.SetFormantPreservation(true);
    processor.SetBypass(false);
    if (!processor.Prepare(kSampleRate, 1U, 512U)) {
        return {mode, 0U, 0.0, 100.0, 0.0F, 100.0F};
    }

    constexpr std::uint32_t iterations = 6U;
    std::vector<float> output(input.size(), 0.0F);
    double elapsedMilliseconds = 0.0;
    for (std::uint32_t iteration = 0U; iteration < iterations; ++iteration) {
        processor.Reset();
        const auto start = std::chrono::steady_clock::now();
        ProcessBlocks(processor, input, output);
        const auto finish = std::chrono::steady_clock::now();
        elapsedMilliseconds += std::chrono::duration<double, std::milli>(finish - start).count();
    }

    const float expected = kInputFrequency * std::pow(2.0F, 7.0F / 12.0F);
    const float measured = EstimateFrequency(output, processor.GetLatencySamples());
    const float errorPercent = std::abs(measured - expected) / expected * 100.0F;
    const double audioMilliseconds = static_cast<double>(input.size()) * 1000.0 * iterations /
        static_cast<double>(kSampleRate);
    const double singleCorePercent = elapsedMilliseconds / audioMilliseconds * 100.0;
    return {
        mode,
        processor.GetLatencySamples(),
        elapsedMilliseconds,
        singleCorePercent,
        measured,
        errorPercent};
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
    const std::vector<float> sineInput = MakeSineInput();
    const std::vector<float> voiceInput = MakeVoiceLikeInput();
    if (!WriteWav(outputDirectory / "input-220hz.wav", sineInput) ||
        !WriteWav(outputDirectory / "input-voice-like.wav", voiceInput)) {
        std::cerr << "Could not write input WAV files.\n";
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
        const std::vector<float> output = ProcessPitch(
            sineInput,
            static_cast<float>(semitones),
            grassiboard::PitchQualityMode::Balanced,
            false,
            false,
            0.0F,
            latency);
        if (output.size() != sineInput.size() || !ValidateFiniteAndPeak(output)) {
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
    const std::vector<float> bypassOutput = ProcessPitch(
        sineInput, 7.0F, grassiboard::PitchQualityMode::Balanced, true, true, 0.0F, bypassLatency);
    for (std::size_t index = bypassLatency; index < bypassOutput.size(); ++index) {
        if (std::abs(bypassOutput[index] - sineInput[index - bypassLatency]) > 1.0e-6F) {
            std::cerr << "Latency-aligned bypass test failed.\n";
            return 6;
        }
    }

    std::uint32_t preserveLatency = 0U;
    std::uint32_t naturalLatency = 0U;
    std::uint32_t shiftedLatency = 0U;
    const std::vector<float> formantPreserved = ProcessPitch(
        voiceInput, 7.0F, grassiboard::PitchQualityMode::Balanced, false, true, 0.0F, preserveLatency);
    const std::vector<float> formantNatural = ProcessPitch(
        voiceInput, 7.0F, grassiboard::PitchQualityMode::Balanced, false, false, 0.0F, naturalLatency);
    const std::vector<float> formantShifted = ProcessPitch(
        voiceInput, 7.0F, grassiboard::PitchQualityMode::Balanced, false, true, 6.0F, shiftedLatency);
    if (!ValidateFiniteAndPeak(formantPreserved) ||
        !ValidateFiniteAndPeak(formantNatural) ||
        !ValidateFiniteAndPeak(formantShifted)) {
        std::cerr << "Formant processing produced invalid samples.\n";
        return 7;
    }
    const std::size_t formantAnalysisStart = static_cast<std::size_t>(
        std::max({preserveLatency, naturalLatency, shiftedLatency})) + kSampleRate;
    const double preservationDifference = RmsDifference(
        formantPreserved, formantNatural, formantAnalysisStart);
    const double shiftDifference = RmsDifference(
        formantPreserved, formantShifted, formantAnalysisStart);
    if (preservationDifference < 0.001 || shiftDifference < 0.001) {
        std::cerr << "Formant controls did not create a measurable difference.\n";
        return 8;
    }
    if (!WriteWav(outputDirectory / "formant-preserved.wav", formantPreserved) ||
        !WriteWav(outputDirectory / "formant-unpreserved.wav", formantNatural) ||
        !WriteWav(outputDirectory / "formant-shift-plus-6.wav", formantShifted)) {
        return 9;
    }

    grassiboard::LivePitchProcessor liveProcessor;
    liveProcessor.SetBypass(false);
    liveProcessor.SetPitchSemitones(7.0F);
    liveProcessor.SetFormantPreservation(true);
    if (!liveProcessor.Prepare(kSampleRate, 1U, 512U)) {
        return 10;
    }
    std::vector<float> liveOutput(voiceInput.size(), 0.0F);
    std::size_t offset = 0U;
    std::size_t patternIndex = 0U;
    while (offset < voiceInput.size()) {
        if (offset >= kSampleRate / 2U && offset < kSampleRate) {
            liveProcessor.SetQualityMode(grassiboard::PitchQualityMode::LowLatency);
        }
        else if (offset >= kSampleRate && offset < kSampleRate * 3U / 2U) {
            liveProcessor.SetQualityMode(grassiboard::PitchQualityMode::HighQuality);
            liveProcessor.SetFormantPreservation(false);
        }
        else if (offset >= kSampleRate * 3U / 2U) {
            liveProcessor.SetQualityMode(grassiboard::PitchQualityMode::Balanced);
            liveProcessor.SetFormantPreservation(true);
            liveProcessor.SetFormantSemitones(3.0F);
        }
        const auto frames = static_cast<std::uint32_t>(std::min<std::size_t>(
            kBlockPattern[patternIndex % kBlockPattern.size()], voiceInput.size() - offset));
        liveProcessor.Process(voiceInput.data() + offset, liveOutput.data() + offset, frames);
        offset += frames;
        ++patternIndex;
    }
    if (!ValidateFiniteAndPeak(liveOutput) ||
        liveProcessor.GetLatencySamples() != reportedLatency) {
        std::cerr << "Live quality switching failed.\n";
        return 11;
    }

    float maximumStep = 0.0F;
    std::size_t longestSilence = 0U;
    std::size_t currentSilence = 0U;
    for (std::size_t index = formantAnalysisStart + 1U; index < liveOutput.size(); ++index) {
        maximumStep = std::max(maximumStep, std::abs(liveOutput[index] - liveOutput[index - 1U]));
        if (std::abs(liveOutput[index]) < 1.0e-6F) {
            ++currentSilence;
            longestSilence = std::max(longestSilence, currentSilence);
        }
        else {
            currentSilence = 0U;
        }
    }
    if (maximumStep > 0.8F || longestSilence > 2'048U) {
        std::cerr << "Live switching introduced a discontinuity or stream cut.\n";
        return 12;
    }
    if (!WriteWav(outputDirectory / "live-mode-switches.wav", liveOutput)) {
        return 13;
    }

    constexpr std::array<grassiboard::PitchQualityMode, 3> modes{
        grassiboard::PitchQualityMode::LowLatency,
        grassiboard::PitchQualityMode::Balanced,
        grassiboard::PitchQualityMode::HighQuality};
    std::array<BenchmarkResult, 3> benchmarks{};
    for (std::size_t index = 0U; index < modes.size(); ++index) {
        benchmarks[index] = BenchmarkMode(sineInput, modes[index]);
        if (benchmarks[index].latencySamples == 0U ||
            benchmarks[index].frequencyErrorPercent > 5.0F ||
            benchmarks[index].singleCorePercent <= 0.0) {
            std::cerr << "Quality benchmark failed for " << ModeName(modes[index]) << ".\n";
            return 14;
        }
    }

    const BenchmarkResult& balanced = benchmarks[1];
    const BenchmarkResult& highQuality = benchmarks[2];
    const bool balancedMeetsDefaultPolicy =
        balanced.frequencyErrorPercent <= 3.0F &&
        balanced.singleCorePercent <= 25.0 &&
        balanced.latencySamples < highQuality.latencySamples;
    if (!balancedMeetsDefaultPolicy) {
        std::cerr << "Balanced mode no longer meets the default-selection policy.\n";
        return 15;
    }

    const double aggregateLivePercent = benchmarks[0].singleCorePercent +
        benchmarks[1].singleCorePercent + benchmarks[2].singleCorePercent;
    std::ofstream benchmarkFile(outputDirectory / "pitch-benchmark.json");
    benchmarkFile << "{\n  \"sampleRate\": " << kSampleRate
                  << ",\n  \"iterations\": 6,\n  \"modes\": [\n";
    for (std::size_t index = 0U; index < benchmarks.size(); ++index) {
        const BenchmarkResult& item = benchmarks[index];
        benchmarkFile << "    {\"mode\": \"" << ModeName(item.mode)
                      << "\", \"latencySamples\": " << item.latencySamples
                      << ", \"latencyMilliseconds\": " << std::fixed << std::setprecision(2)
                      << static_cast<double>(item.latencySamples) * 1000.0 / kSampleRate
                      << ", \"processingMilliseconds\": " << item.processingMilliseconds
                      << ", \"singleCorePercent\": " << item.singleCorePercent
                      << ", \"measuredHz\": " << item.measuredHertz
                      << ", \"frequencyErrorPercent\": " << item.frequencyErrorPercent << "}"
                      << (index + 1U == benchmarks.size() ? "\n" : ",\n");
    }
    benchmarkFile << "  ],\n  \"estimatedLiveParallelSingleCorePercent\": "
                  << aggregateLivePercent
                  << ",\n  \"recommendedDefault\": \"Balanced\",\n"
                  << "  \"selectionPolicy\": \"frequency error <= 3%, measured single-core <= 25%, "
                     "and latency below High quality\"\n}\n";
    if (!benchmarkFile.good()) {
        return 16;
    }

    report << "  ],\n  \"latencySamples\": " << reportedLatency
           << ",\n  \"latencyMilliseconds\": " << std::fixed << std::setprecision(2)
           << static_cast<double>(reportedLatency) * 1000.0 / static_cast<double>(kSampleRate)
           << ",\n  \"formantPreservationRmsDifference\": " << preservationDifference
           << ",\n  \"formantShiftRmsDifference\": " << shiftDifference
           << ",\n  \"maximumLiveSwitchStep\": " << maximumStep
           << ",\n  \"longestLiveSwitchSilenceSamples\": " << longestSilence
           << "\n}\n";
    std::ofstream reportFile(outputDirectory / "pitch-test-report.json");
    reportFile << report.str();
    if (!reportFile.good()) {
        return 17;
    }

    std::cout << "Pitch/formant tests passed. Balanced latency: " << reportedLatency
              << " samples. Estimated live DSP single-core: " << std::fixed << std::setprecision(2)
              << aggregateLivePercent << "%.\n";
    return 0;
}
