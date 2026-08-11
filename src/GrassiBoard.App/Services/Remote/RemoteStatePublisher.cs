namespace GrassiBoard.Services.Remote;

internal sealed class RemoteStatePublisher
{
    private long _revision = 1;

    public long Revision => Interlocked.Read(ref _revision);

    public event Action<long>? Invalidated;

    public long Invalidate()
    {
        long revision = Interlocked.Increment(ref _revision);
        Invalidated?.Invoke(revision);
        return revision;
    }
}
