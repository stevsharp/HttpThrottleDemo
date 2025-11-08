
// -----------------------------------------------------------------------------
// Throttler from the article, with both generic and non generic overloads
// -----------------------------------------------------------------------------
public sealed class HttpThrottler : IDisposable
{
    private readonly SemaphoreSlim _gate;
    private readonly int _capacity;

    public HttpThrottler(int maxConcurrency)
    {
        if (maxConcurrency <= 0) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
        _capacity = maxConcurrency;
        _gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { return await work(ct); }
        finally { _gate.Release(); }
    }

    public async Task RunAsync(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { await work(ct); }
        finally { _gate.Release(); }
    }

    public int Capacity => _capacity;
    public int PermitsInUse => _capacity - _gate.CurrentCount;

    public void Dispose() => _gate.Dispose();
}
