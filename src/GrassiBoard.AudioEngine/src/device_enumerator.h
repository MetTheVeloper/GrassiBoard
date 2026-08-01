#pragma once

#include "grassiboard/audio_engine.h"

#include <mmdeviceapi.h>

#include <string>

namespace grassiboard {

gb_result EnumerateAudioDevicesJson(EDataFlow flow, std::string& json);
std::wstring Utf8ToWide(const std::string& value);
std::string WideToUtf8(const std::wstring& value);

}
