using System.Diagnostics;
using System.Net.Http;

Console.Title = "HttpThrottleDemo";

// Parse simple args
var opts = Options.Parse(args);

// Ctrl+C support
using var cts = new CancellationTokenSource(opts.Timeout);
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine($"Starting with concurrency={opts.Concurrency}, total={opts.Total}, timeout={opts.Timeout}");
Console.WriteLine($"URL template: {opts.UrlTemplate}");
Console.WriteLine("Press Ctrl+C to cancel.\n");

using var http = new HttpClient(new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    AutomaticDecompression = System.Net.DecompressionMethods.All
})
{
    Timeout = Timeout.InfiniteTimeSpan
};

var throttler = new HttpThrottler(opts.Concurrency);

int inFlight = 0;
int maxInFlight = 0;
int ok = 0;
int fail = 0;

var swAll = Stopwatch.StartNew();

try
{
    var tasks = Enumerable.Range(0, opts.Total).Select(i =>
        throttler.RunAsync(async ct =>
        {
            Interlocked.Increment(ref inFlight);
            UpdateMax(ref maxInFlight, inFlight);
            var sw = Stopwatch.StartNew();
            try
            {
                var url = opts.UrlTemplate.Replace("{i}", i.ToString());
                using var resp = await http.GetAsync(url, ct);
                resp.EnsureSuccessStatusCode();
                Interlocked.Increment(ref ok);
                var text = await resp.Content.ReadAsStringAsync(ct);
                return (i, elapsed: sw.Elapsed, ok: true, error: (Exception?)null, payloadBytes: text.Length);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref fail);
                return (i, elapsed: sw.Elapsed, ok: false, error: ex, payloadBytes: 0);
            }
            finally
            {
                Interlocked.Decrement(ref inFlight);
            }
        }, cts.Token));

    var results = await Task.WhenAll(tasks);

    swAll.Stop();

    // Print a short report
    var avgMs = results.Average(r => r.elapsed.TotalMilliseconds);
    var p95Ms = Percentile(results.Select(r => r.elapsed.TotalMilliseconds), 95);

    Console.WriteLine();
    Console.WriteLine("Run summary");
    Console.WriteLine($"  Success: {ok}");
    Console.WriteLine($"  Failed : {fail}");
    Console.WriteLine($"  Max concurrent observed: {maxInFlight}");
    Console.WriteLine($"  Avg latency: {avgMs:F1} ms");
    Console.WriteLine($"  P95 latency: {p95Ms:F1} ms");
    Console.WriteLine($"  Total time: {swAll.Elapsed}");
}
catch (OperationCanceledException)
{
    swAll.Stop();
    Console.WriteLine("\nCanceled by user or timeout.");
    Console.WriteLine($"Partial progress. Success={ok}, Failed={fail}, Max concurrent observed={maxInFlight}");
}
finally
{
    throttler.Dispose();
}

Console.ReadLine();

static void UpdateMax(ref int target, int value)
{
    int snapshot;
    while ((snapshot = target) < value)
        Interlocked.CompareExchange(ref target, value, snapshot);
}

static double Percentile(IEnumerable<double> values, double percentile)
{
    var arr = values.OrderBy(v => v).ToArray();
    if (arr.Length == 0) 
        return 0;
    var rank = (percentile / 100.0) * (arr.Length - 1);
    var low = (int)Math.Floor(rank);
    var high = (int)Math.Ceiling(rank);
    if (low == high) 
        return arr[low];
    var weight = rank - low;

    return arr[low] * (1 - weight) + arr[high] * weight;
}
