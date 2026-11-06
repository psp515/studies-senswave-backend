using System.Text.Json.Nodes;
using Perf.Core;
using Perf.Core.Runs;

namespace Perf.Client;

/// <summary>Records one live update: subscribes, sends one tile action and keeps the first update that names the device.</summary>
internal static class StreamSample
{
    public static async Task<ResponseSample> Record(IApiClient client, Scenario scenario, Transport transport, UserData user, CancellationToken ct)
    {
        var first = new TaskCompletionSource<(JsonNode Json, int Bytes)>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var subscription = await client.Subscribe(user.AccessToken, user.HomeId, _ => { }, ct,
            (json, bytes) =>
            {
                if (json.ToJsonString().Contains(user.StreamDeviceId, StringComparison.Ordinal))
                    first.TrySetResult((json, bytes));
            });

        await client.TileAction(user.AccessToken, user.StreamDeviceId, true, ct);
        var (body, size) = await first.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);

        var (call, encoding) = transport == Transport.Rest
            ? ("SignalR signalr/liveupdates/live, hub method Update", "json")
            : ("senswave.liveupdates.v1.LiveUpdatesService/Subscribe (one stream message)", "protobuf");
        return new ResponseSample(scenario.ToString(), transport.ToString(), call, encoding, size, body);
    }
}
