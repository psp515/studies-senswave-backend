using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Senswave.Contracts.DataSources.V1;
using Senswave.Contracts.Devices.V1;
using Senswave.Contracts.Homes.V1;
using Senswave.TestInfrastructure.Fixtures.Mqtt;

namespace Senswave.TestInfrastructure.TestEnvironments.Grpc;

/// <summary>
/// Arrange helpers over gRPC, shared by every <c>*.EndTestsGrpc</c> project. In gRPC mode the REST routes are not
/// mapped, so arrange steps must go through the gRPC surface too.
/// </summary>
public static class GrpcArrange
{
    public static async Task<string> CreateBrokerAsync(this ChannelBase channel)
    {
        var client = new DataSourcesService.DataSourcesServiceClient(channel);

        var response = await client.CreateBrokerAsync(new CreateBrokerRequest
        {
            Name = Guid.NewGuid().ToString()[..20],
            Url = $"{Guid.NewGuid():N}.example.com",
            ClientName = Guid.NewGuid().ToString(),
            Port = Random.Shared.Next(5000, 10000),
            ProtocolVersion = "MqttV5",
        });

        return response.Id;
    }

    public static async Task<string> CreateHomeAsync(this ChannelBase channel, double latitude = 51, double longitude = 76)
    {
        var client = new HomesService.HomesServiceClient(channel);

        var response = await client.CreateHomeAsync(new CreateHomeRequest
        {
            Name = $"Home {Guid.NewGuid().ToString()[..8]}",
            Icon = "home",
            Latitude = latitude,
            Longitude = longitude,
        });

        return response.Id;
    }

    public static async Task<string> CreateHomeWithBrokerAsync(this ChannelBase channel)
    {
        var homeId = await channel.CreateHomeAsync();
        var brokerId = await channel.CreateBrokerAsync();

        await new HomesService.HomesServiceClient(channel)
            .SetHomeDataSourceAsync(new SetHomeDataSourceRequest { HomeId = homeId, BrokerId = brokerId });

        return homeId;
    }

    public static async Task<string> CreateRoomAsync(this ChannelBase channel, string homeId)
    {
        var client = new RoomsService.RoomsServiceClient(channel);

        var response = await client.CreateRoomAsync(new CreateRoomRequest
        {
            HomeId = homeId,
            Name = $"Room {Guid.NewGuid().ToString()[..8]}",
        });

        return response.Id;
    }

    public static async Task<string> CreateMqttBrokerAsync(this ChannelBase channel, IMqttFixture mqtt)
    {
        var client = new DataSourcesService.DataSourcesServiceClient(channel);

        var response = await client.CreateBrokerAsync(new CreateBrokerRequest
        {
            Name = "Mqtt Broker",
            Url = mqtt.Hostname,
            ClientName = Guid.NewGuid().ToString(),
            Port = mqtt.Port,
            ProtocolVersion = mqtt.Version,
            UseTls = mqtt.UseTls,
            Username = mqtt.Username,
            Password = mqtt.Password,
        });

        return response.Id;
    }

    public static async Task StartClientAsync(this ChannelBase channel, string brokerId, IMqttFixture mqtt) =>
        await new DataSourcesService.DataSourcesServiceClient(channel)
            .StartClientAsync(new StartClientRequest { BrokerId = brokerId, Username = mqtt.Username, Password = mqtt.Password });

    public static async Task<string> CreateDeviceAsync(this ChannelBase channel, string homeId)
    {
        var client = new DevicesService.DevicesServiceClient(channel);

        var response = await client.CreateDeviceAsync(new CreateDeviceRequest
        {
            HomeId = homeId,
            Name = $"Device {Guid.NewGuid().ToString()[..8]}",
            Icon = "device",
        });

        return response.Id;
    }

    public static async Task<string> CreateBooleanOperationAsync(this ChannelBase channel, string deviceId)
    {
        var client = new OperationsService.OperationsServiceClient(channel);

        var response = await client.CreateOperationAsync(new CreateOperationRequest
        {
            DeviceId = deviceId,
            Name = Guid.NewGuid().ToString()[..29],
            Type = "Boolean",
            Topic = Guid.NewGuid().ToString().Replace("-", "/"),
            Configuration = Struct.Parser.ParseJson("""{ "jsonNames": ["value"], "isJson": true, "saveOnUserAction": true }"""),
        });

        return response.Id;
    }

    public static async Task<string> CreateSwitchWidgetAsync(this ChannelBase channel, string operationId)
    {
        var client = new WidgetsService.WidgetsServiceClient(channel);

        var response = await client.CreateWidgetAsync(new CreateWidgetRequest
        {
            OperationId = operationId,
            Name = $"Widget {Guid.NewGuid()}",
            Type = "Switch",
            Configuration = new Struct(),
        });

        return response.Id;
    }
}
