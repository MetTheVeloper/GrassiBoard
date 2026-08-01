#include "grassiboard/audio_engine.h"

#include "device_enumerator.h"
#include "wasapi_engine.h"

#include <cstring>
#include <new>
#include <string>

namespace {
constexpr std::uint32_t kApiVersion = 2;
constexpr char kEngineVersion[] = "0.2.0";

gb_result WriteUtf8Result(
    const std::string& value,
    char* const buffer,
    const std::uint32_t capacity,
    std::uint32_t* const required) noexcept
{
    if (required == nullptr) {
        return GB_ERROR_INVALID_ARGUMENT;
    }

    const auto requiredBytes = static_cast<std::uint32_t>(value.size() + 1U);
    *required = requiredBytes;
    if (buffer == nullptr || capacity == 0U) {
        return GB_OK;
    }
    if (capacity < requiredBytes) {
        return GB_ERROR_BUFFER_TOO_SMALL;
    }

    std::memcpy(buffer, value.c_str(), requiredBytes);
    return GB_OK;
}

gb_result Enumerate(const EDataFlow flow, char* const buffer, const std::uint32_t capacity,
    std::uint32_t* const required) noexcept
{
    try {
        std::string json;
        const gb_result result = grassiboard::EnumerateAudioDevicesJson(flow, json);
        if (result != GB_OK) {
            return result;
        }
        return WriteUtf8Result(json, buffer, capacity, required);
    }
    catch (...) {
        return GB_ERROR_INTERNAL;
    }
}
}

std::uint32_t GB_CALL gb_get_api_version() noexcept
{
    return kApiVersion;
}

const char* GB_CALL gb_get_version() noexcept
{
    return kEngineVersion;
}

std::uint32_t GB_CALL gb_engine_ping(const std::uint32_t value) noexcept
{
    return value ^ 0x47524244U;
}

gb_result GB_CALL gb_enumerate_input_devices(
    char* const buffer,
    const std::uint32_t capacity,
    std::uint32_t* const required) noexcept
{
    return Enumerate(eCapture, buffer, capacity, required);
}

gb_result GB_CALL gb_enumerate_output_devices(
    char* const buffer,
    const std::uint32_t capacity,
    std::uint32_t* const required) noexcept
{
    return Enumerate(eRender, buffer, capacity, required);
}

gb_result GB_CALL gb_engine_create(
    const std::uint32_t requested_api_version,
    gb_engine_handle* const engine) noexcept
{
    if (engine == nullptr || requested_api_version != kApiVersion) {
        return GB_ERROR_INVALID_ARGUMENT;
    }

    grassiboard::WasapiEngine* instance = nullptr;
    try {
        instance = new grassiboard::WasapiEngine();
    }
    catch (const std::bad_alloc&) {
        return GB_ERROR_OUT_OF_MEMORY;
    }
    catch (...) {
        return GB_ERROR_INTERNAL;
    }

    *engine = instance;
    return GB_OK;
}

void GB_CALL gb_engine_destroy(const gb_engine_handle engine) noexcept
{
    delete static_cast<grassiboard::WasapiEngine*>(engine);
}

gb_result GB_CALL gb_engine_start(
    const gb_engine_handle engine,
    const char* const input_device_id_utf8,
    const char* const monitor_device_id_utf8) noexcept
{
    if (engine == nullptr || input_device_id_utf8 == nullptr || monitor_device_id_utf8 == nullptr) {
        return GB_ERROR_INVALID_ARGUMENT;
    }

    try {
        return static_cast<grassiboard::WasapiEngine*>(engine)->Start(
            input_device_id_utf8, monitor_device_id_utf8);
    }
    catch (...) {
        return GB_ERROR_INTERNAL;
    }
}

gb_result GB_CALL gb_engine_stop(const gb_engine_handle engine) noexcept
{
    if (engine == nullptr) {
        return GB_ERROR_INVALID_ARGUMENT;
    }

    try {
        return static_cast<grassiboard::WasapiEngine*>(engine)->Stop();
    }
    catch (...) {
        return GB_ERROR_INTERNAL;
    }
}

gb_result GB_CALL gb_get_audio_statistics(
    const gb_engine_handle engine,
    gb_audio_statistics* const statistics) noexcept
{
    if (engine == nullptr || statistics == nullptr) {
        return GB_ERROR_INVALID_ARGUMENT;
    }

    static_cast<grassiboard::WasapiEngine*>(engine)->GetStatistics(*statistics);
    return GB_OK;
}

gb_result GB_CALL gb_get_last_error(
    const gb_engine_handle engine,
    char* const buffer,
    const std::uint32_t capacity,
    std::uint32_t* const required) noexcept
{
    if (engine == nullptr) {
        return GB_ERROR_INVALID_ARGUMENT;
    }

    try {
        return WriteUtf8Result(
            static_cast<grassiboard::WasapiEngine*>(engine)->GetLastError(),
            buffer,
            capacity,
            required);
    }
    catch (...) {
        return GB_ERROR_INTERNAL;
    }
}
