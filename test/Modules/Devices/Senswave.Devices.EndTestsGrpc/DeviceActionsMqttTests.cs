using Google.Protobuf.WellKnownTypes;
using Senswave.Contracts.DataSources.V1;
using Senswave.Contracts.Devices.V1;
using Senswave.Contracts.Homes.V1;

namespace Senswave.Devices.EndTestsGrpc;

[Trait("Collection", "MqttEndTestGrpc")]
public class DeviceActionsMqttTests(GrpcMqttTestEnvironment environment) : GrpcMqttFeatureTest(environment)
{
    /// <summary>
    /// DeviceTileAction and WidgetsService.Action against a home whose broker client is connected to Mosquitto.
    /// </summary>
    [Fact]
    public async Task TileAndWidgetActions()
    {
        // Arrange
        var mqtt = MqttEnvironment.GetMqttProvider();
        var channel = await CreateAuthorizedChannelAsUser();

        var brokerId = await channel.CreateMqttBrokerAsync(mqtt);
        var homeId = await channel.CreateHomeAsync();
        await new HomesService.HomesServiceClient(channel)
            .SetHomeDataSourceAsync(new SetHomeDataSourceRequest { HomeId = homeId, BrokerId = brokerId });

        var deviceId = await channel.CreateDeviceAsync(homeId);
        var operationId = await channel.CreateBooleanOperationAsync(deviceId);
        var widgetId = await channel.CreateSwitchWidgetAsync(operationId);

        var devices = new DevicesService.DevicesServiceClient(channel);
        await devices.UpdateDeviceAsync(new UpdateDeviceRequest { DeviceId = deviceId, OperationId = operationId, Type = "Switch" });

        await channel.StartClientAsync(brokerId, mqtt);

        try
        {
            // DeviceTileAction
            var tile = await devices.DeviceTileActionAsync(new DeviceTileActionRequest { DeviceId = deviceId, Value = Value.ForBool(true) });

            Assert.Equal(deviceId, tile.Device.Id);

            // WidgetsService.Action
            await new WidgetsService.WidgetsServiceClient(channel)
                .ActionAsync(new ActionRequest { WidgetId = widgetId, Value = Value.ForBool(false) });
        }
        finally
        {
            await new DataSourcesService.DataSourcesServiceClient(channel)
                .StopClientAsync(new StopClientRequest { BrokerId = brokerId });
        }
    }
}
