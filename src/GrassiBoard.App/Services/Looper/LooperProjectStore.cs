using GrassiBoard.Models.Looper;

namespace GrassiBoard.Services.Looper;

internal sealed class LooperProjectStore
{
    public const int MaxTracks = 32;

    public LooperProjectModel Current { get; private set; } = CreateProject();

    public LooperProjectModel Reset()
    {
        Current = CreateProject();
        return Current;
    }

    public void SetMaster(LooperMasterModel master)
    {
        ArgumentNullException.ThrowIfNull(master);
        if (!Current.CanRedefineMaster)
        {
            throw new InvalidOperationException("Remove child tracks before redefining the Master Loop length.");
        }
        if (master.FrameCount <= 0 || master.Samples.Length == 0)
        {
            throw new ArgumentException("Master Loop must contain audio frames.", nameof(master));
        }

        Current.Master = master;
        Current.ModifiedAtUtc = DateTimeOffset.UtcNow;
    }

    public void AddTrack(LooperTrackModel track)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (Current.Master is null) throw new InvalidOperationException("Create a Master Loop before adding layers.");
        if (Current.Tracks.Count >= MaxTracks) throw new InvalidOperationException($"Gate 4 supports up to {MaxTracks} child layers.");
        if (track.Samples.LongLength != Current.Master.FrameCount)
        {
            throw new ArgumentException("Child Track buffer must match the exact Master frame count.", nameof(track));
        }
        Current.Tracks.Add(track);
        Current.ModifiedAtUtc = DateTimeOffset.UtcNow;
    }

    public bool RemoveTrack(LooperTrackModel track)
    {
        ArgumentNullException.ThrowIfNull(track);
        bool removed = Current.Tracks.Remove(track);
        if (removed) Current.ModifiedAtUtc = DateTimeOffset.UtcNow;
        return removed;
    }

    public void Touch() => Current.ModifiedAtUtc = DateTimeOffset.UtcNow;

    private static LooperProjectModel CreateProject() => new();
}
