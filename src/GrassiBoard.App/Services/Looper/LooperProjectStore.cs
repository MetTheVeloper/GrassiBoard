using GrassiBoard.Models.Looper;

namespace GrassiBoard.Services.Looper;

internal sealed class LooperProjectStore
{
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

    private static LooperProjectModel CreateProject() => new();
}
