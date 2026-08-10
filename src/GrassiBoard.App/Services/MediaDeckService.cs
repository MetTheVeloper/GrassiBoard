using System.Diagnostics;
using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GrassiBoard.Services;

internal sealed class MediaDeckService : IDisposable
{
    public const int SampleRate = 48_000;
    private const int Channels = 2;
    private const int BlockFrames = 960;
    private const uint ReadAheadFrames = 9_600U;
    private const int MonitorDesiredLatencyMilliseconds = 50;
    private const uint MonitorQueueFrames = 2_880U;
    private const double MonitorQueueMilliseconds = MonitorQueueFrames * 1000.0 / SampleRate;
    private readonly NativeAudioEngine _engine;
    private readonly Func<bool> _engineRunning;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _stateLock = new();
    private Task? _worker;
    private string _filePath = string.Empty;
    private string _monitorDeviceId = string.Empty;
    private string? _error;
    private double _durationSeconds;
    private double _positionSeconds;
    private double _volume = 0.8;
    private bool _monitorEnabled = true;
    private bool _sendEnabled = true;
    private double _syncOffsetMilliseconds;
    private bool _playing;
    private int _generation;
    private float _peak;
    private bool _disposed;

    public MediaDeckService(NativeAudioEngine engine, Func<bool> engineRunning)
    {
        _engine = engine;
        _engineRunning = engineRunning;
    }

    public string FilePath { get { lock (_stateLock) return _filePath; } }
    public string FileName => string.IsNullOrWhiteSpace(FilePath) ? "No media loaded" : Path.GetFileName(FilePath);
    public string? Error { get { lock (_stateLock) return _error; } }
    public double DurationSeconds { get { lock (_stateLock) return _durationSeconds; } }
    public double PositionSeconds { get { lock (_stateLock) return _positionSeconds; } }
    public bool IsPlaying { get { lock (_stateLock) return _playing; } }
    public float Peak => Volatile.Read(ref _peak);

    public double Volume
    {
        get { lock (_stateLock) return _volume; }
        set { lock (_stateLock) _volume = double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.5) : 0.8; }
    }

    public bool MonitorEnabled
    {
        get { lock (_stateLock) return _monitorEnabled; }
        set { lock (_stateLock) { if (_monitorEnabled == value) return; _monitorEnabled = value; _generation++; } }
    }

    public bool SendEnabled
    {
        get { lock (_stateLock) return _sendEnabled; }
        set
        {
            lock (_stateLock)
            {
                if (_sendEnabled == value) return;
                _sendEnabled = value;
                _generation++;
            }
            _engine.ClearMedia();
            _engine.SetMediaActive(false);
        }
    }

    public double SyncOffsetMilliseconds
    {
        get { lock (_stateLock) return _syncOffsetMilliseconds; }
        set
        {
            double clamped = double.IsFinite(value) ? Math.Clamp(value, -100.0, 100.0) : 0.0;
            bool monitor;
            lock (_stateLock)
            {
                if (Math.Abs(_syncOffsetMilliseconds - clamped) < 0.001) return;
                _syncOffsetMilliseconds = clamped;
                monitor = _monitorEnabled;
            }
            if (_engine.IsAvailable)
            {
                _engine.SetMediaMonitorLatency(CalculateMonitorPathLatencyFrames(monitor, clamped));
            }
        }
    }

    public string MonitorDeviceId
    {
        get { lock (_stateLock) return _monitorDeviceId; }
        set { lock (_stateLock) { if (_monitorDeviceId == value) return; _monitorDeviceId = value ?? string.Empty; _generation++; } }
    }

    public async Task LoadAsync(string path)
    {
        string fullPath = path;
        try
        {
            fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                lock (_stateLock)
                {
                    _filePath = fullPath;
                    _error = "Media file is missing. Locate it again or clear the deck.";
                    _durationSeconds = 0.0;
                    _positionSeconds = 0.0;
                    _playing = false;
                    _generation++;
                }
                return;
            }

            double duration = await Task.Run(() =>
            {
                using WaveStream reader = OpenReader(fullPath);
                return reader.TotalTime.TotalSeconds;
            });
            lock (_stateLock)
            {
                _filePath = fullPath;
                _durationSeconds = Math.Max(0.0, duration);
                _positionSeconds = 0.0;
                _error = null;
                _playing = false;
                _generation++;
            }
            _engine.SetMediaActive(false);
            _engine.ClearMedia();
        }
        catch (Exception exception) when (IsRecoverableMediaFailure(exception))
        {
            lock (_stateLock)
            {
                _filePath = fullPath;
                _error = $"Unable to open media: {exception.Message}";
                _durationSeconds = 0.0;
                _positionSeconds = 0.0;
                _playing = false;
                _generation++;
            }
        }
    }

    public void PlayPause()
    {
        lock (_stateLock)
        {
            if (string.IsNullOrWhiteSpace(_filePath) || _error is not null)
            {
                return;
            }
            _playing = !_playing;
            if (_playing && _positionSeconds >= _durationSeconds - 0.05)
            {
                _positionSeconds = 0.0;
            }
            _generation++;
            EnsureWorker();
        }
        _engine.ClearMedia();
        _engine.SetMediaActive(false);
    }

    public void Stop()
    {
        lock (_stateLock)
        {
            _playing = false;
            _positionSeconds = 0.0;
            _generation++;
        }
        Volatile.Write(ref _peak, 0.0F);
        _engine.SetMediaActive(false);
        _engine.ClearMedia();
    }

    public void Seek(double seconds)
    {
        lock (_stateLock)
        {
            _positionSeconds = Math.Clamp(double.IsFinite(seconds) ? seconds : 0.0, 0.0, _durationSeconds);
            _generation++;
        }
        _engine.SetMediaActive(false);
        _engine.ClearMedia();
    }

    public void Skip(double seconds) => Seek(PositionSeconds + seconds);

    public void Clear()
    {
        Stop();
        lock (_stateLock)
        {
            _filePath = string.Empty;
            _durationSeconds = 0.0;
            _error = null;
            _generation++;
        }
    }

    public void SetEngineRunning(bool running)
    {
        _engine.ClearMedia();
        _engine.SetMediaActive(false);
    }

    private void EnsureWorker()
    {
        if (_worker is null || _worker.IsCompleted)
        {
            _worker = Task.Run(() => WorkerAsync(_shutdown.Token));
        }
    }

    private async Task WorkerAsync(CancellationToken cancellationToken)
    {
        int activeGeneration = -1;
        WaveStream? reader = null;
        ISampleProvider? provider = null;
        WasapiOut? monitorOutput = null;
        BufferedWaveProvider? monitorBuffer = null;
        var samples = new float[BlockFrames * Channels];
        var monitorBytes = new byte[samples.Length * sizeof(float)];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string path;
                string monitorId;
                double position;
                double volume;
                bool playing;
                bool monitor;
                bool send;
                int generation;
                lock (_stateLock)
                {
                    path = _filePath;
                    monitorId = _monitorDeviceId;
                    position = _positionSeconds;
                    volume = _volume;
                    playing = _playing;
                    monitor = _monitorEnabled;
                    send = _sendEnabled;
                    generation = _generation;
                }

                if (!playing)
                {
                    DisposePlayback(ref reader, ref monitorOutput);
                    provider = null;
                    monitorBuffer = null;
                    activeGeneration = generation;
                    Volatile.Write(ref _peak, 0.0F);
                    await Task.Delay(25, cancellationToken);
                    continue;
                }

                if (generation != activeGeneration || reader is null || provider is null)
                {
                    DisposePlayback(ref reader, ref monitorOutput);
                    provider = null;
                    monitorBuffer = null;
                    try
                    {
                        reader = OpenReader(path);
                        reader.CurrentTime = TimeSpan.FromSeconds(Math.Clamp(position, 0.0, reader.TotalTime.TotalSeconds));
                        provider = CreateStereo48KProvider(reader);
                        if (monitor && !string.IsNullOrWhiteSpace(monitorId))
                        {
                            monitorBuffer = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels))
                            {
                                BufferDuration = TimeSpan.FromSeconds(1),
                                DiscardOnBufferOverflow = true,
                                ReadFully = true
                            };
                            using var enumerator = new MMDeviceEnumerator();
                            MMDevice device = enumerator.GetDevice(monitorId);
                            monitorOutput = new WasapiOut(
                                device,
                                AudioClientShareMode.Shared,
                                true,
                                MonitorDesiredLatencyMilliseconds);
                            monitorOutput.Init(monitorBuffer);
                            monitorOutput.Play();
                        }
                        activeGeneration = generation;
                        _engine.SetMediaMonitorLatency(CalculateMonitorPathLatencyFrames(monitor, SyncOffsetMilliseconds));
                        _engine.ClearMedia();
                        _engine.SetMediaActive(false);
                    }
                    catch (Exception exception) when (IsRecoverableMediaFailure(exception))
                    {
                        lock (_stateLock)
                        {
                            _error = $"Media playback failed: {exception.Message}";
                            _playing = false;
                            _generation++;
                        }
                        continue;
                    }
                }

                bool canSend = send && _engineRunning();
                bool canMonitor = monitorBuffer is not null;
                if (!canSend && !canMonitor)
                {
                    await Task.Delay(25, cancellationToken);
                    continue;
                }

                bool nativeFull = false;
                uint alignmentFrames = 0U;
                if (canSend && _engine.GetStatistics(out AudioStatistics statistics) == NativeResult.Ok)
                {
                    alignmentFrames = statistics.MediaAlignmentFrames;
                    uint alignedReadAhead = ReadAheadFrames + alignmentFrames;
                    nativeFull = statistics.MediaBufferFillFrames >= alignedReadAhead;
                }
                bool monitorFull = monitorBuffer is not null &&
                    monitorBuffer.BufferedBytes >= checked((int)(MonitorQueueFrames * Channels * sizeof(float)));
                if ((canSend && nativeFull) || (canMonitor && monitorFull))
                {
                    await Task.Delay(10, cancellationToken);
                    continue;
                }

                int read = provider.Read(samples, 0, samples.Length);
                if (read <= 0)
                {
                    lock (_stateLock)
                    {
                        _positionSeconds = _durationSeconds;
                        _playing = false;
                        _generation++;
                    }
                    _engine.SetMediaActive(false);
                    Volatile.Write(ref _peak, 0.0F);
                    continue;
                }
                int frames = read / Channels;
                float gain = (float)volume;
                float peak = 0.0F;
                for (int index = 0; index < frames * Channels; ++index)
                {
                    float value = float.IsFinite(samples[index]) ? samples[index] * gain : 0.0F;
                    samples[index] = Math.Clamp(value, -4.0F, 4.0F);
                    peak = Math.Max(peak, Math.Abs(samples[index]));
                }
                Volatile.Write(ref _peak, peak);

                if (canSend)
                {
                    NativeResult result = _engine.WriteMedia(samples, (uint)frames, out uint accepted);
                    if (result == NativeResult.Ok && accepted > 0U)
                    {
                        _engine.SetMediaActive(true);
                    }
                    if (result != NativeResult.Ok || accepted != frames)
                    {
                        await Task.Delay(5, cancellationToken);
                    }
                }
                if (monitorBuffer is not null)
                {
                    Buffer.BlockCopy(samples, 0, monitorBytes, 0, frames * Channels * sizeof(float));
                    monitorBuffer.AddSamples(monitorBytes, 0, frames * Channels * sizeof(float));
                }
                lock (_stateLock)
                {
                    _positionSeconds = Math.Min(_durationSeconds, reader.CurrentTime.TotalSeconds);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception exception) when (IsRecoverableMediaFailure(exception))
        {
            lock (_stateLock)
            {
                _error = $"Media playback stopped: {exception.Message}";
                _playing = false;
                _generation++;
            }
        }
        finally
        {
            DisposePlayback(ref reader, ref monitorOutput);
            _engine.SetMediaActive(false);
            _engine.ClearMedia();
        }
    }

    private static WaveStream OpenReader(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".wav" or ".mp3" or ".aiff" or ".aif"
            ? new AudioFileReader(path)
            : new MediaFoundationReader(path);
    }

    internal static uint CalculateMonitorPathLatencyFrames(bool monitor, double offsetMilliseconds)
    {
        if (!monitor)
        {
            return 0U;
        }
        double totalMilliseconds = Math.Max(
            0.0,
            MonitorDesiredLatencyMilliseconds + MonitorQueueMilliseconds + offsetMilliseconds);
        return checked((uint)Math.Round(SampleRate * totalMilliseconds / 1000.0));
    }

    private static bool IsRecoverableMediaFailure(Exception exception) =>
        exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException;

    private static ISampleProvider CreateStereo48KProvider(WaveStream reader)
    {
        ISampleProvider provider = reader is ISampleProvider sampleProvider
            ? sampleProvider
            : reader.ToSampleProvider();
        if (provider.WaveFormat.Channels == 1)
        {
            provider = new MonoToStereoSampleProvider(provider);
        }
        else if (provider.WaveFormat.Channels > 2)
        {
            var multiplex = new MultiplexingSampleProvider([provider], 2);
            multiplex.ConnectInputToOutput(0, 0);
            multiplex.ConnectInputToOutput(1, 1);
            provider = multiplex;
        }
        if (provider.WaveFormat.SampleRate != SampleRate)
        {
            provider = new WdlResamplingSampleProvider(provider, SampleRate);
        }
        return provider;
    }

    private static void DisposePlayback(ref WaveStream? reader, ref WasapiOut? output)
    {
        output?.Stop();
        output?.Dispose();
        output = null;
        reader?.Dispose();
        reader = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdown.Cancel();
        try { _worker?.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
        _shutdown.Dispose();
    }
}
