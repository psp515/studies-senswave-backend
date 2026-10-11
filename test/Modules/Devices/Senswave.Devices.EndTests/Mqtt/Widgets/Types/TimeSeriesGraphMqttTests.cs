using Microsoft.Extensions.DependencyInjection;
using Senswave.DataSources.BrokerConnection.Interfaces;
using Senswave.TestInfrastructure.TestEnvironments.Mqtt;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Mqtt.Widgets.Types;

[Collection("Mqtt E2E Tests Collection")]
[Trait("Collection", "MqttEndTest")]
public class TimeSeriesGraphMqttTests(MqttTestEnvironment factory) : MqttFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ActionIsNotSupported()
    {
        // Arrange
        var user = await CreateUser();
        var (broker, _, device) = await Arrange();
        var operation = await PostIntegerRangedOperation(device);
        var widget = await PostTimeSeriesGraphWidget(operation);

        var clients = Services.GetRequiredService<IClientService>();
        await StartBrokerClient(broker);
        var connection = clients.GetClient(broker);

        var action = RequestFactory.CreateActionRequest(value: JsonValue.Create(42)).Serialize();

        // Act
        var response = await user.PostAsync($"{Paths.WidgetsPath}/{widget}/action", action);

        // Assert
        Assert.True(connection.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Cleanup
        await StopBrokerClientInternal(broker);
    }

    private async Task<(Guid, Guid, Guid)> Arrange()
    {
        var broker = await PostMqttBroker();
        var home = await PostAndPutBrokerForHome(broker);
        var device = await PostDevice(home);

        return (broker, home, device);
    }
}
