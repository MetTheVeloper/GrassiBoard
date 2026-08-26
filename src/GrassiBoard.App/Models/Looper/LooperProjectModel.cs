using System.Collections.ObjectModel;

namespace GrassiBoard.Models.Looper;

internal sealed class LooperProjectModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "Untitled Loop";
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int SampleRate { get; init; } = 48_000;
    public LooperMasterModel? Master { get; set; }
    public ObservableCollection<LooperTrackModel> Tracks { get; } = [];
    public bool HasMaster => Master is not null;
    public bool CanRedefineMaster => Tracks.Count == 0;
    public long MasterFrameCount => Master?.FrameCount ?? 0L;
}
