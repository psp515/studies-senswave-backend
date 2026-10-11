using Microsoft.Extensions.DependencyInjection;
using Senswave.DataSources.BrokerConnection.Interfaces;
using Senswave.TestInfrastructure.TestEnvironments.Mqtt;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Mqtt.Devices.TileTypes;

[Collection("Mqtt E2E Tests Collection")]
[Trait("Collection", "MqttEndTest")]
public class DisplayTileActionMqttTests(MqttTestEnvironment factory) : MqttFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ActionNotSupportedForDisplayTile()
    {
        // Arrange
        var client = CreateClient();
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var deviceId = await PostDevice(home);
        var operationId = await PostNumberOperation(deviceId);
        await PatchDeviceWithDisplayTile(deviceId, operationId);

        var clients = Services.GetRequiredService<IClientService>();

        var tileAction = new JsonObject { ["value"] = 42.0 };

        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction.Serialize());

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Cleanup
        await StopBrokerClientInternal(broker);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task FriendCannotActWithDeviceDisplayPrivilege()
    {
        // Arrange
        var client = CreateClient();
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var deviceId = await PostDevice(home);
        var operationId = await PostNumberOperation(deviceId);

        await PatchDeviceWithDisplayTile(deviceId, operationId);
        await PrepareHomeSharing(home);
        await PrepareDeviceSharing(_displayPrivilege, deviceId);

        var clients = Services.GetRequiredService<IClientService>();

        var tileAction = new JsonObject { ["value"] = 42.0 };

        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction.Serialize());

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Cleanup
        await StopBrokerClientInternal(broker);
        await RemoveSharings(home);
    }
}
