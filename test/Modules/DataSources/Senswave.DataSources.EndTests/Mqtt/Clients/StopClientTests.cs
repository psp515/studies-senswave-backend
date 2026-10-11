using Microsoft.Extensions.DependencyInjection;
using Senswave.DataSources.BrokerConnection.Interfaces;
using Senswave.TestInfrastructure.TestEnvironments.Mqtt;

namespace Senswave.DataSources.EndTests.Mqtt.Clients;

[Collection("Mqtt E2E Tests Collection")]
[Trait("Collection", "MqttEndTest")]
public class StopClientTests(MqttTestEnvironment factory) : MqttFeatureTest(factory)
{
    [Fact]
    public async Task OwnerCanStopConnection()
    {
        // Arrange
        var client = CreateClient();
        var clientService = Services.GetRequiredService<IClientService>();

        // Act
        var brokerId = await PostMqttBroker();
        await AuthorizeClientAsUser(client);
        await StartBrokerClient(brokerId);

        var stopResponse = await client.DeleteAsync(Paths.ClientsPath(brokerId));
        var response = await stopResponse.Content.ReadAsStringAsync();
        var currentClient = clientService.GetClient(brokerId);

        // Assert
        Assert.True(HttpStatusCode.NoContent == stopResponse.StatusCode, $"Is client working: {currentClient.IsFailure} {response}");
        Assert.True(currentClient.IsFailure);

        // Cleanup
        await StopBrokerClientInternal(brokerId);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task NotOwnerCannotStopClient()
    {
        // Arrange
        var client = CreateClient();
        var clientService = Services.GetRequiredService<IClientService>();

        // Act
        var brokerId = await PostMqttBroker();
        await AuthorizeClientAsAdmin(client);
        await StartBrokerClient(brokerId);

        var stopResponse = await client.DeleteAsync(Paths.ClientsPath(brokerId));
        var currentClient = clientService.GetClient(brokerId);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, stopResponse.StatusCode);
        Assert.True(currentClient.IsSuccess);

        // Cleanup
        await StopBrokerClientInternal(brokerId);
    }
}
