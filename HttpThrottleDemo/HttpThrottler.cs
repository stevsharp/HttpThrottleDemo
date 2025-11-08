
// -----------------------------------------------------------------------------
// Throttler from the article, with both generic and non generic overloads
// -----------------------------------------------------------------------------
public sealed class HttpThrottler : IDisposable
{
    private readonly SemaphoreSlim _gate;
    private readonly int _capacity;

    public HttpThrottler(int maxConcurrency)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrency);

        _capacity = maxConcurrency;

        _gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="work"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { return await work(ct); }
        finally { _gate.Release(); }
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="work"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task RunAsync(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { 
            await work(ct); }
        finally { 
            _gate.Release(); 
        }
    }
    /// <summary>
    /// 
    /// </summary>
    public int Capacity => _capacity;
    /// <summary>
    /// 
    /// </summary>
    public int PermitsInUse => _capacity - _gate.CurrentCount;
    /// <summary>
    /// 
    /// </summary>
    public void Dispose() => _gate.Dispose();
}
