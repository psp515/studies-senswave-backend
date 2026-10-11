using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.DataSources.BrokerConnection.Interfaces;
using Senswave.Devices.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Mqtt;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Mqtt.Widgets.Types;

[Collection("Mqtt E2E Tests Collection")]
[Trait("Collection", "MqttEndTest")]
public class RadioActionMqttTests(MqttTestEnvironment factory) : MqttFeatureTest(factory)
{
    [Fact]
    public async Task RadioOperationsWorks()
    {
        // Arrange
        var user = await CreateUser();
        var (broker, _, device) = await Arrange();
        var operation = await PostOptionsOperation(device);
        var widget = await PostRadioWidget(operation);

        var clients = Services.GetRequiredService<IClientService>();
        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);
        var value = new JsonObject
        {
            ["value"] = "Option1"
        };

        using var scope = Factory.Services.CreateScope();
        var deviceContext = scope.ServiceProvider.GetRequiredService<DevicesContext>();

        // Act
        var response = await user.PostAsync($"{Paths.WidgetsPath}/{widget}/action", value.Serialize());

        var lastOperationValue = await deviceContext.Operations
            .Where(x => x.Id == operation)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstAsync();

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Option1", lastOperationValue.Values.First().Value.ToString());

        //Cleanup
        await StopBrokerClientInternal(broker);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task InvalidOperationOption()
    {
        // Arrange
        var user = await CreateUser();
        var (broker, _, device) = await Arrange();
        var operation = await PostOptionsOperation(device);
        var widget = await PostRadioWidget(operation);

        var clients = Services.GetRequiredService<IClientService>();
        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);
        var value = new JsonObject
        {
            ["value"] = "Option1123"
        };

        // Act
        var response = await user.PostAsync($"{Paths.WidgetsPath}/{widget}/action", value.Serialize());

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        //Cleanup
        await StopBrokerClientInternal(broker);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task PresenceGatesRadioAction()
    {
        // Arrange
        var user = await CreateUser();
        var (broker, _, device) = await Arrange();
        var widgetOperation = await PostOptionsOperation(device);
        var presenceOperation = await PostBooleanOperation(device);
        var widget = await PostRadioWidget(widgetOperation);
        await PatchDeviceWithBooleanPresence(device, presenceOperation);

        var clients = Services.GetRequiredService<IClientService>();
        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        var action = new JsonObject { ["value"] = "Option1" }.Serialize();

        // Act 1 — no presence values → 409
        var response1 = await user.PostAsync($"{Paths.WidgetsPath}/{widget}/action", action);

        // Set presence to true
        await SetPresenceOperationValue(presenceOperation, true);

        // Act 2 — presence true → 204
        var response2 = await user.PostAsync($"{Paths.WidgetsPath}/{widget}/action", action);

        // Set presence to false (newer)
        await SetPresenceOperationValue(presenceOperation, false, DateTime.UtcNow.AddSeconds(1));

        // Act 3 — presence false → 409
        var response3 = await user.PostAsync($"{Paths.WidgetsPath}/{widget}/action", action);

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.Conflict, response1.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, response2.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, response3.StatusCode);

        // Cleanup
        await StopBrokerClientInternal(broker);
    }

    private async Task<(Guid, Guid, Guid)> Arrange()
    {
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var deviceId = await PostDevice(home);

        return (broker, home, deviceId);
    }
}
