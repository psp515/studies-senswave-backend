using Microsoft.Extensions.DependencyInjection;
using Senswave.DataSources.BrokerConnection.Interfaces;
using Senswave.TestInfrastructure.TestEnvironments.Mqtt;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Mqtt.Devices;

[Collection("Mqtt E2E Tests Collection")]
[Trait("Collection", "MqttEndTest")]
public class TileActionMqttTests(MqttTestEnvironment factory) : MqttFeatureTest(factory)
{
    [Fact]
    public async Task OwnerCanInvokeActionAndReturnsRefreshedWidget()
    {
        // Arrange
        var client = CreateClient();
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var deviceId = await PostDevice(home);
        var operationId = await PostBooleanOperation(deviceId);
        await PatchDeviceWithSwitchTile(deviceId, operationId);

        var clients = Services.GetRequiredService<IClientService>();

        var tileAction = new JsonObject
        {
            ["value"] = true
        };

        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction.Serialize());
        var responseContent = await response.Content.ReadAsStringAsync();
        var device = JsonNode.Parse(responseContent)!.AsObject();

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(deviceId.ToString(), device["id"]!.ToString());

        // Cleanup
        await StopBrokerClientInternal(broker);
    }

    [Theory]
    [MemberData(nameof(CanActPriviliges))]
    public async Task FriendCanInvokeAction(string privilege)
    {
        // Arrange
        var client = CreateClient();
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var deviceId = await PostDevice(home);
        var operationId = await PostBooleanOperation(deviceId);

        await PatchDeviceWithSwitchTile(deviceId, operationId);
        await PrepareHomeSharing(home);
        await PrepareDeviceSharing(privilege, deviceId);

        var clients = Services.GetRequiredService<IClientService>();

        var tileAction = new JsonObject
        {
            ["value"] = true
        };

        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction.Serialize());

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Cleanup
        await StopBrokerClientInternal(broker);
        await RemoveSharings(home);
    }

    [Fact]
    public async Task FriendWithManageHomePrivilegeCanAct()
    {
        // Arrange
        var client = CreateClient();
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var deviceId = await PostDevice(home);
        var operationId = await PostBooleanOperation(deviceId);

        await PatchDeviceWithSwitchTile(deviceId, operationId);
        await PrepareHomeSharing(home);

        var clients = Services.GetRequiredService<IClientService>();

        var tileAction = new JsonObject
        {
            ["value"] = true
        };

        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction.Serialize());

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Cleanup
        await StopBrokerClientInternal(broker);
        await RemoveSharings(home);
    }

    [Fact]
    public async Task FriendCanActWithHomeDisplayPrivilege()
    {
        // Arrange
        var client = CreateClient();
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var deviceId = await PostDevice(home);
        var operationId = await PostBooleanOperation(deviceId);

        await PatchDeviceWithSwitchTile(deviceId, operationId);
        await PrepareHomeSharing(home, _displayPrivilege);

        var clients = Services.GetRequiredService<IClientService>();

        var tileAction = new JsonObject
        {
            ["value"] = true
        };

        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction.Serialize());

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

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
        var operationId = await PostBooleanOperation(deviceId);

        await PatchDeviceWithSwitchTile(deviceId, operationId);
        await PrepareHomeSharing(home);
        await PrepareDeviceSharing(_displayPrivilege, deviceId);

        var clients = Services.GetRequiredService<IClientService>();

        var tileAction = new JsonObject
        {
            ["value"] = true
        };

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
