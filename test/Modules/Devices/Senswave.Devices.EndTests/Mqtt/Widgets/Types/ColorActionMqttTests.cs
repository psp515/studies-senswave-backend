using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.DataSources.BrokerConnection.Interfaces;
using Senswave.Devices.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Mqtt;
using Senswave.TestInfrastructure.TestSetup.Models;

namespace Senswave.Devices.EndTests.Mqtt.Widgets.Types;

[Collection("Mqtt E2E Tests Collection")]
[Trait("Collection", "MqttEndTest")]
public class ColorActionMqttTests(MqttTestEnvironment factory) : MqttFeatureTest(factory)
{
    [Fact]
    public async Task ColorOperationsWorks()
    {
        // Arrange
        var client = CreateClient();
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var device = await PostDevice(home);
        var operation = await PostHexColorOperation(device);
        var widget = await PostColorWidget(operation);

        var value = "#FF0000";
        var clients = Services.GetRequiredService<IClientService>();
        var action = RequestFactory.CreateActionRequest(value: value).Serialize();

        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        using var scope = Factory.Services.CreateScope();
        var deviceContext = scope.ServiceProvider.GetRequiredService<DevicesContext>();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync($"{Paths.WidgetsPath}/{widget}/action", action);

        var lastOperationValue = await deviceContext.Operations
            .Where(x => x.Id == operation)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstAsync();

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(value, lastOperationValue.Values.First().Value.ToString());

        //Cleanup
        await StopBrokerClientInternal(broker);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task PresenceGatesColorAction()
    {
        // Arrange
        var client = CreateClient();
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var device = await PostDevice(home);
        var widgetOperation = await PostHexColorOperation(device);
        var presenceOperation = await PostBooleanOperation(device);
        var widget = await PostColorWidget(widgetOperation);
        await PatchDeviceWithBooleanPresence(device, presenceOperation);

        var clients = Services.GetRequiredService<IClientService>();
        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        var action = RequestFactory.CreateActionRequest(value: "#FF0000").Serialize();
        await AuthorizeClientAsUser(client);

        // Act 1 — no presence values → 409
        var response1 = await client.PostAsync($"{Paths.WidgetsPath}/{widget}/action", action);

        // Set presence to true
        await SetPresenceOperationValue(presenceOperation, true);

        // Act 2 — presence true → 204
        var response2 = await client.PostAsync($"{Paths.WidgetsPath}/{widget}/action", action);

        // Set presence to false (newer)
        await SetPresenceOperationValue(presenceOperation, false, DateTime.UtcNow.AddSeconds(1));

        // Act 3 — presence false → 409
        var response3 = await client.PostAsync($"{Paths.WidgetsPath}/{widget}/action", action);

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.Conflict, response1.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, response2.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, response3.StatusCode);

        // Cleanup
        await StopBrokerClientInternal(broker);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task EmptyIsInvalidColor()
    {
        // Arrange
        var client = CreateClient();
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var device = await PostDevice(home);
        var operation = await PostHexColorOperation(device);
        var widget = await PostColorWidget(operation);

        var clients = Services.GetRequiredService<IClientService>();
        var action = RequestFactory.CreateActionRequest(value: "");

        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync($"{Paths.WidgetsPath}/{widget}/action", action.Serialize());

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        //Cleanup
        await StopBrokerClientInternal(broker);
    }
}
