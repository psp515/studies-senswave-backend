using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Contracts.Homes.V1;
using Senswave.Contracts.LiveUpdates.V1;
using Senswave.LiveUpdates.Api.Services.DataSourcesUpdates;
using Senswave.LiveUpdates.Api.Services.DevicesUpdates;

namespace Senswave.LiveUpdates.EndTestsGrpc;

[Trait("Collection", "EndTestGrpc")]
public class LiveUpdatesServiceTests(GrpcTestEnvironment environment) : GrpcFeatureTest(environment)
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Subscribe → Initialized, then each of the four update kinds published through the update services arrives typed.
    /// </summary>
    [Fact]
    public async Task SubscribeReceivesEveryUpdateKind()
    {
        // Arrange
        var channel = await CreateAuthorizedChannelAsUser();
        var homeId = await channel.CreateHomeAsync();
        var brokerId = await channel.CreateBrokerAsync();
        await new HomesService.HomesServiceClient(channel)
            .SetHomeDataSourceAsync(new SetHomeDataSourceRequest { HomeId = homeId, BrokerId = brokerId });
        var deviceId = await channel.CreateDeviceAsync(homeId);
        var widgetId = Guid.NewGuid();

        using var streamLifetime = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        using var call = new LiveUpdatesService.LiveUpdatesServiceClient(channel)
            .Subscribe(new SubscribeRequest { HomeReferenceId = homeId }, cancellationToken: streamLifetime.Token);

        // Initialized
        var initialized = await ReadNext(call);
        Assert.Equal(LiveUpdate.UpdateOneofCase.Initialized, initialized.UpdateCase);

        using var scope = Services.CreateScope();
        var devices = scope.ServiceProvider.GetRequiredService<IDevicesUpdateService>();
        var dataSources = scope.ServiceProvider.GetRequiredService<IDataSourcesUpdateService>();

        // DeviceTileUpdate
        await devices.UpdateDeviceTile(Guid.Parse(deviceId));
        var tile = await ReadNext(call);
        Assert.Equal(deviceId, tile.DeviceTile.DeviceId);

        // WidgetsUpdate
        await devices.UpdateWidgets(Guid.Parse(deviceId), [widgetId]);
        var widgets = await ReadNext(call);
        Assert.Equal(deviceId, widgets.Widgets.DeviceId);
        Assert.Equal(widgetId.ToString(), Assert.Single(widgets.Widgets.WidgetIds));

        // DevicePresenceUpdate
        await devices.UpdateDevicePresence(Guid.Parse(deviceId));
        var presence = await ReadNext(call);
        Assert.Equal(deviceId, presence.DevicePresence.DeviceId);

        // DataSourceStateUpdate
        await dataSources.UpdateDataSourceState(Guid.Parse(brokerId), "Connected", CancellationToken.None);
        var state = await ReadNext(call);
        Assert.Equal(brokerId, state.DataSourceState.DataSourceId);
        Assert.Equal("Connected", state.DataSourceState.State);

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
