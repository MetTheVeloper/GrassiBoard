#include "device_enumerator.h"

#include <functiondiscoverykeys_devpkey.h>
#include <wrl/client.h>

#include <iomanip>
#include <sstream>
#include <string_view>

namespace grassiboard {
namespace {
using Microsoft::WRL::ComPtr;

class ComApartment final {
public:
    ComApartment() noexcept
    {
        result_ = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
        uninitialize_ = SUCCEEDED(result_);
    }

    ~ComApartment()
    {
        if (uninitialize_) {
            CoUninitialize();
        }
    }

    HRESULT Result() const noexcept { return result_; }

private:
    HRESULT result_ = E_FAIL;
    bool uninitialize_ = false;
};

std::string JsonEscape(const std::string_view value)
{
    std::ostringstream stream;
    for (const unsigned char character : value) {
        switch (character) {
        case '"': stream << "\\\""; break;
        case '\\': stream << "\\\\"; break;
        case '\b': stream << "\\b"; break;
        case '\f': stream << "\\f"; break;
        case '\n': stream << "\\n"; break;
        case '\r': stream << "\\r"; break;
        case '\t': stream << "\\t"; break;
        default:
            if (character < 0x20U) {
                stream << "\\u" << std::hex << std::setw(4) << std::setfill('0')
                       << static_cast<unsigned int>(character) << std::dec;
            }
            else {
                stream << static_cast<char>(character);
            }
            break;
        }
    }
    return stream.str();
}

std::wstring GetDeviceId(IMMDevice* const device)
{
    LPWSTR rawId = nullptr;
    if (FAILED(device->GetId(&rawId)) || rawId == nullptr) {
        return {};
    }
    std::wstring id(rawId);
    CoTaskMemFree(rawId);
    return id;
}

std::wstring GetFriendlyName(IMMDevice* const device)
{
    ComPtr<IPropertyStore> properties;
    if (FAILED(device->OpenPropertyStore(STGM_READ, &properties))) {
        return L"Unknown audio device";
    }

    PROPVARIANT value;
    PropVariantInit(&value);
    const HRESULT result = properties->GetValue(PKEY_Device_FriendlyName, &value);
    std::wstring name = L"Unknown audio device";
    if (SUCCEEDED(result) && value.vt == VT_LPWSTR && value.pwszVal != nullptr) {
        name = value.pwszVal;
    }
    PropVariantClear(&value);
    return name;
}

std::wstring GetContainerId(IMMDevice* const device)
{
    ComPtr<IPropertyStore> properties;
    if (FAILED(device->OpenPropertyStore(STGM_READ, &properties))) {
        return {};
    }

    PROPVARIANT value;
    PropVariantInit(&value);
    const HRESULT result = properties->GetValue(PKEY_Device_ContainerId, &value);
    std::wstring containerId;
    if (SUCCEEDED(result) && value.vt == VT_CLSID && value.puuid != nullptr) {
        wchar_t buffer[39]{};
        if (StringFromGUID2(*value.puuid, buffer, static_cast<int>(_countof(buffer))) > 0) {
            containerId = buffer;
        }
    }
    PropVariantClear(&value);
    return containerId;
}

std::wstring GetDefaultDeviceId(IMMDeviceEnumerator* const enumerator, const EDataFlow flow)
{
    ComPtr<IMMDevice> device;
    HRESULT result = enumerator->GetDefaultAudioEndpoint(flow, eCommunications, &device);
    if (FAILED(result)) {
        result = enumerator->GetDefaultAudioEndpoint(flow, eConsole, &device);
    }
    return SUCCEEDED(result) ? GetDeviceId(device.Get()) : std::wstring{};
}
}

std::wstring Utf8ToWide(const std::string& value)
{
    if (value.empty()) {
        return {};
    }
    const int size = MultiByteToWideChar(
        CP_UTF8, MB_ERR_INVALID_CHARS, value.data(), static_cast<int>(value.size()), nullptr, 0);
    if (size <= 0) {
        return {};
    }
    std::wstring result(static_cast<std::size_t>(size), L'\0');
    MultiByteToWideChar(
        CP_UTF8, MB_ERR_INVALID_CHARS, value.data(), static_cast<int>(value.size()), result.data(), size);
    return result;
}

std::string WideToUtf8(const std::wstring& value)
{
    if (value.empty()) {
        return {};
    }
    const int size = WideCharToMultiByte(
        CP_UTF8, WC_ERR_INVALID_CHARS, value.data(), static_cast<int>(value.size()), nullptr, 0, nullptr, nullptr);
    if (size <= 0) {
        return {};
    }
    std::string result(static_cast<std::size_t>(size), '\0');
    WideCharToMultiByte(
        CP_UTF8, WC_ERR_INVALID_CHARS, value.data(), static_cast<int>(value.size()), result.data(), size, nullptr, nullptr);
    return result;
}

gb_result EnumerateAudioDevicesJson(const EDataFlow flow, std::string& json)
{
    ComApartment apartment;
    if (FAILED(apartment.Result()) && apartment.Result() != RPC_E_CHANGED_MODE) {
        return GB_ERROR_COM;
    }

    ComPtr<IMMDeviceEnumerator> enumerator;
    HRESULT result = CoCreateInstance(
        __uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL, IID_PPV_ARGS(&enumerator));
    if (FAILED(result)) {
        return GB_ERROR_COM;
    }

    const std::wstring defaultId = GetDefaultDeviceId(enumerator.Get(), flow);
    ComPtr<IMMDeviceCollection> collection;
    result = enumerator->EnumAudioEndpoints(flow, DEVICE_STATE_ACTIVE, &collection);
    if (FAILED(result)) {
        return GB_ERROR_COM;
    }

    UINT count = 0;
    result = collection->GetCount(&count);
    if (FAILED(result)) {
        return GB_ERROR_COM;
    }

    std::ostringstream stream;
    stream << '[';
    bool first = true;
    for (UINT index = 0; index < count; ++index) {
        ComPtr<IMMDevice> device;
        if (FAILED(collection->Item(index, &device))) {
            continue;
        }

        const std::wstring id = GetDeviceId(device.Get());
        if (id.empty()) {
            continue;
        }
        const std::wstring name = GetFriendlyName(device.Get());
        const std::wstring containerId = GetContainerId(device.Get());
        if (!first) {
            stream << ',';
        }
        first = false;
        stream << "{\"id\":\"" << JsonEscape(WideToUtf8(id))
               << "\",\"name\":\"" << JsonEscape(WideToUtf8(name))
               << "\",\"containerId\":\"" << JsonEscape(WideToUtf8(containerId))
               << "\",\"isDefault\":" << (id == defaultId ? "true" : "false") << '}';
    }
    stream << ']';
    json = stream.str();
    return GB_OK;
}

}
