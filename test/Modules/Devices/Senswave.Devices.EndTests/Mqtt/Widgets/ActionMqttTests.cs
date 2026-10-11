using Microsoft.Extensions.DependencyInjection;
using Senswave.DataSources.BrokerConnection.Interfaces;
using Senswave.TestInfrastructure.TestEnvironments.Mqtt;
using Senswave.TestInfrastructure.TestSetup.Models;

namespace Senswave.Devices.EndTests.Mqtt.Widgets;

[Collection("Mqtt E2E Tests Collection")]
[Trait("Collection", "MqttEndTest")]
public class ActionMqttTests(MqttTestEnvironment factory) : MqttFeatureTest(factory)
{
    [Fact]
    public async Task OwnerCanInvokeAction()
    {
        // Arrange
        var client = CreateClient();
        var (broker, _, _, _, widget) = await Arrange();

        var clients = Services.GetRequiredService<IClientService>();

        var action = RequestFactory.CreateActionRequest(value: "").Serialize();

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

    [Theory]
    [MemberData(nameof(CanActPriviliges))]
    public async Task FriendCanInvokeAction(string privilege)
    {
        // Arrange
        var client = CreateClient();
        var (broker, home, device, _, widget) = await Arrange();
        await PrepareHomeSharing(home, _displayPrivilege);
        await PrepareDeviceSharing(privilege, device);

        var clients = Services.GetRequiredService<IClientService>();

        var action = RequestFactory.CreateActionRequest(value: "").Serialize();
        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.PostAsync($"{Paths.WidgetsPath}/{widget}/action", action);

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        //Cleanup
        await StopBrokerClientInternal(broker);
        await RemoveSharings(home);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task FriendCanNotInvokeAction()
    {
        // Arrange
        var client = CreateClient();
        var (broker, home, device, _, widget) = await Arrange();
        await PrepareHomeSharing(home, _displayPrivilege);
        await PrepareDeviceSharing(_displayPrivilege, device);

        var clients = Services.GetRequiredService<IClientService>();

        var action = RequestFactory.CreateActionRequest(value: "").Serialize();

        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.PostAsync($"{Paths.WidgetsPath}/{widget}/action", action);

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        //Cleanup
        await StopBrokerClientInternal(broker);
    }

    private async Task<(Guid, Guid, Guid, Guid, Guid)> Arrange()
    {
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var deviceId = await PostDevice(home);
        var operationId = await PostBooleanOperation(deviceId);
        var widgetId = await PostBooleanButtonWidget(operationId);

        return (broker, home, deviceId, operationId, widgetId);
    }
}
