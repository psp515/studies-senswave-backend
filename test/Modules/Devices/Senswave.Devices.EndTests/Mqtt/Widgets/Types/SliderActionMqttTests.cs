using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.DataSources.BrokerConnection.Interfaces;
using Senswave.Devices.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Mqtt;
using Senswave.TestInfrastructure.TestSetup.Models;

namespace Senswave.Devices.EndTests.Mqtt.Widgets.Types;

[Collection("Mqtt E2E Tests Collection")]
[Trait("Collection", "MqttEndTest")]
public class SliderActionMqttTests(MqttTestEnvironment factory) : MqttFeatureTest(factory)
{
    [Fact]
    public async Task IntegerOperationsWorks()
    {
        // Arrange
        var user = await CreateUser();
        var (broker, _, device) = await Arrange();
        var operation = await PostIntegerRangedOperation(device);
        var widget = await PostSliderWidget(operation);

        var clients = Services.GetRequiredService<IClientService>();
        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        var value = 50;
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
        var operation = await PostNumberRangedOperation(device);
        var widget = await PostSliderWidget(operation);

        var action = RequestFactory.CreateActionRequest(value: 0.50).Serialize();

        var clients = Services.GetRequiredService<IClientService>();
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
    public async Task PresenceGatesSliderAction()
    {
        // Arrange
        var user = await CreateUser();
        var (broker, _, device) = await Arrange();
        var widgetOperation = await PostIntegerRangedOperation(device);
        var presenceOperation = await PostBooleanOperation(device);
        var widget = await PostSliderWidget(widgetOperation);
        await PatchDeviceWithBooleanPresence(device, presenceOperation);

        var clients = Services.GetRequiredService<IClientService>();
        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        var action = RequestFactory.CreateActionRequest(value: 5).Serialize();

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
