using System.Runtime.InteropServices;

namespace GrassiBoard.Services;

internal sealed partial class NativeAudioEngine
{
    private static readonly object ProcessRegistrySync = new();
    private static readonly List<WeakReference<NativeAudioEngine>> ProcessRegistry = [];

    public NativeAudioEngine()
    {
        lock (ProcessRegistrySync)
        {
            ProcessRegistry.Add(new WeakReference<NativeAudioEngine>(this));
        }
    }

    internal static NativeAudioEngine? FindRunningProcessEngine()
    {
        lock (ProcessRegistrySync)
        {
            for (int index = ProcessRegistry.Count - 1; index >= 0; index--)
            {
                if (!ProcessRegistry[index].TryGetTarget(out NativeAudioEngine? candidate))
                {
                    ProcessRegistry.RemoveAt(index);
                    continue;
                }
                if (!candidate.IsAvailable) continue;
                try
                {
                    if (candidate.GetStatistics(out AudioStatistics statistics) == NativeResult.Ok && statistics.Running != 0U)
                    {
                        return candidate;
                    }
                }
                catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
                {
                }
            }
            return null;
        }
    }

    public unsafe NativeResult LoadLooperMaster(float[] samples, long startFrame, long frameCount)
    {
        if (startFrame < 0L || frameCount <= 0L ||
            startFrame > int.MaxValue || frameCount > int.MaxValue ||
            ((ulong)startFrame + (ulong)frameCount) * 2UL > (ulong)samples.LongLength)
        {
            return NativeResult.InvalidArgument;
        }

        fixed (float* pointer = samples)
        {
            return NativeMethods.LoadLooperMaster(
                _engine,
                pointer + checked((int)startFrame * 2),
                checked((ulong)frameCount));
        }
    }

    public NativeResult ClearLooper() => NativeMethods.ClearLooper(_engine);
    public NativeResult SetLooperTransport(LooperTransportState state) => NativeMethods.SetLooperTransport(_engine, (uint)state);
    public NativeResult SeekLooper(ulong frame) => NativeMethods.SeekLooper(_engine, frame);
    public NativeResult GetLooperState(out LooperNativeState state) => NativeMethods.GetLooperState(_engine, out state);

    public unsafe NativeResult ReadLooperMonitor(float[] interleavedStereo, uint capacityFrames, out uint readFrames)
    {
        if (capacityFrames == 0U || (ulong)capacityFrames * 2UL > (ulong)interleavedStereo.LongLength)
        {
            readFrames = 0U;
            return NativeResult.InvalidArgument;
        }

        fixed (float* pointer = interleavedStereo)
        {
            return NativeMethods.ReadLooperMonitor(_engine, pointer, capacityFrames, out readFrames);
        }
    }

    private static unsafe partial class NativeMethods
    {
        [LibraryImport(LibraryName, EntryPoint = "gb_looper_load_master")]
        internal static partial NativeResult LoadLooperMaster(nint engine, float* samples, ulong frameCount);

        [LibraryImport(LibraryName, EntryPoint = "gb_looper_clear")]
        internal static partial NativeResult ClearLooper(nint engine);

        [LibraryImport(LibraryName, EntryPoint = "gb_looper_set_transport")]
        internal static partial NativeResult SetLooperTransport(nint engine, uint transport);

        [LibraryImport(LibraryName, EntryPoint = "gb_looper_seek")]
        internal static partial NativeResult SeekLooper(nint engine, ulong frame);

        [LibraryImport(LibraryName, EntryPoint = "gb_looper_get_state")]
        internal static partial NativeResult GetLooperState(nint engine, out LooperNativeState state);

        [LibraryImport(LibraryName, EntryPoint = "gb_looper_monitor_read")]
        internal static partial NativeResult ReadLooperMonitor(nint engine, float* samples, uint capacityFrames, out uint readFrames);
    }
}

internal enum LooperTransportState : uint
{
    Stopped = 0U,
    Paused = 1U,
    Playing = 2U
}

[StructLayout(LayoutKind.Sequential)]
internal struct LooperNativeState
{
    public uint StructSize;
    public uint Transport;
    public uint SampleRate;
    public uint Channels;
    public ulong LoopFrames;
    public ulong PlayheadFrame;
    public uint MonitorFillFrames;
    public uint MonitorCapacityFrames;
    public ulong MonitorOverrunCount;
}