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

    publisher = s.Scenario == Scenario.S6a ? await MqttPublisher.Connect(s, cts.Token) : new TileActionPublisher(api);
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
var inflight = new List<Task>();
var start = Now();
var measureStart = DateTimeOffset.UtcNow;

try
{
    for (long i = 0; i < total && !cts.IsCancellationRequested; i++)
    {
        var due = start + TimeSpan.FromSeconds((double)i / s.Rate).Ticks;
        var wait = due - Now();
        if (wait > TimeSpan.TicksPerMillisecond)
            await Task.Delay(TimeSpan.FromTicks(wait), cts.Token);
        else if (wait < -TimeSpan.FromMilliseconds(100).Ticks)
            behind++;

        if (i == measuredFrom)
            measureStart = DateTimeOffset.UtcNow;

        var user = data.Users[(int)(i % data.Users.Count)];
        tracker.Published(user.DeviceId, Now(), measured: i >= measuredFrom);
        var sequence = i;
        inflight.Add(Task.Run(async () =>
        {
            try { await publisher.Publish(user, sequence, cts.Token); }
            catch { Interlocked.Increment(ref failures); }
        }));
        if (inflight.Count > 2000)
            inflight.RemoveAll(t => t.IsCompleted);
    }
}
catch (OperationCanceledException)
{
    // cancelled: report what happened so far
}

var measureEnd = DateTimeOffset.UtcNow;
await Task.WhenAll(inflight);
await Task.Delay(TimeSpan.FromSeconds(5));           // grace for in-flight deliveries
var result = tracker.Finish(Now(), TimeSpan.FromSeconds(5));

var ended = subscriptions.Count(x => x.Ended);
foreach (var subscription in subscriptions)
    await subscription.DisposeAsync();

var setupP = Percentiles.Of(setup.ToArray(), 50, 95, 100);
var latencyP = Percentiles.Of(result.LatenciesMs, 50, 90, 95, 99, 100);
var summary = new StreamSummary(s.Scenario.ToString(), s.Transport.ToString(), s.Rate, subscriptions.Count,
    new Percentile3(setupP[0], setupP[1], setupP[2]),
    result.Published, result.Delivered, result.Lost, result.Unmatched, failures, behind,
    ended, measureStart, measureEnd,
    new Percentile5(latencyP[0], latencyP[1], latencyP[2], latencyP[3], latencyP[4]));
await File.WriteAllTextAsync(s.Out, JsonSerializer.Serialize(summary, PerfJson.Options));

Console.WriteLine(JsonSerializer.Serialize(summary, PerfJson.Options));
return s.Mode == "verify" && result.Delivered < Math.Max(1, data.Users.Count / 10) ? 1 : 0;
