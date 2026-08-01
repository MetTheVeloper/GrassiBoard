/* Derived from Microsoft's SysVAD sample under the Microsoft Public License. */
#pragma once

#include "speakertopo.h"
#include "speakertoptable.h"
#include "speakerwavtable.h"
#include "micintopo.h"
#include "micintoptable.h"
#include "micinwavtable.h"

NTSTATUS CreateMiniportWaveRTSYSVAD(
    _Out_ PUNKNOWN*, _In_ REFCLSID, _In_opt_ PUNKNOWN, _In_ POOL_FLAGS,
    _In_ PUNKNOWN, _In_opt_ PVOID, _In_ PENDPOINT_MINIPAIR);

NTSTATUS CreateMiniportTopologySYSVAD(
    _Out_ PUNKNOWN*, _In_ REFCLSID, _In_opt_ PUNKNOWN, _In_ POOL_FLAGS,
    _In_ PUNKNOWN, _In_opt_ PVOID, _In_ PENDPOINT_MINIPAIR);

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
    SpeakerPinDeviceFormatsAndModes,
    SIZEOF_ARRAY(SpeakerPinDeviceFormatsAndModes),
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
    MICIN_DEVICE_MAX_CHANNELS,
    MicInPinDeviceFormatsAndModes,
    SIZEOF_ARRAY(MicInPinDeviceFormatsAndModes),
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
