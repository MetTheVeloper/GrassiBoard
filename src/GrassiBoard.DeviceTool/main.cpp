#include <windows.h>
#include <cfgmgr32.h>
#include <newdev.h>
#include <setupapi.h>

#include <algorithm>
#include <cwctype>
#include <iostream>
#include <string>
#include <vector>

namespace
{
constexpr wchar_t HardwareId[] = L"ROOT\\GrassiBoardVirtualAudio";
constexpr wchar_t DeviceName[] = L"GrassiBoard Virtual Audio";

std::wstring ErrorMessage(DWORD code)
{
    wchar_t* text = nullptr;
    const DWORD size = FormatMessageW(
        FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
        nullptr, code, 0, reinterpret_cast<wchar_t*>(&text), 0, nullptr);
    std::wstring message = size && text ? text : L"Unknown Windows error";
    if (text) LocalFree(text);
    while (!message.empty() && std::iswspace(message.back())) message.pop_back();
    return message;
}

bool EqualsIgnoreCase(const wchar_t* left, const wchar_t* right)
{
    return _wcsicmp(left, right) == 0;
}

bool HasHardwareId(HDEVINFO devices, SP_DEVINFO_DATA& device)
{
    DWORD type = 0;
    DWORD required = 0;
    SetupDiGetDeviceRegistryPropertyW(devices, &device, SPDRP_HARDWAREID, &type, nullptr, 0, &required);
    if (GetLastError() != ERROR_INSUFFICIENT_BUFFER || required < sizeof(wchar_t)) return false;

    std::vector<BYTE> buffer(required + sizeof(wchar_t), 0);
    if (!SetupDiGetDeviceRegistryPropertyW(devices, &device, SPDRP_HARDWAREID, &type, buffer.data(), required, nullptr)) return false;
    const wchar_t* id = reinterpret_cast<const wchar_t*>(buffer.data());
    while (*id)
    {
        if (EqualsIgnoreCase(id, HardwareId)) return true;
        id += wcslen(id) + 1;
    }
    return false;
}

int Install(const wchar_t* infArgument)
{
    wchar_t infPath[MAX_PATH]{};
    if (!GetFullPathNameW(infArgument, ARRAYSIZE(infPath), infPath, nullptr))
    {
        std::wcerr << L"Cannot resolve INF path: " << ErrorMessage(GetLastError()) << L'\n';
        return 2;
    }

    GUID classGuid{};
    wchar_t className[MAX_CLASS_NAME_LEN]{};
    if (!SetupDiGetINFClassW(infPath, &classGuid, className, ARRAYSIZE(className), nullptr))
    {
        std::wcerr << L"Cannot read INF class: " << ErrorMessage(GetLastError()) << L'\n';
        return 3;
    }

    HDEVINFO devices = SetupDiCreateDeviceInfoList(&classGuid, nullptr);
    if (devices == INVALID_HANDLE_VALUE)
    {
        std::wcerr << L"Cannot create device list: " << ErrorMessage(GetLastError()) << L'\n';
        return 4;
    }

    SP_DEVINFO_DATA device{sizeof(device)};
    if (!SetupDiCreateDeviceInfoW(devices, DeviceName, &classGuid, DeviceName, nullptr, DICD_GENERATE_ID, &device))
    {
        const DWORD error = GetLastError();
        SetupDiDestroyDeviceInfoList(devices);
        std::wcerr << L"Cannot create root device: " << ErrorMessage(error) << L'\n';
        return 5;
    }

    const size_t hardwareIdLength = wcslen(HardwareId);
    std::vector<wchar_t> hardwareIds(hardwareIdLength + 2, L'\0');
    std::copy_n(HardwareId, hardwareIdLength, hardwareIds.data());
    const DWORD bytes = static_cast<DWORD>(hardwareIds.size() * sizeof(wchar_t));
    if (!SetupDiSetDeviceRegistryPropertyW(
            devices, &device, SPDRP_HARDWAREID,
            reinterpret_cast<const BYTE*>(hardwareIds.data()), bytes) ||
        !SetupDiCallClassInstaller(DIF_REGISTERDEVICE, devices, &device))
    {
        const DWORD error = GetLastError();
        SetupDiDestroyDeviceInfoList(devices);
        std::wcerr << L"Cannot register root device: " << ErrorMessage(error) << L'\n';
        return 6;
    }

    BOOL reboot = FALSE;
    if (!UpdateDriverForPlugAndPlayDevicesW(nullptr, HardwareId, infPath, INSTALLFLAG_FORCE, &reboot))
    {
        const DWORD error = GetLastError();
        SetupDiCallClassInstaller(DIF_REMOVE, devices, &device);
        SetupDiDestroyDeviceInfoList(devices);
        std::wcerr << L"Driver update failed; the temporary device was removed: " << ErrorMessage(error) << L'\n';
        return 7;
    }

    SetupDiDestroyDeviceInfoList(devices);
    std::wcout << L"Installed " << HardwareId << L". rebootRequired=" << (reboot ? L"true" : L"false") << L'\n';
    return reboot ? 10 : 0;
}

int ForEachMatchingDevice(bool remove)
{
    HDEVINFO devices = SetupDiGetClassDevsW(nullptr, nullptr, nullptr, DIGCF_ALLCLASSES);
    if (devices == INVALID_HANDLE_VALUE)
    {
        std::wcerr << L"Cannot enumerate devices: " << ErrorMessage(GetLastError()) << L'\n';
        return 2;
    }

    unsigned matches = 0;
    bool reboot = false;
    for (DWORD index = 0;; ++index)
    {
        SP_DEVINFO_DATA device{sizeof(device)};
        if (!SetupDiEnumDeviceInfo(devices, index, &device))
        {
            if (GetLastError() == ERROR_NO_MORE_ITEMS) break;
            std::wcerr << L"Device enumeration failed: " << ErrorMessage(GetLastError()) << L'\n';
            SetupDiDestroyDeviceInfoList(devices);
            return 3;
        }
        if (!HasHardwareId(devices, device)) continue;
        ++matches;

        ULONG status = 0;
        ULONG problem = 0;
        const CONFIGRET configResult = CM_Get_DevNode_Status(&status, &problem, device.DevInst, 0);
        if (!remove)
        {
            std::wcout << L"present=true status=0x" << std::hex << status << L" problem=" << std::dec
                       << (configResult == CR_SUCCESS ? problem : static_cast<ULONG>(-1)) << L'\n';
            continue;
        }

        SP_REMOVEDEVICE_PARAMS parameters{};
        parameters.ClassInstallHeader.cbSize = sizeof(SP_CLASSINSTALL_HEADER);
        parameters.ClassInstallHeader.InstallFunction = DIF_REMOVE;
        parameters.Scope = DI_REMOVEDEVICE_GLOBAL;
        parameters.HwProfile = 0;
        if (!SetupDiSetClassInstallParamsW(devices, &device, &parameters.ClassInstallHeader, sizeof(parameters)) ||
            !SetupDiCallClassInstaller(DIF_REMOVE, devices, &device))
        {
            const DWORD error = GetLastError();
            SetupDiDestroyDeviceInfoList(devices);
            std::wcerr << L"Cannot remove device: " << ErrorMessage(error) << L'\n';
            return 4;
        }

        SP_DEVINSTALL_PARAMS_W installParameters{sizeof(installParameters)};
        if (SetupDiGetDeviceInstallParamsW(devices, &device, &installParameters))
        {
            reboot = reboot || (installParameters.Flags & (DI_NEEDREBOOT | DI_NEEDRESTART)) != 0;
        }
    }

    SetupDiDestroyDeviceInfoList(devices);
    if (!remove && matches == 0) std::wcout << L"present=false\n";
    if (remove) std::wcout << L"removed=" << matches << L" rebootRequired=" << (reboot ? L"true" : L"false") << L'\n';
    return reboot ? 10 : 0;
}
} // namespace

int wmain(int argc, wchar_t** argv)
{
    if (argc == 3 && EqualsIgnoreCase(argv[1], L"install")) return Install(argv[2]);
    if (argc == 2 && EqualsIgnoreCase(argv[1], L"remove")) return ForEachMatchingDevice(true);
    if (argc == 2 && EqualsIgnoreCase(argv[1], L"status")) return ForEachMatchingDevice(false);
    std::wcerr << L"Usage: GrassiBoard.DeviceTool.exe install <absolute-inf> | remove | status\n";
    return 1;
}
