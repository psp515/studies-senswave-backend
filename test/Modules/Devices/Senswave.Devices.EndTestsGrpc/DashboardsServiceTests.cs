using Google.Protobuf.WellKnownTypes;
using Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.EndTestsGrpc;

[Trait("Collection", "EndTestGrpc")]
public class DashboardsServiceTests(GrpcTestEnvironment environment) : GrpcFeatureTest(environment)
{
    /// <summary>
    /// CreateDashboard → GetDashboard → DisplayDashboards → UpdateDashboard → SetWidgetOnDashboard → DisplayDashboard
    /// → DeleteDashboardWidget → DeleteDashboard.
    /// </summary>
    [Fact]
    public async Task DashboardLifecycle()
    {
        // Arrange
        var channel = await CreateAuthorizedChannelAsUser();
        var client = new DashboardsService.DashboardsServiceClient(channel);
        var deviceId = await channel.CreateDeviceAsync(await channel.CreateHomeWithBrokerAsync());
        var widgetId = await channel.CreateSwitchWidgetAsync(await channel.CreateBooleanOperationAsync(deviceId));

        // CreateDashboard
        var created = await client.CreateDashboardAsync(new CreateDashboardRequest
        {
            DeviceId = deviceId,
            Name = "Main",
            Icon = "default-icon",
            Configuration = Struct.Parser.ParseJson("""{ "rows": 6, "columns": 4 }"""),
        });
        var dashboardId = created.Id;

        // GetDashboard + DisplayDashboards
        var dashboard = await client.GetDashboardAsync(new GetDashboardRequest { DashboardId = dashboardId });
        var dashboards = await client.DisplayDashboardsAsync(new DisplayDashboardsRequest { DeviceId = deviceId });

        Assert.Equal("Main", dashboard.Name);
        Assert.Contains(dashboards.Items, x => x.Id == dashboardId);

        // UpdateDashboard
        await client.UpdateDashboardAsync(new UpdateDashboardRequest { DashboardId = dashboardId, Name = "Renamed", Icon = "default-icon" });
        var updated = await client.GetDashboardAsync(new GetDashboardRequest { DashboardId = dashboardId });

        Assert.Equal("Renamed", updated.Name);

        // SetWidgetOnDashboard + DisplayDashboard
        await client.SetWidgetOnDashboardAsync(new SetWidgetOnDashboardRequest
        {
            DashboardId = dashboardId,
            WidgetId = widgetId,
            Row = 0,
            RowSpan = 1,
            Column = 0,
            ColumnSpan = 2,
        });
        var display = await client.DisplayDashboardAsync(new DisplayDashboardRequest { DashboardId = dashboardId });

        Assert.False(string.IsNullOrEmpty(display.Type));

        // DeleteDashboardWidget + DeleteDashboard
        await client.DeleteDashboardWidgetAsync(new DeleteDashboardWidgetRequest { DashboardId = dashboardId, WidgetId = widgetId });
        await client.DeleteDashboardAsync(new DeleteDashboardRequest { DashboardId = dashboardId });
    }
}
