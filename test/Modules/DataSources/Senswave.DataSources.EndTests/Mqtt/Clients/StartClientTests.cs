using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Senswave.DataSources.BrokerConnection.Interfaces;
using Senswave.TestInfrastructure.TestEnvironments.Mqtt;
using Senswave.TestInfrastructure.TestSetup.Models;

namespace Senswave.DataSources.EndTests.Mqtt.Clients;

[Collection("Mqtt E2E Tests Collection")]
[Trait("Collection", "MqttEndTest")]
public class StartClientTests(MqttTestEnvironment factory) : MqttFeatureTest(factory)
{
    [Fact]
    public async Task ClientIsStarting()
    {
        // Arrange
        var client = CreateClient();
        var request = RequestFactory.CreateStartClient(
            username: Factory.GetMqttProvider().Username,
            password: Factory.GetMqttProvider().Password).Serialize();

        using var scope = Factory.Server.Services.CreateScope();
        var clientService = Services.GetRequiredService<IClientService>();
        var testHarness = scope.ServiceProvider.GetRequiredService<ITestHarness>()!;

        // Act
        await testHarness.Start();

        var brokerId = await PostMqttBroker();
        await AuthorizeClientAsUser(client);

        var response = await client.PostAsync(Paths.ClientsPath(brokerId), request);

        var getClient = clientService.GetClient(brokerId);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(getClient.IsSuccess);
        Assert.True(await testHarness.Published.Any<Integration.DataSource.BrokerConnection.Start.StartClientRequest>());

        await StopBrokerClientInternal(brokerId);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ClientFailsToStartAndIsNotAddedToList()
    {
        // Arrange
        var client = CreateClient();
        var request = RequestFactory.CreateStartClient(
            username: Factory.GetMqttProvider().Username,
            password: Factory.GetMqttProvider().Password + Factory.GetMqttProvider().Password).Serialize();

        using var scope = Factory.Server.Services.CreateScope();
        var clientService = Services.GetRequiredService<IClientService>();
        var testHarness = scope.ServiceProvider.GetRequiredService<ITestHarness>()!;

        // Act
        await testHarness.Start();

        var brokerId = await PostMqttBroker();
        await AuthorizeClientAsUser(client);

        var response = await client.PostAsync(Paths.ClientsPath(brokerId), request);

        var getClient = clientService.GetClient(brokerId);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(getClient.IsFailure);
        Assert.True(await testHarness.Published.Any<Integration.DataSource.BrokerConnection.Start.StartClientRequest>());

        await StopBrokerClientInternal(brokerId);
    }
}
