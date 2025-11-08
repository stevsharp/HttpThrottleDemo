# HttpThrottleDemo

A tiny console that shows `SemaphoreSlim` based throttling for async HTTP work. You set a max concurrency, it runs a batch of requests, it reports latency and the highest concurrency observed.

## Requirements

* .NET 9 SDK

## Quick start

```bash
git clone <your-repo-url>
cd HttpThrottleDemo
dotnet run -- -c 8 -n 100 -t 30 -u "https://httpbin.org/delay/1?i={i}"
```

## What it does

* Limits parallel requests with a `SemaphoreSlim`
* Supports Ctrl+C and a global timeout
* Prints success and failure counts, average and P95 latency, max in flight

## Usage

```bash
dotnet run -- [options]
```

**Options**

* `-c`, `--concurrency`  max parallel requests, default 8
* `-n`, `--total`        total requests, default 100
* `-t`, `--timeout`      global timeout in seconds, default 30
* `-u`, `--url`          URL template, use `{i}` as index placeholder
* `-h`, `--help`         show help

**Examples**

```bash
# 12 concurrent requests, 200 total, 45 seconds timeout
dotnet run -- -c 12 -n 200 -t 45 -u "https://httpbin.org/delay/1?i={i}"

# Low concurrency against your own API
dotnet run -- -c 4 -n 50 -u "https://example.com/items/{i}"
```

## How it works

`HttpThrottler.RunAsync` accepts a delegate `Func<CancellationToken, Task<T>>` and a token.

* The token cancels waiting to enter, and it also flows into your HTTP call, so both admission and the work can be canceled with one source.
* The permit is released in `finally`, even if the work fails or is canceled.

## Files

* `HttpThrottleDemo.csproj`
* `Program.cs` with `HttpThrottler` and minimal options parsing

That is it. Tune `-c` for your API and network, keep an eye on the summary to verify the limit behaves as expected.

## Connect with Me

[![LinkedIn](https://img.shields.io/badge/LinkedIn-Profile-blue)](https://www.linkedin.com/in/spyros-ponaris-913a6937/)
