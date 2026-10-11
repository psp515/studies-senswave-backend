using Microsoft.Extensions.DependencyInjection;
using Senswave.DataSources.BrokerConnection.Interfaces;
using Senswave.TestInfrastructure.TestEnvironments.Mqtt;
using Senswave.TestInfrastructure.TestSetup.Models;

namespace Senswave.DataSources.EndTests.Mqtt.Brokers;

[Collection("Mqtt E2E Tests Collection")]
[Trait("Collection", "MqttEndTest")]
public class PatchBroker(MqttTestEnvironment factory) : MqttFeatureTest(factory)
{

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task PatchBrokerCannotBeInvokedIfWorking()
    {
        // Arrange
        var client = CreateClient();
        var request = RequestFactory.CreatePatchBrokerRequest(
            name: "name",
            clientName: "clientName",
            port: 123,
            protocolVersion: "MqttV5",
            url: "url.com").Serialize();
        var connectionService = Services.GetRequiredService<IClientService>();

        // Act
        var id = await PostMqttBroker();
        await StartBrokerClient(id);

        await AuthorizeClientAsUser(client);

        var firstConnectionResult = connectionService!.GetClient(id);
        var response = await client.PatchAsync($"{Paths.BrokersPath}/{id}", request);
        var secondConnectionResult = connectionService!.GetClient(id);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(firstConnectionResult.IsSuccess);
        Assert.True(secondConnectionResult.IsSuccess);

        await StopBrokerClientInternal(id);
    }

    [Fact]
    public async Task UserCanPatchHisBrokerWithTest()
    {
        // Arrange
        var client = CreateClient();
        var request = RequestFactory.CreatePatchBrokerRequest(
            clientName: Guid.NewGuid().ToString(),
            password: Factory.GetMqttProvider().Password,
            username: Factory.GetMqttProvider().Username).Serialize();

        // Act
        var id = await PostMqttBroker();
        await AuthorizeClientAsUser(client);
        var response = await client.PatchAsync($"{Paths.BrokersPath}/{id}", request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UserFailsToPatchBrokerIfTestNotPasses()
    {
        // Arrange
        var client = CreateClient();

        var request = RequestFactory.CreatePatchBrokerRequest(
            clientName: Guid.NewGuid().ToString(),
            password: Factory.GetMqttProvider().Password + Factory.GetMqttProvider().Password,
            username: Factory.GetMqttProvider().Username).Serialize();

        // Act
        var id = await PostMqttBroker();
        await AuthorizeClientAsUser(client);
        var response = await client.PatchAsync($"{Paths.BrokersPath}/{id}", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


}
