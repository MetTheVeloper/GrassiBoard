/* Derived from Microsoft's SysVAD sample under the Microsoft Public License. */
#pragma once

#include "speakertopo.h"
#include "speakertoptable.h"
#include "speakerwavtable.h"
#include "micintopo.h"
#include "micintoptable.h"
#include "micinwavtable.h"
#include "cabletransport.h"

NTSTATUS CreateMiniportWaveRTSYSVAD(
    _Out_ PUNKNOWN*, _In_ REFCLSID, _In_opt_ PUNKNOWN, _In_ POOL_FLAGS,
    _In_ PUNKNOWN, _In_opt_ PVOID, _In_ PENDPOINT_MINIPAIR);

NTSTATUS CreateMiniportTopologySYSVAD(
    _Out_ PUNKNOWN*, _In_ REFCLSID, _In_opt_ PUNKNOWN, _In_ POOL_FLAGS,
    _In_ PUNKNOWN, _In_opt_ PVOID, _In_ PENDPOINT_MINIPAIR);

// Keep the working SysVAD speaker contract stereo and the reference MicIn
// contract mono. Windows Audio converts client formats; the kernel cable only
// performs the required stereo-to-mono PCM16 downmix between its endpoints.
static KSDATAFORMAT_WAVEFORMATEXTENSIBLE GrassiBoardCableRenderPcmFormats[] =
{
    {
        {
            sizeof(KSDATAFORMAT_WAVEFORMATEXTENSIBLE),
            0,
            0,
            0,
            STATICGUIDOF(KSDATAFORMAT_TYPE_AUDIO),
            STATICGUIDOF(KSDATAFORMAT_SUBTYPE_PCM),
            STATICGUIDOF(KSDATAFORMAT_SPECIFIER_WAVEFORMATEX)
        },
        {
            {
                WAVE_FORMAT_EXTENSIBLE,
                GrassiBoardCableTransport::RenderChannelCount,
                GrassiBoardCableTransport::SampleRate,
                GrassiBoardCableTransport::SampleRate * GrassiBoardCableTransport::RenderBlockAlign,
                GrassiBoardCableTransport::RenderBlockAlign,
                GrassiBoardCableTransport::BitsPerSample,
                sizeof(WAVEFORMATEXTENSIBLE) - sizeof(WAVEFORMATEX)
            },
            GrassiBoardCableTransport::BitsPerSample,
            KSAUDIO_SPEAKER_STEREO,
            STATICGUIDOF(KSDATAFORMAT_SUBTYPE_PCM)
        }
    }
};

static KSDATAFORMAT_WAVEFORMATEXTENSIBLE GrassiBoardCableCapturePcmFormats[] =
{
    {
        {
            sizeof(KSDATAFORMAT_WAVEFORMATEXTENSIBLE),
            0,
            0,
            0,
            STATICGUIDOF(KSDATAFORMAT_TYPE_AUDIO),
            STATICGUIDOF(KSDATAFORMAT_SUBTYPE_PCM),
            STATICGUIDOF(KSDATAFORMAT_SPECIFIER_WAVEFORMATEX)
        },
        {
            {
                WAVE_FORMAT_EXTENSIBLE,
                GrassiBoardCableTransport::CaptureChannelCount,
                GrassiBoardCableTransport::SampleRate,
                GrassiBoardCableTransport::CaptureBytesPerSecond,
                GrassiBoardCableTransport::CaptureBlockAlign,
                GrassiBoardCableTransport::BitsPerSample,
                sizeof(WAVEFORMATEXTENSIBLE) - sizeof(WAVEFORMATEX)
            },
            GrassiBoardCableTransport::BitsPerSample,
            KSAUDIO_SPEAKER_MONO,
            STATICGUIDOF(KSDATAFORMAT_SUBTYPE_PCM)
        }
    }
};

static MODE_AND_DEFAULT_FORMAT GrassiBoardRenderModes[] =
{
    { STATIC_AUDIO_SIGNALPROCESSINGMODE_RAW, &GrassiBoardCableRenderPcmFormats[0].DataFormat },
    { STATIC_AUDIO_SIGNALPROCESSINGMODE_DEFAULT, &GrassiBoardCableRenderPcmFormats[0].DataFormat },
    { STATIC_AUDIO_SIGNALPROCESSINGMODE_MEDIA, &GrassiBoardCableRenderPcmFormats[0].DataFormat },
    { STATIC_AUDIO_SIGNALPROCESSINGMODE_MOVIE, &GrassiBoardCableRenderPcmFormats[0].DataFormat },
    { STATIC_AUDIO_SIGNALPROCESSINGMODE_COMMUNICATIONS, &GrassiBoardCableRenderPcmFormats[0].DataFormat },
    { STATIC_AUDIO_SIGNALPROCESSINGMODE_NOTIFICATION, &GrassiBoardCableRenderPcmFormats[0].DataFormat },
};

static MODE_AND_DEFAULT_FORMAT GrassiBoardCaptureModes[] =
{
    { STATIC_AUDIO_SIGNALPROCESSINGMODE_RAW, &GrassiBoardCableCapturePcmFormats[0].DataFormat },
    { STATIC_AUDIO_SIGNALPROCESSINGMODE_DEFAULT, &GrassiBoardCableCapturePcmFormats[0].DataFormat },
    { STATIC_AUDIO_SIGNALPROCESSINGMODE_SPEECH, &GrassiBoardCableCapturePcmFormats[0].DataFormat },
    { STATIC_AUDIO_SIGNALPROCESSINGMODE_COMMUNICATIONS, &GrassiBoardCableCapturePcmFormats[0].DataFormat },
    { STATIC_AUDIO_SIGNALPROCESSINGMODE_FAR_FIELD_SPEECH, &GrassiBoardCableCapturePcmFormats[0].DataFormat },
};

static PIN_DEVICE_FORMATS_AND_MODES GrassiBoardRenderFormatsAndModes[] =
{
    {
        SystemRenderPin,
        GrassiBoardCableRenderPcmFormats,
        SIZEOF_ARRAY(GrassiBoardCableRenderPcmFormats),
        GrassiBoardRenderModes,
        SIZEOF_ARRAY(GrassiBoardRenderModes)
    },
    {
        OffloadRenderPin,
        GrassiBoardCableRenderPcmFormats,
        SIZEOF_ARRAY(GrassiBoardCableRenderPcmFormats),
        GrassiBoardRenderModes,
        SIZEOF_ARRAY(GrassiBoardRenderModes)
    },
    {
        RenderLoopbackPin,
        GrassiBoardCableRenderPcmFormats,
        SIZEOF_ARRAY(GrassiBoardCableRenderPcmFormats),
        NULL,
        0
    },
    { BridgePin, NULL, 0, NULL, 0 },
    {
        NoPin,
        GrassiBoardCableRenderPcmFormats,
        SIZEOF_ARRAY(GrassiBoardCableRenderPcmFormats),
        NULL,
        0
    }
};

static PIN_DEVICE_FORMATS_AND_MODES GrassiBoardCaptureFormatsAndModes[] =
{
    { BridgePin, NULL, 0, NULL, 0 },
    {
        SystemCapturePin,
        GrassiBoardCableCapturePcmFormats,
        SIZEOF_ARRAY(GrassiBoardCableCapturePcmFormats),
        GrassiBoardCaptureModes,
        SIZEOF_ARRAY(GrassiBoardCaptureModes)
    }
};

#if defined(GRASSIBOARD_CAPTURE_REFERENCE_MODES)
#define GRASSIBOARD_SELECTED_CAPTURE_FORMATS MicInPinDeviceFormatsAndModes
#else
#define GRASSIBOARD_SELECTED_CAPTURE_FORMATS GrassiBoardCaptureFormatsAndModes
#endif

static struct
{
    KSAUDIO_PACKETSIZE_CONSTRAINTS2 TransportPacketConstraints;
    KSAUDIO_PACKETSIZE_PROCESSINGMODE_CONSTRAINT AdditionalProcessingConstraints[1];
} GrassiBoardRenderPacketConstraints =
{
    {
        2 * HNSTIME_PER_MILLISECOND,
        FILE_BYTE_ALIGNMENT,
        0,
        2,
        { STATIC_AUDIO_SIGNALPROCESSINGMODE_DEFAULT, 128, 0 },
    },
    { { STATIC_AUDIO_SIGNALPROCESSINGMODE_MOVIE, 1024, 0 } },
};

const SYSVAD_DEVPROPERTY GrassiBoardRenderInterfaceProperties[] =
{
    {
        &DEVPKEY_KsAudio_PacketSize_Constraints2,
        DEVPROP_TYPE_BINARY,
        sizeof(GrassiBoardRenderPacketConstraints),
        &GrassiBoardRenderPacketConstraints,
    },
};

static PHYSICALCONNECTIONTABLE GrassiBoardRenderPhysicalConnections[] =
{
    { KSPIN_TOPO_WAVEOUT_SOURCE, KSPIN_WAVE_RENDER_SOURCE, CONNECTIONTYPE_WAVE_OUTPUT },
};

static ENDPOINT_MINIPAIR GrassiBoardRenderMiniports =
{
    eSpeakerDevice,
    L"TopologyGrassiBoardRender",
    NULL,
    CreateMiniportTopologySYSVAD,
    &SpeakerTopoMiniportFilterDescriptor,
    0, NULL,
    L"WaveGrassiBoardRender",
    NULL,
    CreateMiniportWaveRTSYSVAD,
    &SpeakerWaveMiniportFilterDescriptor,
    ARRAYSIZE(GrassiBoardRenderInterfaceProperties),
    GrassiBoardRenderInterfaceProperties,
    SPEAKER_DEVICE_MAX_CHANNELS,
    GrassiBoardRenderFormatsAndModes,
    SIZEOF_ARRAY(GrassiBoardRenderFormatsAndModes),
    GrassiBoardRenderPhysicalConnections,
    SIZEOF_ARRAY(GrassiBoardRenderPhysicalConnections),
    ENDPOINT_OFFLOAD_SUPPORTED,
    SpeakerModulesWaveFilter,
    SIZEOF_ARRAY(SpeakerModulesWaveFilter),
    &SpeakerModuleNotificationDeviceId,
};

static PHYSICALCONNECTIONTABLE GrassiBoardCapturePhysicalConnections[] =
{
    { KSPIN_TOPO_BRIDGE, KSPIN_WAVE_BRIDGE, CONNECTIONTYPE_TOPOLOGY_OUTPUT },
};

static ENDPOINT_MINIPAIR GrassiBoardCaptureMiniports =
{
    eMicInDevice,
    L"TopologyGrassiBoardCapture",
    NULL,
    CreateMiniportTopologySYSVAD,
    &MicInTopoMiniportFilterDescriptor,
    0, NULL,
    L"WaveGrassiBoardCapture",
    NULL,
    CreateMiniportWaveRTSYSVAD,
    &MicInWaveMiniportFilterDescriptor,
    0, NULL,
    GrassiBoardCableTransport::CaptureChannelCount,
    GRASSIBOARD_SELECTED_CAPTURE_FORMATS,
    SIZEOF_ARRAY(GRASSIBOARD_SELECTED_CAPTURE_FORMATS),
    GrassiBoardCapturePhysicalConnections,
    SIZEOF_ARRAY(GrassiBoardCapturePhysicalConnections),
    ENDPOINT_NO_FLAGS,
    NULL, 0, NULL,
};

static PENDPOINT_MINIPAIR g_RenderEndpoints[] = { &GrassiBoardRenderMiniports };
#define g_cRenderEndpoints SIZEOF_ARRAY(g_RenderEndpoints)

static PENDPOINT_MINIPAIR g_CaptureEndpoints[] = { &GrassiBoardCaptureMiniports };
#define g_cCaptureEndpoints SIZEOF_ARRAY(g_CaptureEndpoints)
#define g_MaxMiniports ((g_cRenderEndpoints + g_cCaptureEndpoints) * 2)
