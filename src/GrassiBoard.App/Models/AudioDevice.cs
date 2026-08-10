using GrassiBoard.Shared;

namespace GrassiBoard.Models;

internal sealed record AudioDevice
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ContainerId { get; init; } = string.Empty;
    public bool IsDefault { get; init; }

    public AudioEndpointDescriptor ToDescriptor() => new(Id, Name, ContainerId, IsDefault);

    public override string ToString() => Name;
}
