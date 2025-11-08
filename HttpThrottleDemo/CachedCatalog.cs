namespace HttpThrottleDemo;

public sealed class CachedCatalog
{
    private readonly ReaderWriterLockSlim _lock = new();
    private Dictionary<int, string> _data = new();

    public string? Get(int id)
    {
        _lock.EnterReadLock();
        try { return _data.TryGetValue(id, out var v) ? v : null; }
        finally { _lock.ExitReadLock(); }
    }

    public void Refresh(Dictionary<int, string> snapshot)
    {
        _lock.EnterWriteLock();
        try { _data = snapshot; }
        finally { _lock.ExitWriteLock(); }
    }
}

