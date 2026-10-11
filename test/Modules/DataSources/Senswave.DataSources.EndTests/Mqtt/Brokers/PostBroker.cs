using Senswave.TestInfrastructure.TestEnvironments.Mqtt;
using Senswave.TestInfrastructure.TestSetup.Models;

namespace Senswave.DataSources.EndTests.Mqtt.Brokers;

[Collection("Mqtt E2E Tests Collection")]
[Trait("Collection", "MqttEndTest")]
public class PostBroker(MqttTestEnvironment factory) : MqttFeatureTest(factory)
{
    [Fact]
    public async Task UserCanCreateBroker()
    {
        // Arrange
        var client = CreateClient();
        var request = RequestFactory.CreatePostBrokerRequest(
            name: Guid.NewGuid().ToString(),
            clientName: Guid.NewGuid().ToString(),
            url: Factory.GetMqttProvider().Hostname,
            port: Factory.GetMqttProvider().Port,
            protocolVersion: Factory.GetMqttProvider().Version,
            useTls: Factory.GetMqttProvider().UseTls,
            password: Factory.GetMqttProvider().Password,
            username: Factory.GetMqttProvider().Username).Serialize();

        // Act
        await DeleteBroker();
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.BrokersPath, request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task InvalidPasswordForBrokerIsNotCreatingBroker()
    {
        // Arrange
        var client = CreateClient();
        var request = RequestFactory.CreatePostBrokerRequest(
            name: Guid.NewGuid().ToString(),
            clientName: Guid.NewGuid().ToString(),
            url: Factory.GetMqttProvider().Hostname,
            port: Factory.GetMqttProvider().Port,
            protocolVersion: Factory.GetMqttProvider().Version,
            useTls: Factory.GetMqttProvider().UseTls,
            password: Factory.GetMqttProvider().Password + Factory.GetMqttProvider().Password,
            username: Factory.GetMqttProvider().Username).Serialize();

        // Act
        await DeleteBroker();
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.BrokersPath, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
