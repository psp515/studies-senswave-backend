using Grpc.Core;
using Grpc.Net.Client.Web;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Contracts.LiveUpdates.V1;
using Senswave.LiveUpdates.Api.Services.DevicesUpdates;

namespace Senswave.LiveUpdates.EndTestsGrpcWeb;

[Trait("Collection", "EndTestGrpcWeb")]
public class LiveUpdatesGrpcWebTests(GrpcWebTestEnvironment environment) : GrpcWebFeatureTest(environment)
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Subscribe → Initialized, then one published update arrives typed while the stream stays open.
    /// Two messages prove incremental delivery, the property gRPC-Web puts at risk; both wire formats are covered.
    /// </summary>
    [Theory]
    [InlineData(GrpcWebMode.GrpcWeb)]
    [InlineData(GrpcWebMode.GrpcWebText)]
    public async Task SubscribeReceivesUpdatesIncrementally(GrpcWebMode mode)
    {
        // Arrange
        WebMode = mode;
        var channel = await CreateAuthorizedChannelAsUser();
        var homeId = await channel.CreateHomeWithBrokerAsync();
        var deviceId = await channel.CreateDeviceAsync(homeId);

        using var streamLifetime = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        using var call = new LiveUpdatesService.LiveUpdatesServiceClient(channel)
            .Subscribe(new SubscribeRequest { HomeReferenceId = homeId }, cancellationToken: streamLifetime.Token);

        // Initialized
        var initialized = await ReadNext(call);
        Assert.Equal(LiveUpdate.UpdateOneofCase.Initialized, initialized.UpdateCase);

        // DeviceTileUpdate
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IDevicesUpdateService>().UpdateDeviceTile(Guid.Parse(deviceId));

        var tile = await ReadNext(call);
        Assert.Equal(deviceId, tile.DeviceTile.DeviceId);

        streamLifetime.Cancel();
    }

    /// <summary>Every read is bounded: a missing update fails the test instead of hanging the suite.</summary>
    private static async Task<LiveUpdate> ReadNext(AsyncServerStreamingCall<LiveUpdate> call)
    {
        using var timeout = new CancellationTokenSource(ReadTimeout);

        Assert.True(await call.ResponseStream.MoveNext(timeout.Token), "The live-updates stream ended unexpectedly.");

        return call.ResponseStream.Current;
    }
}
