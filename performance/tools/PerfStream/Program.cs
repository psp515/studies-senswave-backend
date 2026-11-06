using System.Diagnostics;
using System.Text.Json;
using Perf.Client;
using Perf.Core;
using Perf.Core.Runs;
using Perf.Core.Streams;
using PerfStream;

var s = StreamSettings.FromEnvironment();
var data = JsonSerializer.Deserialize<RunData>(await File.ReadAllTextAsync(s.DataFile), PerfJson.Options)!;
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var clock = Stopwatch.StartNew();
long Now() => clock.Elapsed.Ticks;

var tracker = new DeliveryTracker();
await using var api = ApiClientFactory.Create(s.Transport, new ApiTarget(s.BaseUrl, s.Insecure));

// 1. One subscriber per user (its home), all confirmed before publishing starts.
var setup = new List<double>();
var subscriptions = new List<ILiveUpdateSubscription>();
IPublisher publisher;
try
{
    foreach (var user in data.Users)
    {
        var t0 = Now();
        subscriptions.Add(await api.Subscribe(user.AccessToken, user.HomeId, deviceId => tracker.Delivered(deviceId, Now()), cts.Token));
        setup.Add(TimeSpan.FromTicks(Now() - t0).TotalMilliseconds);
    }

    publisher = await MqttPublisher.Connect(s, cts.Token);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"setup failed after {subscriptions.Count}/{data.Users.Count} subscriptions: {ex}");
    foreach (var subscription in subscriptions)
        await subscription.DisposeAsync();
    return 1;
}

await using var publisherScope = publisher;

// 2. Open-model publishing: event i is due at start + i/rate, round-robin over users. Warm-up events are not recorded.
var warmup = s.Mode == "verify" ? 0 : s.WarmupSeconds;
var total = (warmup + s.DurationSeconds) * (long)s.Rate;
var measuredFrom = warmup * (long)s.Rate;
long failures = 0, behind = 0;
var slowPublish = TimeSpan.FromSeconds(1);
var inflight = new List<Task>();
var start = Now();
var measureStart = DateTimeOffset.UtcNow;
var cancelled = false;
var crashed = false;
StreamResult result = new(0, 0, 0, 0, [], []);
var measureEnd = measureStart;

try
{
    try
    {
        for (long i = 0; i < total && !cts.IsCancellationRequested; i++)
        {
            var due = start + TimeSpan.FromSeconds((double)i / s.Rate).Ticks;
            var wait = due - Now();
            if (wait > TimeSpan.TicksPerMillisecond)
                await Task.Delay(TimeSpan.FromTicks(wait), cts.Token);
            else if (wait < -TimeSpan.FromMilliseconds(100).Ticks && i >= measuredFrom)
                behind++;

            if (i == measuredFrom)
                measureStart = DateTimeOffset.UtcNow;

            var user = data.Users[(int)(i % data.Users.Count)];
            var publishedAt = Now();
            tracker.Published(user.StreamDeviceId, publishedAt, measured: i >= measuredFrom);
            var sequence = i / data.Users.Count;      // per-user counter: every device sees alternating values
            var measured = i >= measuredFrom;
            inflight.Add(Task.Run(async () =>
            {
                try
                {
                    await publisher.Publish(user, sequence, cts.Token);
                    // A publish call that takes over a second means the system under test is falling behind (backlog).
                    if (measured && TimeSpan.FromTicks(Now() - publishedAt) > slowPublish)
                        Interlocked.Increment(ref behind);
                }
                catch
                {
                    // A failed publish will never be delivered: drop it from the pending FIFO so later matches stay aligned.
                    tracker.Retract(user.StreamDeviceId, publishedAt);
                    if (measured)
                        Interlocked.Increment(ref failures);
                }
            }));
            if (i % 1000 == 0 && inflight.Count > 2000)
                inflight.RemoveAll(t => t.IsCompleted);
        }

        measureEnd = DateTimeOffset.UtcNow;
        await Task.WhenAll(inflight);
        await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);       // grace for in-flight deliveries
    }
    catch (OperationCanceledException)
    {
        cancelled = true;
        measureEnd = DateTimeOffset.UtcNow;
    }
    catch (Exception ex)
    {
        crashed = true;
        Console.Error.WriteLine($"run failed: {ex}");
        measureEnd = DateTimeOffset.UtcNow;
    }

    result = tracker.Finish(Now(), TimeSpan.FromSeconds(5));
    var ended = subscriptions.Count(x => x.Ended);

    // NaN (nothing delivered) cannot be serialized: report 0.
    static double Finite(double v) => double.IsFinite(v) ? v : 0;
    var setupP = Percentiles.Of(setup.ToArray(), 50, 95, 100).Select(Finite).ToArray();
    var latencyP = Percentiles.Of(result.LatenciesMs, 50, 90, 95, 99, 100).Select(Finite).ToArray();
    var summary = new StreamSummary(s.Scenario.ToString(), s.Transport.ToString(), s.Rate, subscriptions.Count,
        new Percentile3(setupP[0], setupP[1], setupP[2]),
        result.Published, result.Delivered, result.Lost, result.Unmatched, failures, behind,
        ended, measureStart, measureEnd,
        new Percentile5(latencyP[0], latencyP[1], latencyP[2], latencyP[3], latencyP[4]));
    var json = JsonSerializer.Serialize(summary, PerfJson.Options);
    await File.WriteAllTextAsync(s.Out, json);
    Console.WriteLine(json);

    // Raw samples: one row per measured delivery, receive time relative to the first publish and the delivery latency.
    if (s.RawOut is { Length: > 0 } rawOut && s.Mode == "run")
    {
        await using var file = File.Create(rawOut);
        await using var gzip = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionLevel.Fastest);
        await using var writer = new StreamWriter(gzip);
        await writer.WriteLineAsync("delivered_ms,latency_ms");
        for (var i = 0; i < result.LatenciesMs.Length; i++)
        {
            var deliveredMs = TimeSpan.FromTicks(result.DeliveredAtTicks[i] - start).TotalMilliseconds;
            await writer.WriteLineAsync(FormattableString.Invariant($"{deliveredMs:F3},{result.LatenciesMs[i]:F3}"));
        }
    }
}
finally
{
    foreach (var subscription in subscriptions)
    {
        try { await subscription.DisposeAsync(); } catch { /* best effort */ }
    }
}

if (cancelled)
    return 130;
if (crashed)
    return 1;
return s.Mode == "verify" && result.Delivered < Math.Max(1, data.Users.Count / 10) ? 1 : 0;
