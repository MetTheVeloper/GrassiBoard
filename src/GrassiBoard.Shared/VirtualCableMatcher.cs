using System.Text;

namespace GrassiBoard.Shared;

public sealed record AudioEndpointDescriptor(
    string Id,
    string Name,
    string ContainerId,
    bool IsDefault);

public static class VirtualCableMatcher
{
    private static readonly string[] VirtualMarkers =
    [
        "virtual",
        "cable",
        "voicemeeter",
        "vac"
    ];

    private static readonly HashSet<string> DirectionWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "input",
        "output",
        "playback",
        "recording",
        "speaker",
        "speakers",
        "microphone",
        "mic",
        "headphone",
        "headphones",
        "earphone",
        "headset",
        "line",
        "device"
    };

    public static bool IsExternalVirtualEndpoint(AudioEndpointDescriptor endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        string name = endpoint.Name;
        return !name.Contains("GrassiBoard", StringComparison.OrdinalIgnoreCase) &&
            VirtualMarkers.Any(marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    public static AudioEndpointDescriptor? FindPairedCaptureEndpoint(
        AudioEndpointDescriptor renderEndpoint,
        IEnumerable<AudioEndpointDescriptor> captureEndpoints)
    {
        ArgumentNullException.ThrowIfNull(renderEndpoint);
        ArgumentNullException.ThrowIfNull(captureEndpoints);

        if (!IsExternalVirtualEndpoint(renderEndpoint))
        {
            return null;
        }

        AudioEndpointDescriptor[] candidates = captureEndpoints
            .Where(endpoint => endpoint.Id != renderEndpoint.Id && IsExternalVirtualEndpoint(endpoint))
            .ToArray();

        if (!string.IsNullOrWhiteSpace(renderEndpoint.ContainerId))
        {
            AudioEndpointDescriptor? containerMatch = candidates.FirstOrDefault(endpoint =>
                string.Equals(endpoint.ContainerId, renderEndpoint.ContainerId, StringComparison.OrdinalIgnoreCase));
            if (containerMatch is not null)
            {
                return containerMatch;
            }
        }

        string renderFamily = BuildFamilyKey(renderEndpoint.Name);
        return renderFamily.Length == 0
            ? null
            : candidates.FirstOrDefault(endpoint =>
                string.Equals(BuildFamilyKey(endpoint.Name), renderFamily, StringComparison.Ordinal));
    }

    private static string BuildFamilyKey(string name)
    {
        var result = new StringBuilder(name.Length);
        var word = new StringBuilder();

        void FlushWord()
        {
            if (word.Length == 0)
            {
                return;
            }

            string token = word.ToString();
            if (!DirectionWords.Contains(token))
            {
                result.Append(token.ToLowerInvariant());
            }
            word.Clear();
        }

        foreach (char character in name)
        {
            if (char.IsLetterOrDigit(character))
            {
                word.Append(character);
            }
            else
            {
                FlushWord();
            }
        }
        FlushWord();
        return result.ToString();
    }
}
