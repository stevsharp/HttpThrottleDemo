
// -----------------------------------------------------------------------------
// Minimal options parsing
// -----------------------------------------------------------------------------
public sealed record Options(int Concurrency, int Total, TimeSpan Timeout, string UrlTemplate)
{
    public static Options Parse(string[] args)
    {
        // Defaults: 8 concurrent, 100 requests, 30 seconds, httpbin delay endpoint
        int concurrency = 8;
        int total = 100;
        TimeSpan timeout = TimeSpan.FromSeconds(30);
        string url = "https://httpbin.org/delay/1?i={i}";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-c":
                case "--concurrency":
                    concurrency = int.Parse(args[++i]);
                    break;

                case "-n":
                case "--total":
                    total = int.Parse(args[++i]);
                    break;

                case "-t":
                case "--timeout":
                    timeout = TimeSpan.FromSeconds(double.Parse(args[++i]));
                    break;

                case "-u":
                case "--url":
                    url = args[++i];
                    break;

                case "-h":
                case "--help":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
            }
        }

        return new Options(concurrency, total, timeout, url);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("HttpThrottleDemo usage");
        Console.WriteLine("  -c, --concurrency   Max parallel requests (default 8)");
        Console.WriteLine("  -n, --total         Total number of requests (default 100)");
        Console.WriteLine("  -t, --timeout       Global timeout in seconds (default 30)");
        Console.WriteLine("  -u, --url           URL template. Use {i} as the index placeholder.");
        Console.WriteLine();
        Console.WriteLine("Examples");
        Console.WriteLine("  dotnet run -- -c 12 -n 200 -t 45 -u \"https://httpbin.org/delay/1?i={i}\"");
        Console.WriteLine("  dotnet run -- -c 4 -n 50 -u \"https://example.com/{i}\"");
    }
}
