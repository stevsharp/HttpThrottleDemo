using System.Diagnostics;
using System.Threading.Channels;

Console.Title = "HttpThrottleDemo";

// Parse args
var opts = Options.Parse(args);

// Ctrl+C support
using var cts = new CancellationTokenSource(opts.Timeout);
Console.CancelKeyPress += (_, e) => { 
    e.Cancel = true; cts.Cancel(); 
};

Console.WriteLine($"Mode={(opts.UseChannel ? "Channel" : "Throttler")}, concurrency={opts.Concurrency}, total={opts.Total}, timeout={opts.Timeout}");

if (opts.UseChannel) 
    Console.WriteLine($"ChannelCapacity={opts.ChannelCapacity}");

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

int inFlight = 0;
int maxInFlight = 0;
int ok = 0;
int fail = 0;
var swAll = Stopwatch.StartNew();

try
{
    var urls = Enumerable.Range(0, opts.Total)
                         .Select(i => opts.UrlTemplate.Replace("{i}", i.ToString()))
                         .ToArray();

    //opts.UseChannel = true;     // force channel mode testing purposes
    //opts.ChannelCapacity = 8;   // desired capacity

    if (opts.UseChannel)
    {
        // Channel mode with backpressure
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(capacity: opts.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        var writer = Task.Run(async () =>
        {
            foreach (var url in urls) await channel.Writer.WriteAsync(url, cts.Token);
            channel.Writer.Complete();
        });

        var consumers = Enumerable.Range(0, opts.Concurrency).Select(_ => Task.Run(async () =>
        {
            await foreach (var url in channel.Reader.ReadAllAsync(cts.Token))
            {
                Interlocked.Increment(ref inFlight);
                UpdateMax(ref maxInFlight, inFlight);
                var sw = Stopwatch.StartNew();
                try
                {
                    using var resp = await http.GetAsync(url, cts.Token);
                    resp.EnsureSuccessStatusCode();
                    Interlocked.Increment(ref ok);
                }
                catch
                {
                    Interlocked.Increment(ref fail);
                }
                finally
                {
                    Interlocked.Decrement(ref inFlight);
                }
            }
        }));

        await Task.WhenAll(consumers.Append(writer));
    }
    else
    {
        // Throttler mode
        using var throttler = new HttpThrottler(opts.Concurrency);

        var tasks = urls.Select(u =>
            throttler.RunAsync(async ct =>
            {
                Interlocked.Increment(ref inFlight);
                UpdateMax(ref maxInFlight, inFlight);
                var sw = Stopwatch.StartNew();
                try
                {
                    using var resp = await http.GetAsync(u, ct);
                    resp.EnsureSuccessStatusCode();
                    Interlocked.Increment(ref ok);
                }
                catch
                {
                    Interlocked.Increment(ref fail);
                }
                finally
                {
                    Interlocked.Decrement(ref inFlight);
                }

                return true;
            }, cts.Token));

        await Task.WhenAll(tasks);
    }

    swAll.Stop();

    Console.WriteLine();
    Console.WriteLine("Run summary");
    Console.WriteLine($"  Success: {ok}");
    Console.WriteLine($"  Failed : {fail}");
    Console.WriteLine($"  Max concurrent observed: {maxInFlight}");
    Console.WriteLine($"  Total time: {swAll.Elapsed}");
}
catch (OperationCanceledException)
{
    swAll.Stop();
    Console.WriteLine("\nCanceled by user or timeout.");
    Console.WriteLine($"Partial progress. Success={ok}, Failed={fail}, Max concurrent observed={maxInFlight}");
}

// helpers

static void UpdateMax(ref int target, int value)
{
    int snapshot;
    while ((snapshot = target) < value)
        Interlocked.CompareExchange(ref target, value, snapshot);
}

