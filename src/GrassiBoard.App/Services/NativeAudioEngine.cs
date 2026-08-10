using System.Runtime.InteropServices;
using System.Text.Json;
using GrassiBoard.Models;

namespace GrassiBoard.Services;

internal sealed partial class NativeAudioEngine : IDisposable
{
    internal const uint ExpectedApiVersion = 8U;
    private nint _engine;

    public uint ApiVersion { get; private set; }
    public string NativeVersion { get; private set; } = "unknown";
    public bool IsAvailable => _engine != nint.Zero;

    public NativeResult Initialize()
    {
        ApiVersion = NativeMethods.GetApiVersion();
        NativeVersion = Marshal.PtrToStringUTF8(NativeMethods.GetVersion()) ?? "unknown";
        if (ApiVersion != ExpectedApiVersion)
        {
            return NativeResult.InvalidArgument;
        }

        return NativeMethods.EngineCreate(ExpectedApiVersion, out _engine);
    }

    public IReadOnlyList<AudioDevice> EnumerateDevices(bool input)
    {
        uint required;
        NativeResult result = input
            ? NativeMethods.EnumerateInputDevices(nint.Zero, 0U, out required)
            : NativeMethods.EnumerateOutputDevices(nint.Zero, 0U, out required);
        if (result != NativeResult.Ok || required <= 1U)
        {
            return [];
        }

        nint buffer = Marshal.AllocHGlobal(checked((int)required));
        try
        {
            uint written;
            result = input
                ? NativeMethods.EnumerateInputDevices(buffer, required, out written)
                : NativeMethods.EnumerateOutputDevices(buffer, required, out written);
            if (result != NativeResult.Ok || written <= 1U)
            {
                throw new InvalidOperationException($"Device enumeration failed: {result}");
            }

            string json = Marshal.PtrToStringUTF8(buffer, checked((int)written - 1)) ?? "[]";
            return JsonSerializer.Deserialize<List<AudioDevice>>(json, JsonOptions) ?? [];
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public NativeResult Start(string inputId, string outputId) =>
        NativeMethods.EngineStart(_engine, inputId, outputId);

    public NativeResult Stop() => NativeMethods.EngineStop(_engine);
    public NativeResult SetPitch(float semitones) => NativeMethods.SetPitchSemitones(_engine, semitones);
    public NativeResult SetFinePitch(float cents) => NativeMethods.SetPitchCents(_engine, cents);
    public NativeResult SetVoiceFxEnabled(bool enabled) => NativeMethods.SetPitchBypass(_engine, enabled ? 0U : 1U);
    public NativeResult SetFormant(float semitones) => NativeMethods.SetFormantSemitones(_engine, semitones);
    public NativeResult SetFormantPreservation(bool preserve) => NativeMethods.SetFormantPreservation(_engine, preserve ? 1U : 0U);
    public NativeResult SetQuality(uint quality) => NativeMethods.SetPitchQuality(_engine, quality);
    public NativeResult SetMicrophoneMuted(bool muted) => NativeMethods.SetMicrophoneMuted(_engine, muted ? 1U : 0U);
    public NativeResult SetMixerSettings(in MixerSettings settings) => NativeMethods.SetMixerSettings(_engine, in settings);
    public NativeResult PlaySound(ulong key, float volume, bool loop, bool restart) =>
        NativeMethods.PlaySound(_engine, key, volume, loop ? 1U : 0U, restart ? 1U : 0U);
    public NativeResult StopSound(ulong key) => NativeMethods.StopSound(_engine, key);
    public NativeResult StopAllSounds() => NativeMethods.StopAllSounds(_engine);
    public NativeResult SetMediaActive(bool active) => NativeMethods.SetMediaActive(_engine, active ? 1U : 0U);
    public NativeResult ClearMedia() => NativeMethods.ClearMedia(_engine);
    public NativeResult SetMediaMonitorLatency(uint latencyFrames) =>
        NativeMethods.SetMediaMonitorLatency(_engine, latencyFrames);
    public NativeResult GetStatistics(out AudioStatistics statistics) => NativeMethods.GetAudioStatistics(_engine, out statistics);

    public unsafe NativeResult LoadSound(ulong key, float[] samples, ulong frameCount)
    {
        fixed (float* pointer = samples)
        {
            return NativeMethods.LoadSound(_engine, key, pointer, frameCount);
        }
    }

    public unsafe NativeResult WriteMedia(float[] samples, uint frameCount, out uint acceptedFrames)
    {
        fixed (float* pointer = samples)
        {
            return NativeMethods.WriteMedia(_engine, pointer, frameCount, out acceptedFrames);
        }
    }

    public string ReadLastError()
    {
        NativeResult result = NativeMethods.GetLastError(_engine, nint.Zero, 0U, out uint required);
        if (result != NativeResult.Ok || required <= 1U)
        {
            return "no detail";
        }

        nint buffer = Marshal.AllocHGlobal(checked((int)required));
        try
        {
            result = NativeMethods.GetLastError(_engine, buffer, required, out uint written);
            return result == NativeResult.Ok
                ? Marshal.PtrToStringUTF8(buffer, checked((int)written - 1)) ?? "no detail"
                : result.ToString();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        if (_engine == nint.Zero)
        {
            return;
        }

        NativeMethods.EngineStop(_engine);
        NativeMethods.EngineDestroy(_engine);
        _engine = nint.Zero;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static unsafe partial class NativeMethods
    {
        private const string LibraryName = "GrassiBoard.AudioEngine.dll";

        [LibraryImport(LibraryName, EntryPoint = "gb_get_api_version")]
        internal static partial uint GetApiVersion();

        [LibraryImport(LibraryName, EntryPoint = "gb_get_version")]
        internal static partial nint GetVersion();

        [LibraryImport(LibraryName, EntryPoint = "gb_enumerate_input_devices")]
        internal static partial NativeResult EnumerateInputDevices(nint buffer, uint capacity, out uint required);

        [LibraryImport(LibraryName, EntryPoint = "gb_enumerate_output_devices")]
        internal static partial NativeResult EnumerateOutputDevices(nint buffer, uint capacity, out uint required);

        [LibraryImport(LibraryName, EntryPoint = "gb_engine_create")]
        internal static partial NativeResult EngineCreate(uint apiVersion, out nint engine);

        [LibraryImport(LibraryName, EntryPoint = "gb_engine_destroy")]
        internal static partial void EngineDestroy(nint engine);

        [LibraryImport(LibraryName, EntryPoint = "gb_engine_start", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial NativeResult EngineStart(nint engine, string inputDeviceId, string outputDeviceId);

        [LibraryImport(LibraryName, EntryPoint = "gb_engine_stop")]
        internal static partial NativeResult EngineStop(nint engine);

        [LibraryImport(LibraryName, EntryPoint = "gb_set_pitch_semitones")]
        internal static partial NativeResult SetPitchSemitones(nint engine, float semitones);

        [LibraryImport(LibraryName, EntryPoint = "gb_set_pitch_cents")]
        internal static partial NativeResult SetPitchCents(nint engine, float cents);

        [LibraryImport(LibraryName, EntryPoint = "gb_set_pitch_bypass")]
        internal static partial NativeResult SetPitchBypass(nint engine, uint bypass);

        [LibraryImport(LibraryName, EntryPoint = "gb_set_formant_semitones")]
        internal static partial NativeResult SetFormantSemitones(nint engine, float semitones);

        [LibraryImport(LibraryName, EntryPoint = "gb_set_formant_preservation")]
        internal static partial NativeResult SetFormantPreservation(nint engine, uint preserve);

        [LibraryImport(LibraryName, EntryPoint = "gb_set_pitch_quality")]
        internal static partial NativeResult SetPitchQuality(nint engine, uint qualityMode);

        [LibraryImport(LibraryName, EntryPoint = "gb_load_sound_clip")]
        internal static partial NativeResult LoadSound(nint engine, ulong key, float* samples, ulong frameCount);

        [LibraryImport(LibraryName, EntryPoint = "gb_play_sound_clip")]
        internal static partial NativeResult PlaySound(nint engine, ulong key, float volume, uint loop, uint restart);

        [LibraryImport(LibraryName, EntryPoint = "gb_stop_sound_clip")]
        internal static partial NativeResult StopSound(nint engine, ulong key);

        [LibraryImport(LibraryName, EntryPoint = "gb_stop_all_sounds")]
        internal static partial NativeResult StopAllSounds(nint engine);

        [LibraryImport(LibraryName, EntryPoint = "gb_media_write")]
        internal static partial NativeResult WriteMedia(
            nint engine, float* samples, uint frameCount, out uint acceptedFrames);

        [LibraryImport(LibraryName, EntryPoint = "gb_media_set_active")]
        internal static partial NativeResult SetMediaActive(nint engine, uint active);

        [LibraryImport(LibraryName, EntryPoint = "gb_media_clear")]
        internal static partial NativeResult ClearMedia(nint engine);

        [LibraryImport(LibraryName, EntryPoint = "gb_media_set_monitor_latency")]
        internal static partial NativeResult SetMediaMonitorLatency(nint engine, uint latencyFrames);

        [LibraryImport(LibraryName, EntryPoint = "gb_set_microphone_muted")]
        internal static partial NativeResult SetMicrophoneMuted(nint engine, uint muted);

        [LibraryImport(LibraryName, EntryPoint = "gb_set_mixer_settings")]
        internal static partial NativeResult SetMixerSettings(nint engine, in MixerSettings settings);

        [LibraryImport(LibraryName, EntryPoint = "gb_get_audio_statistics")]
        internal static partial NativeResult GetAudioStatistics(nint engine, out AudioStatistics statistics);

        [LibraryImport(LibraryName, EntryPoint = "gb_get_last_error")]
        internal static partial NativeResult GetLastError(nint engine, nint buffer, uint capacity, out uint required);
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct MixerSettings
{
    public uint StructSize;
    public float MicGainDb;
    public float SoundboardGainDb;
    public float MasterGainDb;
    public float GateThresholdDb;
    public float CompressorThresholdDb;
    public float CompressorRatio;
    public float LimiterCeilingDb;
    public float DuckingAmountDb;
    public float PitchWetMix;
    public uint GateEnabled;
    public uint CompressorEnabled;
    public uint LimiterEnabled;
    public uint DuckingEnabled;
    public uint ClippingProtectionEnabled;

    public static MixerSettings CreateDefault() => new()
    {
        StructSize = (uint)Marshal.SizeOf<MixerSettings>(),
        GateThresholdDb = -55.0F,
        CompressorThresholdDb = -18.0F,
        CompressorRatio = 3.0F,
        LimiterCeilingDb = -1.0F,
        DuckingAmountDb = 9.0F,
        PitchWetMix = 1.0F,
        LimiterEnabled = 1U,
        ClippingProtectionEnabled = 1U
    };
}

internal enum NativeResult
{
    Ok = 0,
    InvalidArgument = 1,
    OutOfMemory = 2,
    Com = 3,
    DeviceNotFound = 4,
    AudioClient = 5,
    AlreadyRunning = 6,
    NotRunning = 7,
    BufferTooSmall = 8,
    Internal = 9,
    QueueFull = 10
}

[StructLayout(LayoutKind.Sequential)]
internal struct AudioStatistics
{
    public uint StructSize;
    public uint Running;
    public uint SampleRate;
    public uint CaptureBufferFrames;
    public uint RenderBufferFrames;
    public uint RingBufferFillFrames;
    public uint PitchLatencySamples;
    public ulong CapturedFrames;
    public ulong RenderedFrames;
    public ulong UnderrunCount;
    public ulong OverrunCount;
    public ulong DiscontinuityCount;
    public float InputPeak;
    public float InputRms;
    public float OutputPeak;
    public float OutputRms;
    public float SoundboardPeak;
    public float SoundboardRms;
    public float MasterPeak;
    public float MasterRms;
    public uint ActiveSoundCount;
    public uint MicrophoneMuted;
    public uint MediaBufferFillFrames;
    public uint MediaBufferCapacityFrames;
    public ulong MediaUnderrunCount;
    public float MediaPeak;
    public float MediaRms;
    public uint MediaActive;
    public uint MediaAlignmentFrames;
}
