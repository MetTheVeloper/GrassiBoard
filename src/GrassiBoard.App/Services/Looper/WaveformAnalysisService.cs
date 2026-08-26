using System.IO;
using GrassiBoard.Models.Looper;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GrassiBoard.Services.Looper;

internal sealed record WaveformAnalysisResult(
    float[] Samples,
    long FrameCount,
    TimeSpan Duration,
    WaveformEnvelope Envelope);

internal sealed class WaveformAnalysisService
{
    public const int TargetSampleRate = 48_000;
    public const int TargetChannels = 2;
    private const int MaxImportMinutes = 10;

    public Task<WaveformAnalysisResult> AnalyzeFileAsync(
        string path,
        int preferredBucketCount = 2_048,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("An audio path is required.", nameof(path));
        }

        return Task.Run(() => AnalyzeFile(path, preferredBucketCount, cancellationToken), cancellationToken);
    }

    internal WaveformEnvelope BuildEnvelope(
        float[] interleavedSamples,
        int channels = TargetChannels,
        int preferredBucketCount = 2_048)
    {
        ArgumentNullException.ThrowIfNull(interleavedSamples);
        if (channels <= 0 || interleavedSamples.Length < channels)
        {
            return WaveformEnvelope.Empty;
        }

        int frameCount = interleavedSamples.Length / channels;
        int bucketCount = Math.Clamp(preferredBucketCount, 1, frameCount);
        float[] minimum = new float[bucketCount];
        float[] maximum = new float[bucketCount];

        for (int bucket = 0; bucket < bucketCount; bucket++)
        {
            int startFrame = (int)((long)bucket * frameCount / bucketCount);
            int endFrame = (int)((long)(bucket + 1) * frameCount / bucketCount);
            if (endFrame <= startFrame)
            {
                endFrame = Math.Min(frameCount, startFrame + 1);
            }

            float min = 0.0F;
            float max = 0.0F;
            bool initialized = false;
            for (int frame = startFrame; frame < endFrame; frame++)
            {
                int sampleOffset = frame * channels;
                for (int channel = 0; channel < channels; channel++)
                {
                    float value = interleavedSamples[sampleOffset + channel];
                    if (!float.IsFinite(value)) value = 0.0F;
                    if (!initialized)
                    {
                        min = max = value;
                        initialized = true;
                    }
                    else
                    {
                        min = Math.Min(min, value);
                        max = Math.Max(max, value);
                    }
                }
            }
            minimum[bucket] = min;
            maximum[bucket] = max;
        }

        return new WaveformEnvelope(minimum, maximum);
    }

    private WaveformAnalysisResult AnalyzeFile(
        string path,
        int preferredBucketCount,
        CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(path);
        if (!extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("GrassiLooper Gate 1 supports WAV and MP3 imports.");
        }

        using var reader = new AudioFileReader(path);
        if (reader.TotalTime > TimeSpan.FromMinutes(MaxImportMinutes))
        {
            throw new InvalidDataException($"Imported Master audio is limited to {MaxImportMinutes} minutes in the v1.4 MVP.");
        }

        ISampleProvider provider = reader;
        provider = provider.WaveFormat.Channels switch
        {
            1 => new MonoToStereoSampleProvider(provider),
            2 => provider,
            _ => throw new NotSupportedException("GrassiLooper currently imports mono or stereo audio.")
        };

        if (provider.WaveFormat.SampleRate != TargetSampleRate)
        {
            provider = new WdlResamplingSampleProvider(provider, TargetSampleRate);
        }

        int initialCapacity = (int)Math.Min(
            Math.Ceiling(reader.TotalTime.TotalSeconds * TargetSampleRate * TargetChannels),
            8_000_000.0);
        var samples = new List<float>(Math.Max(TargetChannels, initialCapacity));
        float[] buffer = new float[16_384];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int index = 0; index < read; index++)
            {
                samples.Add(float.IsFinite(buffer[index]) ? buffer[index] : 0.0F);
            }
        }

        int completeSampleCount = samples.Count - samples.Count % TargetChannels;
        if (completeSampleCount < TargetChannels)
        {
            throw new InvalidDataException("The selected audio file contains no decodable samples.");
        }
        if (completeSampleCount != samples.Count)
        {
            samples.RemoveRange(completeSampleCount, samples.Count - completeSampleCount);
        }

        float[] result = samples.ToArray();
        long frameCount = result.LongLength / TargetChannels;
        return new WaveformAnalysisResult(
            result,
            frameCount,
            TimeSpan.FromSeconds(frameCount / (double)TargetSampleRate),
            BuildEnvelope(result, TargetChannels, preferredBucketCount));
    }
}
