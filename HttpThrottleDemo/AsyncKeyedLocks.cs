
using System.Collections.Concurrent;

namespace HttpThrottleDemo;

public sealed class AsyncKeyedLocks<TKey> where TKey : notnull
{
    private sealed class RefCount
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public int Count;
    }

    private readonly ConcurrentDictionary<TKey, RefCount> _map = new();

    public async Task<IDisposable> LockAsync(TKey key, CancellationToken ct)
    {
        var rc = _map.GetOrAdd(key, _ => new RefCount());
        Interlocked.Increment(ref rc.Count);
        await rc.Gate.WaitAsync(ct);
        return new Releaser(key, this);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly TKey _key;
        private readonly AsyncKeyedLocks<TKey> _owner;
        private bool _disposed;
        public Releaser(TKey key, AsyncKeyedLocks<TKey> owner) { _key = key; _owner = owner; }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var rc = _owner._map[_key];
            rc.Gate.Release();
            if (Interlocked.Decrement(ref rc.Count) == 0)
            {
                _owner._map.TryRemove(_key, out _);
                rc.Gate.Dispose();
            }
        }
    }
}

