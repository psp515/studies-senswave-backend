using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.DataSources.BrokerConnection.Interfaces;
using Senswave.Devices.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Mqtt;
using Senswave.TestInfrastructure.TestSetup.Models;

namespace Senswave.Devices.EndTests.Mqtt.Widgets.Types;

[Collection("Mqtt E2E Tests Collection")]
[Trait("Collection", "MqttEndTest")]
public class ButtonActionMqttTests(MqttTestEnvironment factory) : MqttFeatureTest(factory)
{
    [Fact]
    public async Task BooleanOperationsWorks()
    {
        // Arrange
        var client = CreateClient();
        var (broker, _, device) = await Arrange();

        var value = true;
        var operation = await PostBooleanOperation(device);
        var widget = await PostBooleanButtonWidget(operation, value);

        var clients = Services.GetRequiredService<IClientService>();

        var action = RequestFactory.CreateActionRequest(value: "").Serialize();

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
        Assert.Equal(value, lastOperationValue.Values.First().Value.GetValue<bool>());

        //Cleanup
        await StopBrokerClientInternal(broker);
    }

    [Fact]
    public async Task IntegerOperationsWorks()
    {
        // Arrange
        var client = CreateClient();
        var (broker, _, device) = await Arrange();

        var value = -89;
        var operation = await PostIntegerRangedOperation(device);
        var widget = await PostIntegerButtonWidget(operation, value);

        var clients = Services.GetRequiredService<IClientService>();

        var action = RequestFactory.CreateActionRequest(value: "").Serialize();

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
        Assert.Equal(value, lastOperationValue.Values.First().Value.GetValue<int>());

        //Cleanup
        await StopBrokerClientInternal(broker);
    }

    [Fact]
    public async Task NumberOperationsWorks()
    {
        // Arrange
        var client = CreateClient();
        var (broker, _, device) = await Arrange();

        var value = 42.42356678;
        var operation = await PostNumberRangedOperation(device);
        var widget = await PostNumberButtonWidget(operation, value);

        var clients = Services.GetRequiredService<IClientService>();

        var action = RequestFactory.CreateActionRequest(value: null).Serialize();

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
        Assert.Equal(value, lastOperationValue.Values.First().Value.GetValue<double>());

        //Cleanup
        await StopBrokerClientInternal(broker);
    }

    [Fact]
    public async Task TextOperationsWorks()
    {
        // Arrange
        var client = CreateClient();
        var (broker, _, device) = await Arrange();

        var value = "lolololl";
        var operation = await PostTextOperation(device);
        var widget = await PostTextButtonWidget(operation, value);

        var clients = Services.GetRequiredService<IClientService>();

        var action = RequestFactory.CreateActionRequest(value: "").Serialize();

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

    [Fact]
    public async Task OptionsOperationWorks()
    {
        // Arrange
        var client = CreateClient();
        var (broker, _, device) = await Arrange();

        var value = "Option4";
        var operation = await PostOptionsOperation(device);
        var widget = await PostOptionOperationWidget(operation, value);

        var clients = Services.GetRequiredService<IClientService>();

        var action = RequestFactory.CreateActionRequest(value: "").Serialize();

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

    [Fact]
    public async Task HexColorOperationWorks()
    {
        // Arrange
        var client = CreateClient();
        var (broker, _, device) = await Arrange();

        var value = "#FF00FF";
        var operation = await PostHexColorOperation(device);
        var widget = await PostHexColorButtonWidget(operation, value);

        var clients = Services.GetRequiredService<IClientService>();

        var action = RequestFactory.CreateActionRequest(value: "").Serialize();

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

    [Fact]
    public async Task FailsWhenValueProvided()
    {
        // Arrange
        var client = CreateClient();
        var (broker, _, device) = await Arrange();

        var operation = await PostBooleanOperation(device);
        var widget = await PostBooleanButtonWidget(operation);

        var clients = Services.GetRequiredService<IClientService>();

        var action = RequestFactory.CreateActionRequest(value: "true").Serialize();

        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync($"{Paths.WidgetsPath}/{widget}/action", action);

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        //Cleanup
        await StopBrokerClientInternal(broker);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task PresenceGatesButtonAction()
    {
        // Arrange
        var client = CreateClient();
        var (broker, _, device) = await Arrange();
        var widgetOperation = await PostBooleanOperation(device);
        var presenceOperation = await PostBooleanOperation(device);
        var widget = await PostBooleanButtonWidget(widgetOperation);
        await PatchDeviceWithBooleanPresence(device, presenceOperation);

        var clients = Services.GetRequiredService<IClientService>();
        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        var action = RequestFactory.CreateActionRequest(value: "").Serialize();
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

    private async Task<(Guid, Guid, Guid)> Arrange()
    {
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var deviceId = await PostDevice(home);

        return (broker, home, deviceId);
    }
}
