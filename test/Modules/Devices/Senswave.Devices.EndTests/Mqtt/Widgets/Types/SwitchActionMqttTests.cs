using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.DataSources.BrokerConnection.Interfaces;
using Senswave.Devices.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Mqtt;
using Senswave.TestInfrastructure.TestSetup.Models;

namespace Senswave.Devices.EndTests.Mqtt.Widgets.Types;

[Collection("Mqtt E2E Tests Collection")]
[Trait("Collection", "MqttEndTest")]
public class SwitchActionMqttTests(MqttTestEnvironment factory) : MqttFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task BooleanOperationsFails()
    {
        // Arrange
        var user = await CreateUser();
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var device = await PostDevice(home);
        var operation = await PostBooleanOperation(device);
        var widget = await PostSwitchWidget(operation);

        var clients = Services.GetRequiredService<IClientService>();
        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);
        var action = RequestFactory.CreateActionRequest(value: 2).Serialize();

        // Act
        var response = await user.PostAsync($"{Paths.WidgetsPath}/{widget}/action", action);

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        //Cleanup
        await StopBrokerClientInternal(broker);
    }

    [Fact]
    public async Task TrueBooleanOperationWorks()
    {
        // Arrange
        var user = await CreateUser();
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var device = await PostDevice(home);
        var operation = await PostBooleanOperation(device);
        var widget = await PostSwitchWidget(operation);

        var clients = Services.GetRequiredService<IClientService>();
        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        var value = true;
        var action = RequestFactory.CreateActionRequest(value: value).Serialize();

        using var scope = Factory.Services.CreateScope();
        var deviceContext = scope.ServiceProvider.GetRequiredService<DevicesContext>();

        // Act
        var response = await user.PostAsync($"{Paths.WidgetsPath}/{widget}/action", action);

        var lastOperationValue = await deviceContext.Operations
            .Where(x => x.Id == operation)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstAsync();

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(value, lastOperationValue.Values.First().Value.ToString() == "true");

        //Cleanup
        await StopBrokerClientInternal(broker);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task PresenceGatesSwitchAction()
    {
        // Arrange
        var user = await CreateUser();
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var device = await PostDevice(home);
        var widgetOperation = await PostBooleanOperation(device);
        var presenceOperation = await PostBooleanOperation(device);
        var widget = await PostSwitchWidget(widgetOperation);
        await PatchDeviceWithBooleanPresence(device, presenceOperation);

        var clients = Services.GetRequiredService<IClientService>();
        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        var action = RequestFactory.CreateActionRequest(value: true).Serialize();

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

    [Fact]
    public async Task FalseBooleanOperationWorks()
    {
        // Arrange
        var user = await CreateUser();
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var device = await PostDevice(home);
        var operation = await PostBooleanOperation(device);
        var widget = await PostSwitchWidget(operation);

        var clients = Services.GetRequiredService<IClientService>();
        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);
        var action = RequestFactory.CreateActionRequest(value: false).Serialize();

        // Act
        var response = await user.PostAsync($"{Paths.WidgetsPath}/{widget}/action", action);

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        //Cleanup
        await StopBrokerClientInternal(broker);
    }
}
