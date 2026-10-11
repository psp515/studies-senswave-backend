using Google.Protobuf.WellKnownTypes;
using Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.EndTestsGrpc;

[Trait("Collection", "EndTestGrpc")]
public class WidgetsServiceTests(GrpcTestEnvironment environment) : GrpcFeatureTest(environment)
{
    /// <summary>
    /// CreateWidget → GetWidget → DisplayWidgets → State → DeleteWidget.
    /// Action needs a live broker client and is covered by <see cref="DeviceActionsMqttTests"/>.
    /// </summary>
    [Fact]
    public async Task WidgetLifecycle()
    {
        // Arrange
        var channel = await CreateAuthorizedChannelAsUser();
        var client = new WidgetsService.WidgetsServiceClient(channel);
        var deviceId = await channel.CreateDeviceAsync(await channel.CreateHomeWithBrokerAsync());
        var operationId = await channel.CreateBooleanOperationAsync(deviceId);

        // CreateWidget
        var created = await client.CreateWidgetAsync(new CreateWidgetRequest
        {
            OperationId = operationId,
            Name = "Power switch",
            Type = "Switch",
            Configuration = new Struct(),
        });
        var widgetId = created.Id;

        // GetWidget + DisplayWidgets
        var widget = await client.GetWidgetAsync(new GetWidgetRequest { WidgetId = widgetId });
        var widgets = await client.DisplayWidgetsAsync(new DisplayWidgetsRequest { DeviceId = deviceId });

        Assert.Equal("Power switch", widget.Name);
        Assert.Equal(deviceId, widget.DeviceId);
        Assert.Equal(operationId, widget.OperationId);
        Assert.Contains(widgets.Items, group => group.Operation.Id == operationId && group.Widgets.Any(x => x.Id == widgetId));

        // State
        await client.StateAsync(new StateRequest { WidgetId = widgetId, Enabled = !widget.Enabled });
        var toggled = await client.GetWidgetAsync(new GetWidgetRequest { WidgetId = widgetId });

        Assert.Equal(!widget.Enabled, toggled.Enabled);

        // DeleteWidget
        await client.DeleteWidgetAsync(new DeleteWidgetRequest { WidgetId = widgetId });
    }
}
