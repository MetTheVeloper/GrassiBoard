#include "wasapi_engine.h"

#include "device_enumerator.h"

#include <audioclient.h>
#include <avrt.h>
#include <mmdeviceapi.h>
#include <wrl/client.h>

#include <algorithm>
#include <array>
#include <cmath>
#include <cstring>
#include <limits>
#include <sstream>

namespace grassiboard {
namespace {
using Microsoft::WRL::ComPtr;

constexpr std::uint32_t kSampleRate = 48'000U;
constexpr std::uint16_t kCaptureChannels = 1U;
constexpr std::uint16_t kRenderChannels = 2U;
constexpr std::size_t kRingCapacityFrames = kSampleRate * 2U;
constexpr std::size_t kMediaCapacityFrames = kSampleRate * 4U;
#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
constexpr std::size_t kMonitorTapCapacityFrames = kSampleRate * 2U;
constexpr std::size_t kRemoteInputCapacityFrames = kSampleRate / 4U; // 250 ms hard native bound
#endif

WAVEFORMATEX MakeFloatFormat(const std::uint16_t channels) noexcept
{
    WAVEFORMATEX format{};
    format.wFormatTag = WAVE_FORMAT_IEEE_FLOAT;
    format.nChannels = channels;
    format.nSamplesPerSec = kSampleRate;
    format.wBitsPerSample = 32U;
    format.nBlockAlign = static_cast<WORD>(channels * sizeof(float));
    format.nAvgBytesPerSec = format.nSamplesPerSec * format.nBlockAlign;
    format.cbSize = 0U;
    return format;
}

HRESULT GetDevice(
    IMMDeviceEnumerator* const enumerator,
    const EDataFlow flow,
    const std::wstring& id,
    IMMDevice** const device) noexcept
{
    if (id.empty()) {
        HRESULT result = enumerator->GetDefaultAudioEndpoint(flow, eCommunications, device);
        if (FAILED(result)) {
            result = enumerator->GetDefaultAudioEndpoint(flow, eConsole, device);
        }
        return result;
    }

    return enumerator->GetDevice(id.c_str(), device);
}

HRESULT InitializeClient(
    IMMDevice* const device,
    const WAVEFORMATEX& format,
    HANDLE eventHandle,
    IAudioClient** const client) noexcept
{
    ComPtr<IAudioClient> audioClient;
    HRESULT result = device->Activate(
        __uuidof(IAudioClient), CLSCTX_ALL, nullptr, reinterpret_cast<void**>(audioClient.GetAddressOf()));
    if (FAILED(result)) {
        return result;
    }

    constexpr DWORD flags = AUDCLNT_STREAMFLAGS_EVENTCALLBACK |
        AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM |
        AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY |
        AUDCLNT_STREAMFLAGS_NOPERSIST;
    result = audioClient->Initialize(
        AUDCLNT_SHAREMODE_SHARED, flags, 0, 0, &format, nullptr);
    if (FAILED(result)) {
        return result;
    }

    result = audioClient->SetEventHandle(eventHandle);
    if (FAILED(result)) {
        return result;
    }

    *client = audioClient.Detach();
    return S_OK;
}

float SafeSample(const float sample) noexcept
{
    return std::isfinite(sample) ? std::clamp(sample, -1.0F, 1.0F) : 0.0F;
}
}

FloatRingBuffer::FloatRingBuffer(const std::size_t capacity)
    : samples_(capacity, 0.0F)
{
}

void FloatRingBuffer::Reset() noexcept
{
    read_index_ = 0;
    write_index_ = 0;
    size_ = 0;
}

bool FloatRingBuffer::Push(const float sample) noexcept
{
    if (size_ == samples_.size()) {
        return false;
    }
    samples_[write_index_] = sample;
    write_index_ = (write_index_ + 1U) % samples_.size();
    ++size_;
    return true;
}

bool FloatRingBuffer::Pop(float& sample) noexcept
{
    if (size_ == 0U) {
        sample = 0.0F;
        return false;
    }
    sample = samples_[read_index_];
    read_index_ = (read_index_ + 1U) % samples_.size();
    --size_;
    return true;
}

std::size_t FloatRingBuffer::Size() const noexcept
{
    return size_;
}

WasapiEngine::WasapiEngine()
    : ring_buffer_(kRingCapacityFrames)
    , media_stream_(kMediaCapacityFrames)
#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
    , monitor_tap_(kMonitorTapCapacityFrames)
    , voice_monitor_tap_(kMonitorTapCapacityFrames)
    , remote_input_(kRemoteInputCapacityFrames)
#endif
{
}

WasapiEngine::~WasapiEngine()
{
    Stop();
}

gb_result WasapiEngine::Start(const std::string& inputDeviceId, const std::string& monitorDeviceId)
{
    std::scoped_lock lock(control_mutex_);
    if (worker_.joinable() || running_.load(std::memory_order_acquire)) {
        return GB_ERROR_ALREADY_RUNNING;
    }
    if (inputDeviceId.empty() || monitorDeviceId.empty()) {
        return GB_ERROR_INVALID_ARGUMENT;
    }

    input_device_id_ = Utf8ToWide(inputDeviceId);
    monitor_device_id_ = Utf8ToWide(monitorDeviceId);
    if (input_device_id_.empty() || monitor_device_id_.empty()) {
        return GB_ERROR_INVALID_ARGUMENT;
    }

    stop_event_ = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (stop_event_ == nullptr) {
        last_hresult_.store(HRESULT_FROM_WIN32(::GetLastError()), std::memory_order_release);
        return GB_ERROR_INTERNAL;
    }

    start_complete_ = false;
    start_result_ = GB_ERROR_INTERNAL;
    ResetStatistics();
    try {
        worker_ = std::thread(&WasapiEngine::Worker, this);
    }
    catch (...) {
        CloseHandle(stop_event_);
        stop_event_ = nullptr;
        return GB_ERROR_INTERNAL;
    }

    std::unique_lock startLock(start_mutex_);
    start_condition_.wait(startLock, [this] { return start_complete_; });
    const gb_result result = start_result_;
    startLock.unlock();

    if (result != GB_OK) {
        if (worker_.joinable()) {
            worker_.join();
        }
        CloseHandle(stop_event_);
        stop_event_ = nullptr;
    }
    return result;
}

gb_result WasapiEngine::Stop()
{
    std::scoped_lock lock(control_mutex_);
    if (!worker_.joinable()) {
        running_.store(false, std::memory_order_release);
        return GB_OK;
    }

    SetEvent(stop_event_);
    worker_.join();
    CloseHandle(stop_event_);
    stop_event_ = nullptr;
    running_.store(false, std::memory_order_release);
    return GB_OK;
}

void WasapiEngine::SetPitchSemitones(const float semitones) noexcept
{
    pitch_semitones_.store(std::clamp(semitones, -12.0F, 12.0F), std::memory_order_release);
    UpdatePitchTarget();
}

void WasapiEngine::SetPitchCents(const float cents) noexcept
{
    pitch_cents_.store(std::clamp(cents, -100.0F, 100.0F), std::memory_order_release);
    UpdatePitchTarget();
}

void WasapiEngine::SetPitchBypass(const bool bypass) noexcept
{
    pitch_processor_.SetBypass(bypass);
}

void WasapiEngine::SetFormantSemitones(const float semitones) noexcept
{
    pitch_processor_.SetFormantSemitones(semitones);
}

void WasapiEngine::SetFormantPreservation(const bool preserve) noexcept
{
    pitch_processor_.SetFormantPreservation(preserve);
}

void WasapiEngine::SetPitchQuality(const PitchQualityMode mode) noexcept
{
    pitch_processor_.SetQualityMode(mode);
    UpdateMediaAlignment();
}

gb_result WasapiEngine::LoadSoundClip(
    const std::uint64_t key,
    const float* const stereoSamples,
    const std::uint64_t frameCount)
{
    return soundboard_mixer_.LoadClip(key, stereoSamples, frameCount);
}

gb_result WasapiEngine::PlaySoundClip(
    const std::uint64_t key,
    const float volume,
    const bool loop,
    const bool restart) noexcept
{
    return soundboard_mixer_.Play(key, volume, loop, restart);
}

gb_result WasapiEngine::StopSoundClip(const std::uint64_t key) noexcept
{
    return soundboard_mixer_.Stop(key);
}

gb_result WasapiEngine::StopAllSounds() noexcept
{
    return soundboard_mixer_.StopAll();
}

gb_result WasapiEngine::WriteMedia(
    const float* const stereoSamples,
    const std::uint32_t frameCount,
    std::uint32_t& acceptedFrames) noexcept
{
    if (stereoSamples == nullptr || frameCount == 0U) {
        acceptedFrames = 0U;
        return GB_ERROR_INVALID_ARGUMENT;
    }
    acceptedFrames = media_stream_.Write(stereoSamples, frameCount);
    return GB_OK;
}

void WasapiEngine::SetMediaActive(const bool active) noexcept
{
    if (active) {
        UpdateMediaAlignment();
    }
    media_stream_.SetActive(active);
}

void WasapiEngine::ClearMedia() noexcept
{
    media_stream_.Clear();
}

void WasapiEngine::SetMediaMonitorLatency(const std::uint32_t latencyFrames) noexcept
{
    media_monitor_latency_frames_.store(
        std::min<std::uint32_t>(latencyFrames, kSampleRate), std::memory_order_release);
    UpdateMediaAlignment();
}

void WasapiEngine::SetMicrophoneMuted(const bool muted) noexcept
{
    microphone_muted_.store(muted, std::memory_order_release);
}

void WasapiEngine::SetMixerSettings(const gb_mixer_settings& settings) noexcept
{
    mixer_processor_.SetMicGainDb(settings.mic_gain_db);
    mixer_processor_.SetSoundboardGainDb(settings.soundboard_gain_db);
    mixer_processor_.SetMasterGainDb(settings.master_gain_db);
    mixer_processor_.SetNoiseGate(settings.gate_enabled != 0U, settings.gate_threshold_db);
    mixer_processor_.SetCompressor(
        settings.compressor_enabled != 0U,
        settings.compressor_threshold_db,
        settings.compressor_ratio);
    mixer_processor_.SetLimiter(settings.limiter_enabled != 0U, settings.limiter_ceiling_db);
    mixer_processor_.SetDucking(settings.ducking_enabled != 0U, settings.ducking_amount_db);
    mixer_processor_.SetClippingProtection(settings.clipping_protection_enabled != 0U);
    pitch_processor_.SetWetDryMix(settings.pitch_wet_mix);
}

void WasapiEngine::UpdatePitchTarget() noexcept
{
    const float semitones = pitch_semitones_.load(std::memory_order_acquire);
    const float cents = pitch_cents_.load(std::memory_order_acquire);
    pitch_processor_.SetPitchSemitones(semitones + cents / 100.0F);
}

void WasapiEngine::UpdateMediaAlignment() noexcept
{
    const std::uint32_t pitchLatency = pitch_processor_.GetLatencySamples();
#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
    const bool remoteInput =
        active_input_source_mode_.load(std::memory_order_relaxed) == GB_INPUT_SOURCE_REMOTE;
    const std::uint64_t sourceBufferFrames = remoteInput
        ? remote_input_.FillFrames()
        : static_cast<std::uint64_t>(capture_buffer_frames_.load(std::memory_order_relaxed)) +
            ring_buffer_fill_frames_.load(std::memory_order_relaxed);
#else
    const std::uint64_t sourceBufferFrames =
        static_cast<std::uint64_t>(capture_buffer_frames_.load(std::memory_order_relaxed)) +
        ring_buffer_fill_frames_.load(std::memory_order_relaxed);
#endif
    const std::uint64_t microphonePath = sourceBufferFrames + pitchLatency;
    const std::uint64_t monitorPath = media_monitor_latency_frames_.load(std::memory_order_relaxed);
    const std::uint64_t aligned = std::min<std::uint64_t>(
        microphonePath + monitorPath, static_cast<std::uint64_t>(kMediaCapacityFrames - 1U));
    media_alignment_pitch_frames_.store(pitchLatency, std::memory_order_release);
    media_alignment_frames_.store(static_cast<std::uint32_t>(aligned), std::memory_order_release);
}

void WasapiEngine::GetStatistics(gb_audio_statistics& statistics) const noexcept
{
    statistics = {};
    statistics.struct_size = sizeof(gb_audio_statistics);
    statistics.running = running_.load(std::memory_order_acquire) ? 1U : 0U;
    statistics.sample_rate = kSampleRate;
    statistics.capture_buffer_frames = capture_buffer_frames_.load(std::memory_order_relaxed);
    statistics.render_buffer_frames = render_buffer_frames_.load(std::memory_order_relaxed);
    statistics.ring_buffer_fill_frames = ring_buffer_fill_frames_.load(std::memory_order_relaxed);
    statistics.pitch_latency_samples = pitch_processor_.GetLatencySamples();
    statistics.captured_frames = captured_frames_.load(std::memory_order_relaxed);
    statistics.rendered_frames = rendered_frames_.load(std::memory_order_relaxed);
    statistics.underrun_count = underrun_count_.load(std::memory_order_relaxed);
    statistics.overrun_count = overrun_count_.load(std::memory_order_relaxed);
    statistics.discontinuity_count = discontinuity_count_.load(std::memory_order_relaxed);
    statistics.input_peak = input_peak_.load(std::memory_order_relaxed);
    statistics.input_rms = input_rms_.load(std::memory_order_relaxed);
    statistics.output_peak = output_peak_.load(std::memory_order_relaxed);
    statistics.output_rms = output_rms_.load(std::memory_order_relaxed);
    statistics.soundboard_peak = soundboard_peak_.load(std::memory_order_relaxed);
    statistics.soundboard_rms = soundboard_rms_.load(std::memory_order_relaxed);
    statistics.master_peak = master_peak_.load(std::memory_order_relaxed);
    statistics.master_rms = master_rms_.load(std::memory_order_relaxed);
    statistics.active_sound_count = soundboard_mixer_.ActiveVoiceCount();
    statistics.microphone_muted = microphone_muted_.load(std::memory_order_relaxed) ? 1U : 0U;
    statistics.media_buffer_fill_frames = media_stream_.FillFrames();
    statistics.media_buffer_capacity_frames = media_stream_.CapacityFrames();
    statistics.media_underrun_count = media_underrun_count_.load(std::memory_order_relaxed);
    statistics.media_peak = media_peak_.load(std::memory_order_relaxed);
    statistics.media_rms = media_rms_.load(std::memory_order_relaxed);
    statistics.media_active = media_stream_.IsActive() ? 1U : 0U;
    statistics.media_alignment_frames = media_alignment_frames_.load(std::memory_order_relaxed);
}

#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
void WasapiEngine::SetMonitorTapEnabled(const bool enabled) noexcept
{
    if (enabled) {
        monitor_tap_.Reset();
    }
    monitor_tap_enabled_.store(enabled, std::memory_order_release);
}

void WasapiEngine::ClearMonitorTap() noexcept
{
    monitor_tap_.Reset();
}

std::uint32_t WasapiEngine::ReadMonitorTap(
    float* const stereoSamples,
    const std::uint32_t capacityFrames) noexcept
{
    return monitor_tap_.Read(stereoSamples, capacityFrames);
}

void WasapiEngine::GetMonitorTapStatistics(gb_monitor_tap_statistics& statistics) const noexcept
{
    statistics = {};
    statistics.struct_size = sizeof(gb_monitor_tap_statistics);
    statistics.enabled = monitor_tap_enabled_.load(std::memory_order_acquire) ? 1U : 0U;
    statistics.fill_frames = monitor_tap_.FillFrames();
    statistics.capacity_frames = monitor_tap_.CapacityFrames();
    statistics.overrun_count = monitor_tap_.OverrunCount();
}

void WasapiEngine::SetVoiceMonitorTapEnabled(const bool enabled) noexcept
{
    if (enabled) {
        voice_monitor_tap_.Reset();
    }
    voice_monitor_tap_enabled_.store(enabled, std::memory_order_release);
}

void WasapiEngine::ClearVoiceMonitorTap() noexcept
{
    voice_monitor_tap_.Reset();
}

std::uint32_t WasapiEngine::ReadVoiceMonitorTap(
    float* const stereoSamples,
    const std::uint32_t capacityFrames) noexcept
{
    return voice_monitor_tap_.Read(stereoSamples, capacityFrames);
}

void WasapiEngine::GetVoiceMonitorTapStatistics(gb_monitor_tap_statistics& statistics) const noexcept
{
    statistics = {};
    statistics.struct_size = sizeof(gb_monitor_tap_statistics);
    statistics.enabled = voice_monitor_tap_enabled_.load(std::memory_order_acquire) ? 1U : 0U;
    statistics.fill_frames = voice_monitor_tap_.FillFrames();
    statistics.capacity_frames = voice_monitor_tap_.CapacityFrames();
    statistics.overrun_count = voice_monitor_tap_.OverrunCount();
}

void WasapiEngine::SetInputSourceMode(const std::uint32_t sourceMode) noexcept
{
    input_source_mode_.store(
        sourceMode == GB_INPUT_SOURCE_REMOTE ? GB_INPUT_SOURCE_REMOTE : GB_INPUT_SOURCE_WINDOWS,
        std::memory_order_release);
}

std::uint32_t WasapiEngine::WriteRemoteInput(
    const float* const monoSamples,
    const std::uint32_t frameCount) noexcept
{
    return remote_input_.Write(monoSamples, frameCount);
}

void WasapiEngine::ResetRemoteInput() noexcept
{
    remote_input_.Reset();
}

void WasapiEngine::GetRemoteInputStatistics(gb_remote_input_statistics& statistics) const noexcept
{
    statistics = {};
    statistics.struct_size = sizeof(gb_remote_input_statistics);
    statistics.requested_source_mode = input_source_mode_.load(std::memory_order_acquire);
    statistics.active_source_mode = active_input_source_mode_.load(std::memory_order_acquire);
    statistics.fill_frames = remote_input_.FillFrames();
    statistics.capacity_frames = remote_input_.CapacityFrames();
    statistics.pushed_frames = remote_input_.PushedFrames();
    statistics.consumed_frames = remote_input_.ConsumedFrames();
    statistics.underrun_frames = remote_input_.UnderrunFrames();
    statistics.overrun_frames = remote_input_.OverrunFrames();
}
#endif

std::string WasapiEngine::GetLastError() const
{
    const HRESULT result = last_hresult_.load(std::memory_order_acquire);
    if (SUCCEEDED(result)) {
        return {};
    }

    LPWSTR message = nullptr;
    const DWORD length = FormatMessageW(
        FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
        nullptr,
        static_cast<DWORD>(result),
        0,
        reinterpret_cast<LPWSTR>(&message),
        0,
        nullptr);

    std::ostringstream stream;
    stream << "HRESULT 0x" << std::hex << static_cast<std::uint32_t>(result);
    if (length > 0U && message != nullptr) {
        std::wstring text(message, length);
        while (!text.empty() && (text.back() == L'\r' || text.back() == L'\n' || text.back() == L' ')) {
            text.pop_back();
        }
        stream << ": " << WideToUtf8(text);
    }
    if (message != nullptr) {
        LocalFree(message);
    }
    return stream.str();
}

void WasapiEngine::ResetStatistics() noexcept
{
    ring_buffer_.Reset();
    last_hresult_.store(S_OK, std::memory_order_relaxed);
    capture_buffer_frames_.store(0U, std::memory_order_relaxed);
    render_buffer_frames_.store(0U, std::memory_order_relaxed);
    ring_buffer_fill_frames_.store(0U, std::memory_order_relaxed);
    captured_frames_.store(0U, std::memory_order_relaxed);
    rendered_frames_.store(0U, std::memory_order_relaxed);
    underrun_count_.store(0U, std::memory_order_relaxed);
    overrun_count_.store(0U, std::memory_order_relaxed);
    discontinuity_count_.store(0U, std::memory_order_relaxed);
    input_peak_.store(0.0F, std::memory_order_relaxed);
    input_rms_.store(0.0F, std::memory_order_relaxed);
    output_peak_.store(0.0F, std::memory_order_relaxed);
    output_rms_.store(0.0F, std::memory_order_relaxed);
    soundboard_peak_.store(0.0F, std::memory_order_relaxed);
    soundboard_rms_.store(0.0F, std::memory_order_relaxed);
    master_peak_.store(0.0F, std::memory_order_relaxed);
    master_rms_.store(0.0F, std::memory_order_relaxed);
    media_underrun_count_.store(0U, std::memory_order_relaxed);
    media_peak_.store(0.0F, std::memory_order_relaxed);
    media_rms_.store(0.0F, std::memory_order_relaxed);
    media_alignment_frames_.store(0U, std::memory_order_relaxed);
    media_alignment_pitch_frames_.store(0U, std::memory_order_relaxed);
    media_stream_.Clear();
    media_stream_.SetActive(false);
    soundboard_mixer_.ResetPlayback();
#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
    monitor_tap_.Reset();
    voice_monitor_tap_.Reset();
    input_source_mode_.store(GB_INPUT_SOURCE_WINDOWS, std::memory_order_release);
    active_input_source_mode_.store(GB_INPUT_SOURCE_WINDOWS, std::memory_order_release);
    remote_input_.Reset();
#endif
    mixer_processor_.Reset();
}

void WasapiEngine::SignalStart(const gb_result result, const HRESULT detail) noexcept
{
    if (FAILED(detail)) {
        last_hresult_.store(detail, std::memory_order_release);
    }
    {
        std::scoped_lock lock(start_mutex_);
        start_result_ = result;
        start_complete_ = true;
    }
    start_condition_.notify_one();
}

void WasapiEngine::Worker() noexcept
{
    const HRESULT apartmentResult = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    if (FAILED(apartmentResult)) {
        SignalStart(GB_ERROR_COM, apartmentResult);
        return;
    }

    HANDLE captureEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    HANDLE renderEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    ComPtr<IMMDeviceEnumerator> enumerator;
    ComPtr<IMMDevice> captureDevice;
    ComPtr<IMMDevice> renderDevice;
    ComPtr<IAudioClient> captureClient;
    ComPtr<IAudioClient> renderClient;
    ComPtr<IAudioCaptureClient> captureService;
    ComPtr<IAudioRenderClient> renderService;
    HRESULT result = S_OK;

    if (captureEvent == nullptr || renderEvent == nullptr) {
        result = HRESULT_FROM_WIN32(::GetLastError());
    }
    if (SUCCEEDED(result)) {
        result = CoCreateInstance(
            __uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL, IID_PPV_ARGS(&enumerator));
    }
    if (SUCCEEDED(result)) {
        result = GetDevice(enumerator.Get(), eCapture, input_device_id_, &captureDevice);
    }
    if (SUCCEEDED(result)) {
        result = GetDevice(enumerator.Get(), eRender, monitor_device_id_, &renderDevice);
    }

    const WAVEFORMATEX captureFormat = MakeFloatFormat(kCaptureChannels);
    const WAVEFORMATEX renderFormat = MakeFloatFormat(kRenderChannels);
    if (SUCCEEDED(result)) {
        result = InitializeClient(captureDevice.Get(), captureFormat, captureEvent, &captureClient);
    }
    if (SUCCEEDED(result)) {
        result = InitializeClient(renderDevice.Get(), renderFormat, renderEvent, &renderClient);
    }
    if (SUCCEEDED(result)) {
        result = captureClient->GetService(IID_PPV_ARGS(&captureService));
    }
    if (SUCCEEDED(result)) {
        result = renderClient->GetService(IID_PPV_ARGS(&renderService));
    }

    UINT32 captureFrames = 0;
    UINT32 renderFrames = 0;
    if (SUCCEEDED(result)) {
        result = captureClient->GetBufferSize(&captureFrames);
    }
    if (SUCCEEDED(result)) {
        result = renderClient->GetBufferSize(&renderFrames);
    }
    if (SUCCEEDED(result)) {
        capture_buffer_frames_.store(captureFrames, std::memory_order_relaxed);
        render_buffer_frames_.store(renderFrames, std::memory_order_relaxed);

        try {
            const UINT32 maximumBlockFrames = std::max(captureFrames, renderFrames);
            pitch_input_buffer_.resize(maximumBlockFrames);
            pitch_output_buffer_.resize(maximumBlockFrames);
            if (!pitch_processor_.Prepare(kSampleRate, kCaptureChannels, maximumBlockFrames)) {
                result = E_FAIL;
            }
            mixer_processor_.Prepare(kSampleRate);
        }
        catch (...) {
            result = E_OUTOFMEMORY;
        }
    }

    if (SUCCEEDED(result)) {

        BYTE* initialBuffer = nullptr;
        result = renderService->GetBuffer(renderFrames, &initialBuffer);
        if (SUCCEEDED(result)) {
            result = renderService->ReleaseBuffer(renderFrames, AUDCLNT_BUFFERFLAGS_SILENT);
        }
    }
    if (SUCCEEDED(result)) {
        result = captureClient->Start();
    }
    if (SUCCEEDED(result)) {
        result = renderClient->Start();
    }

    if (FAILED(result)) {
        if (renderClient) renderClient->Stop();
        if (captureClient) captureClient->Stop();
        SignalStart(GB_ERROR_AUDIO_CLIENT, result);
        if (captureEvent != nullptr) CloseHandle(captureEvent);
        if (renderEvent != nullptr) CloseHandle(renderEvent);
        renderService.Reset();
        captureService.Reset();
        renderClient.Reset();
        captureClient.Reset();
        renderDevice.Reset();
        captureDevice.Reset();
        enumerator.Reset();
        CoUninitialize();
        return;
    }

    running_.store(true, std::memory_order_release);
    SignalStart(GB_OK, S_OK);

    DWORD mmcssTaskIndex = 0;
    HANDLE mmcssHandle = AvSetMmThreadCharacteristicsW(L"Pro Audio", &mmcssTaskIndex);
    const std::array<HANDLE, 3> events{stop_event_, captureEvent, renderEvent};
    bool active = true;
#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
    std::uint32_t workerInputSourceMode =
        input_source_mode_.load(std::memory_order_acquire);
    active_input_source_mode_.store(workerInputSourceMode, std::memory_order_release);
#endif

    while (active) {
        const DWORD waitResult = WaitForMultipleObjects(
            static_cast<DWORD>(events.size()), events.data(), FALSE, 2000U);
        if (waitResult == WAIT_OBJECT_0) {
            break;
        }

        if (waitResult == WAIT_OBJECT_0 + 1U) {
            UINT32 packetFrames = 0;
            result = captureService->GetNextPacketSize(&packetFrames);
            while (SUCCEEDED(result) && packetFrames > 0U) {
                BYTE* data = nullptr;
                DWORD flags = 0;
                UINT64 devicePosition = 0;
                UINT64 performancePosition = 0;
                result = captureService->GetBuffer(
                    &data, &packetFrames, &flags, &devicePosition, &performancePosition);
                if (FAILED(result)) {
                    break;
                }
                if (packetFrames > pitch_input_buffer_.size() || packetFrames > pitch_output_buffer_.size()) {
                    captureService->ReleaseBuffer(packetFrames);
                    result = E_BOUNDS;
                    break;
                }

                if ((flags & AUDCLNT_BUFFERFLAGS_DATA_DISCONTINUITY) != 0U) {
                    discontinuity_count_.fetch_add(1U, std::memory_order_relaxed);
                }

                const bool silent = (flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0U || data == nullptr;
                const float* samples = reinterpret_cast<const float*>(data);
#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
                const bool useWindowsInput =
                    active_input_source_mode_.load(std::memory_order_relaxed) == GB_INPUT_SOURCE_WINDOWS;
#else
                constexpr bool useWindowsInput = true;
#endif
                if (useWindowsInput) {
                    bool overrun = false;
                    for (UINT32 frame = 0; frame < packetFrames; ++frame) {
                        const float sample = silent ? 0.0F : SafeSample(samples[frame]);
                        pitch_input_buffer_[frame] = sample;
                    }

                    pitch_processor_.Process(
                        pitch_input_buffer_.data(), pitch_output_buffer_.data(), packetFrames);
                    for (UINT32 frame = 0; frame < packetFrames; ++frame) {
                        if (!ring_buffer_.Push(SafeSample(pitch_output_buffer_[frame]))) {
                            overrun = true;
                        }
                    }
                    if (overrun) {
                        overrun_count_.fetch_add(1U, std::memory_order_relaxed);
                    }
                    captured_frames_.fetch_add(packetFrames, std::memory_order_relaxed);
                    ring_buffer_fill_frames_.store(
                        static_cast<std::uint32_t>(ring_buffer_.Size()), std::memory_order_relaxed);
                }

                result = captureService->ReleaseBuffer(packetFrames);
                if (FAILED(result)) {
                    break;
                }
                result = captureService->GetNextPacketSize(&packetFrames);
            }
        }
        else if (waitResult == WAIT_OBJECT_0 + 2U) {
            UINT32 padding = 0;
            result = renderClient->GetCurrentPadding(&padding);
            if (SUCCEEDED(result) && padding <= renderFrames) {
                const UINT32 available = renderFrames - padding;
                if (available > 0U) {
                    BYTE* data = nullptr;
                    result = renderService->GetBuffer(available, &data);
                    if (SUCCEEDED(result)) {
                        float* samples = reinterpret_cast<float*>(data);
                        float boardPeak = 0.0F;
                        float microphonePeak = 0.0F;
                        float masterPeak = 0.0F;
                        float mediaPeak = 0.0F;
                        double microphoneSquareSum = 0.0;
                        double boardSquareSum = 0.0;
                        double masterSquareSum = 0.0;
                        double mediaSquareSum = 0.0;
                        bool underrun = false;
                        bool mediaUnderrun = false;
                        mixer_processor_.BeginBlock();
                        if (media_stream_.IsActive() &&
                            pitch_processor_.GetLatencySamples() !=
                                media_alignment_pitch_frames_.load(std::memory_order_relaxed)) {
                            UpdateMediaAlignment();
                        }
                        media_stream_.SynchronizeDelay(
                            media_alignment_frames_.load(std::memory_order_relaxed));
#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
                        const std::uint32_t requestedInputSourceMode =
                            input_source_mode_.load(std::memory_order_acquire);
                        if (requestedInputSourceMode != workerInputSourceMode) {
                            // Both source branches are already 48 kHz mono and enter the same
                            // prepared Voice DSP. Do not reset/reconfigure Pitch here: source
                            // switching runs on the realtime worker and must stay allocation-free.
                            // Clearing the physical staging ring is sufficient to prevent stale
                            // Windows frames from reappearing after the atomic source handoff.
                            ring_buffer_.Reset();
                            workerInputSourceMode = requestedInputSourceMode;
                            active_input_source_mode_.store(workerInputSourceMode, std::memory_order_release);
                            UpdateMediaAlignment();
                        }

                        std::uint32_t remoteReadFrames = 0U;
                        if (workerInputSourceMode == GB_INPUT_SOURCE_REMOTE) {
                            remoteReadFrames = remote_input_.Read(pitch_input_buffer_.data(), available);
                            pitch_processor_.Process(
                                pitch_input_buffer_.data(), pitch_output_buffer_.data(), available);
                            captured_frames_.fetch_add(remoteReadFrames, std::memory_order_relaxed);
                            if (remoteReadFrames < available) {
                                underrun = true;
                            }
                        }
#endif
                        for (UINT32 frame = 0; frame < available; ++frame) {
                            float microphoneSample = 0.0F;
#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
                            if (workerInputSourceMode == GB_INPUT_SOURCE_REMOTE) {
                                microphoneSample = SafeSample(pitch_output_buffer_[frame]);
                            }
                            else
#endif
                            if (!ring_buffer_.Pop(microphoneSample)) {
                                underrun = true;
                            }
                            if (microphone_muted_.load(std::memory_order_relaxed)) {
                                microphoneSample = 0.0F;
                            }
#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
                            if (voice_monitor_tap_enabled_.load(std::memory_order_relaxed)) {
                                // My Voice tap: post Pitch/Formant + Mic Mute, but pre
                                // Program Mic Gain/dynamics/Master. Duplicate mono into
                                // stereo so the managed monitor worker can share the same
                                // bounded 48 kHz stereo framing as the other source taps.
                                const float processedVoice = SafeSample(microphoneSample);
                                voice_monitor_tap_.Push(processedVoice, processedVoice);
                            }
#endif

                            float boardLeft = 0.0F;
                            float boardRight = 0.0F;
                            soundboard_mixer_.MixFrame(boardLeft, boardRight);
#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
                            if (monitor_tap_enabled_.load(std::memory_order_relaxed)) {
                                // Raw Soundboard source tap: post per-pad volume, pre Program
                                // Soundboard gain/master processing. The tap is write-only from
                                // this realtime thread and cannot alter the Program mix.
                                monitor_tap_.Push(SafeSample(boardLeft), SafeSample(boardRight));
                            }
#endif
                            float mediaLeft = 0.0F;
                            float mediaRight = 0.0F;
                            if (media_stream_.IsActive() && !media_stream_.Pop(mediaLeft, mediaRight)) {
                                mediaUnderrun = true;
                            }
                            const MixerFrame mixed = mixer_processor_.ProcessFrame(
                                microphoneSample, boardLeft, boardRight, mediaLeft, mediaRight);
                            const float masterLeft = SafeSample(mixed.left);
                            const float masterRight = SafeSample(mixed.right);

                            microphonePeak = std::max(microphonePeak, std::abs(mixed.microphone));
                            boardPeak = std::max({boardPeak, std::abs(mixed.board_left), std::abs(mixed.board_right)});
                            mediaPeak = std::max({mediaPeak, std::abs(mixed.media_left), std::abs(mixed.media_right)});
                            masterPeak = std::max({masterPeak, std::abs(masterLeft), std::abs(masterRight)});
                            microphoneSquareSum += static_cast<double>(mixed.microphone) * mixed.microphone;
                            boardSquareSum += (static_cast<double>(mixed.board_left) * mixed.board_left +
                                static_cast<double>(mixed.board_right) * mixed.board_right) * 0.5;
                            mediaSquareSum += (static_cast<double>(mixed.media_left) * mixed.media_left +
                                static_cast<double>(mixed.media_right) * mixed.media_right) * 0.5;
                            masterSquareSum += (static_cast<double>(masterLeft) * masterLeft +
                                static_cast<double>(masterRight) * masterRight) * 0.5;
                            samples[frame * kRenderChannels] = masterLeft;
                            samples[frame * kRenderChannels + 1U] = masterRight;
                        }
                        if (underrun) {
                            underrun_count_.fetch_add(1U, std::memory_order_relaxed);
                        }
                        if (mediaUnderrun) {
                            media_underrun_count_.fetch_add(1U, std::memory_order_relaxed);
                        }
                        const float boardRms = available == 0U
                            ? 0.0F
                            : static_cast<float>(std::sqrt(boardSquareSum / static_cast<double>(available)));
                        const float masterRms = available == 0U
                            ? 0.0F
                            : static_cast<float>(std::sqrt(masterSquareSum / static_cast<double>(available)));
                        const float mediaRms = available == 0U
                            ? 0.0F
                            : static_cast<float>(std::sqrt(mediaSquareSum / static_cast<double>(available)));
                        const float microphoneRms = available == 0U
                            ? 0.0F
                            : static_cast<float>(std::sqrt(
                                microphoneSquareSum / static_cast<double>(available)));
                        input_peak_.store(microphonePeak, std::memory_order_relaxed);
                        input_rms_.store(microphoneRms, std::memory_order_relaxed);
                        output_peak_.store(masterPeak, std::memory_order_relaxed);
                        output_rms_.store(masterRms, std::memory_order_relaxed);
                        soundboard_peak_.store(boardPeak, std::memory_order_relaxed);
                        soundboard_rms_.store(boardRms, std::memory_order_relaxed);
                        master_peak_.store(masterPeak, std::memory_order_relaxed);
                        master_rms_.store(masterRms, std::memory_order_relaxed);
                        media_peak_.store(mediaPeak, std::memory_order_relaxed);
                        media_rms_.store(mediaRms, std::memory_order_relaxed);
                        rendered_frames_.fetch_add(available, std::memory_order_relaxed);
#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
                        ring_buffer_fill_frames_.store(
                            workerInputSourceMode == GB_INPUT_SOURCE_REMOTE
                                ? remote_input_.FillFrames()
                                : static_cast<std::uint32_t>(ring_buffer_.Size()),
                            std::memory_order_relaxed);
#else
                        ring_buffer_fill_frames_.store(
                            static_cast<std::uint32_t>(ring_buffer_.Size()), std::memory_order_relaxed);
#endif
                        result = renderService->ReleaseBuffer(available, 0U);
                    }
                }
            }
        }
        else if (waitResult == WAIT_FAILED) {
            result = HRESULT_FROM_WIN32(::GetLastError());
        }

        if (FAILED(result)) {
            last_hresult_.store(result, std::memory_order_release);
            active = false;
        }
    }

    renderClient->Stop();
    captureClient->Stop();
    running_.store(false, std::memory_order_release);
    input_peak_.store(0.0F, std::memory_order_relaxed);
    input_rms_.store(0.0F, std::memory_order_relaxed);
    output_peak_.store(0.0F, std::memory_order_relaxed);
    output_rms_.store(0.0F, std::memory_order_relaxed);
    soundboard_peak_.store(0.0F, std::memory_order_relaxed);
    soundboard_rms_.store(0.0F, std::memory_order_relaxed);
    master_peak_.store(0.0F, std::memory_order_relaxed);
    master_rms_.store(0.0F, std::memory_order_relaxed);
    media_peak_.store(0.0F, std::memory_order_relaxed);
    media_rms_.store(0.0F, std::memory_order_relaxed);
    media_stream_.Clear();
    media_stream_.SetActive(false);
    soundboard_mixer_.ResetPlayback();
#if defined(GRASSIBOARD_REMOTE_MONITOR_TAP)
    monitor_tap_.Reset();
    voice_monitor_tap_.Reset();
    input_source_mode_.store(GB_INPUT_SOURCE_WINDOWS, std::memory_order_release);
    active_input_source_mode_.store(GB_INPUT_SOURCE_WINDOWS, std::memory_order_release);
    remote_input_.Reset();
#endif

    if (mmcssHandle != nullptr) {
        AvRevertMmThreadCharacteristics(mmcssHandle);
    }
    CloseHandle(captureEvent);
    CloseHandle(renderEvent);
    renderService.Reset();
    captureService.Reset();
    renderClient.Reset();
    captureClient.Reset();
    renderDevice.Reset();
    captureDevice.Reset();
    enumerator.Reset();
    CoUninitialize();
}

}
