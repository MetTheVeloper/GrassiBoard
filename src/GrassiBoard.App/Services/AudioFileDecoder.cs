using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GrassiBoard.Services;

internal sealed record DecodedAudio(float[] Samples, ulong FrameCount, TimeSpan Duration);

internal static class AudioFileDecoder
{
    private const int TargetSampleRate = 48_000;
    private const int TargetChannels = 2;
    private const int MaxDurationMinutes = 10;

    public static DecodedAudio Decode(string path)
    {
        string extension = Path.GetExtension(path);
        if (!extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only WAV and MP3 files are supported in v0.8.x.");
        }

        using var reader = new AudioFileReader(path);
        if (reader.TotalTime > TimeSpan.FromMinutes(MaxDurationMinutes))
        {
            throw new InvalidDataException($"Sound Pads are limited to {MaxDurationMinutes} minutes per file.");
        }

        ISampleProvider provider = reader;
        provider = provider.WaveFormat.Channels switch
        {
            1 => new MonoToStereoSampleProvider(provider),
            2 => provider,
            _ => throw new NotSupportedException("Sound Pads currently support mono or stereo audio files.")
        };

        if (provider.WaveFormat.SampleRate != TargetSampleRate)
        {
            provider = new WdlResamplingSampleProvider(provider, TargetSampleRate);
        }

        var samples = new List<float>(checked((int)Math.Min(
            (long)reader.TotalTime.TotalSeconds * TargetSampleRate * TargetChannels,
            8_000_000L)));
        float[] buffer = new float[16_384];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int index = 0; index < read; ++index)
            {
                samples.Add(float.IsFinite(buffer[index]) ? buffer[index] : 0.0F);
            }
        }

        if (samples.Count < TargetChannels)
        {
            throw new InvalidDataException("The selected audio file contains no decodable samples.");
        }

        int completeSampleCount = samples.Count - samples.Count % TargetChannels;
        if (completeSampleCount != samples.Count)
        {
            samples.RemoveRange(completeSampleCount, samples.Count - completeSampleCount);
        }
        float[] result = samples.ToArray();
        ulong frames = checked((ulong)(result.Length / TargetChannels));
        return new DecodedAudio(result, frames, TimeSpan.FromSeconds(frames / (double)TargetSampleRate));
    }
}
