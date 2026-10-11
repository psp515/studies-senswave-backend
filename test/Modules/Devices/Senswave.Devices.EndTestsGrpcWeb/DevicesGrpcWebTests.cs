using Google.Protobuf.WellKnownTypes;
using Senswave.Contracts.Devices.V1;
using HomeSharing = Senswave.Contracts.Homes.V1;

namespace Senswave.Devices.EndTestsGrpcWeb;

[Trait("Collection", "EndTestGrpcWeb")]
public class DevicesGrpcWebTests(GrpcWebTestEnvironment environment) : GrpcWebFeatureTest(environment)
{
    /// <summary>
    /// DevicesService: CreateDevice → GetDevice.
    /// </summary>
    [Fact]
    public async Task CreateThenGetDevice()
    {
        // Arrange
        var channel = await CreateAuthorizedChannelAsUser();
        var client = new DevicesService.DevicesServiceClient(channel);
        var homeId = await channel.CreateHomeWithBrokerAsync();

        // CreateDevice
        var created = await client.CreateDeviceAsync(new CreateDeviceRequest { HomeId = homeId, Name = "Lamp", Icon = "lamp" });

        // GetDevice
        var device = await client.GetDeviceAsync(new GetDeviceRequest { DeviceId = created.Id });

        Assert.Equal("Lamp", device.Name);
    }

    /// <summary>
    /// OperationsService: CreateOperation → GetOperation.
    /// </summary>
    [Fact]
    public async Task CreateThenGetOperation()
    {
        // Arrange
        var channel = await CreateAuthorizedChannelAsUser();
        var client = new OperationsService.OperationsServiceClient(channel);
        var deviceId = await channel.CreateDeviceAsync(await channel.CreateHomeWithBrokerAsync());

        // CreateOperation
        var operationId = await channel.CreateBooleanOperationAsync(deviceId);

        // GetOperation
        var operation = await client.GetOperationAsync(new GetOperationRequest { OperationId = operationId });

        Assert.Equal("Boolean", operation.Type);
        Assert.True(operation.Configuration.Fields["isJson"].BoolValue);
    }

    /// <summary>
    /// WidgetsService: CreateWidget → GetWidget.
    /// </summary>
    [Fact]
    public async Task CreateThenGetWidget()
    {
        // Arrange
        var channel = await CreateAuthorizedChannelAsUser();
        var client = new WidgetsService.WidgetsServiceClient(channel);
        var deviceId = await channel.CreateDeviceAsync(await channel.CreateHomeWithBrokerAsync());
        var operationId = await channel.CreateBooleanOperationAsync(deviceId);

        // CreateWidget
        var widgetId = await channel.CreateSwitchWidgetAsync(operationId);

        // GetWidget
        var widget = await client.GetWidgetAsync(new GetWidgetRequest { WidgetId = widgetId });

        Assert.Equal(deviceId, widget.DeviceId);
        Assert.Equal(operationId, widget.OperationId);
    }

    /// <summary>
    /// DashboardsService: CreateDashboard → GetDashboard.
    /// </summary>
    [Fact]
    public async Task CreateThenGetDashboard()
    {
        // Arrange
        var channel = await CreateAuthorizedChannelAsUser();
        var client = new DashboardsService.DashboardsServiceClient(channel);
        var deviceId = await channel.CreateDeviceAsync(await channel.CreateHomeWithBrokerAsync());

        // CreateDashboard
        var created = await client.CreateDashboardAsync(new CreateDashboardRequest
        {
            DeviceId = deviceId,
            Name = "Main",
            Icon = "default-icon",
            Configuration = Struct.Parser.ParseJson("""{ "rows": 6, "columns": 4 }"""),
        });

        // GetDashboard
        var dashboard = await client.GetDashboardAsync(new GetDashboardRequest { DashboardId = created.Id });

        Assert.Equal("Main", dashboard.Name);
    }

    /// <summary>
    /// SharingService: SetSharing → GetSharings, for a friend the home is already shared with.
    /// </summary>
    [Fact]
    public async Task ShareThenGetSharings()
    {
        // Arrange: the friend must be a member of the home before a device can be shared.
        var ownerChannel = await CreateAuthorizedChannelAsUser();
        var friendChannel = await CreateAuthorizedChannelAsAdmin();
        var homeId = await ownerChannel.CreateHomeWithBrokerAsync();
        var deviceId = await ownerChannel.CreateDeviceAsync(homeId);

        var invitation = await new HomeSharing.SharingService.SharingServiceClient(ownerChannel)
            .CreateSharingAsync(new HomeSharing.CreateSharingRequest { HomeId = homeId, FriendEmail = AdminEmail, SharingType = "Display" });
        await new HomeSharing.SharingService.SharingServiceClient(friendChannel)
            .AcceptSharingAsync(new HomeSharing.AcceptSharingRequest { Password = invitation.Password });

        var client = new SharingService.SharingServiceClient(ownerChannel);

        // SetSharing
        await client.SetSharingAsync(new SetSharingRequest { DeviceId = deviceId, FriendEmail = AdminEmail, SharingType = "Action" });

        // GetSharings
        var sharings = await client.GetSharingsAsync(new GetSharingsRequest { DeviceId = deviceId });

        Assert.Equal("Action", Assert.Single(sharings.Items, x => x.FriendEmail == AdminEmail).SharingType);
    }
}
