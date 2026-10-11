using Microsoft.Extensions.DependencyInjection;
using Senswave.DataSources.BrokerConnection.Interfaces;
using Senswave.TestInfrastructure.TestEnvironments.Mqtt;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Mqtt.Devices.Presence;

[Collection("Mqtt E2E Tests Collection")]
[Trait("Collection", "MqttEndTest")]
public class PresenceGatingMqttTests(MqttTestEnvironment factory) : MqttFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task PresenceGatesTileAction()
    {
        // Arrange
        var client = CreateClient();
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var deviceId = await PostDevice(home);
        var tileOperationId = await PostBooleanOperation(deviceId);
        var presenceOperationId = await PostBooleanOperation(deviceId);
        await PatchDeviceWithSwitchTile(deviceId, tileOperationId);
        await PatchDeviceWithBooleanPresence(deviceId, presenceOperationId);

        var clients = Services.GetRequiredService<IClientService>();
        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        var tileAction = new JsonObject { ["value"] = true };

        await AuthorizeClientAsUser(client);

        // Act 1 — no presence values → 409
        var response1 = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction.Serialize());

        // Set presence to true
        await SetPresenceOperationValue(presenceOperationId, true);

        // Act 2 — presence true → succeeds → 200
        var response2 = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction.Serialize());

        // Set presence to false (newer)
        await SetPresenceOperationValue(presenceOperationId, false, DateTime.UtcNow.AddSeconds(1));

        // Act 3 — presence false → 409
        var response3 = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction.Serialize());

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.Conflict, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, response3.StatusCode);

        // Cleanup
        await StopBrokerClientInternal(broker);
    }
}
