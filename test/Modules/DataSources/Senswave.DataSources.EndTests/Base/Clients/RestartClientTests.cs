using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Senswave.DataSources.BrokerConnection.Interfaces;
using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.DataSources.EndTests.Base.Clients;

[Trait("Collection", "EndTest")]
public class RestartClientTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var brokerId = await PostBroker();
        var response = await client.PatchAsync($"{Paths.ClientsPath(brokerId)}/restart", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task FailsToRestartNotWorkingClient()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var brokerId = await PostBroker();
        await AuthorizeClientAsUser(client);

        var stopResponse = await client.PatchAsync($"{Paths.ClientsPath(brokerId)}/restart", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, stopResponse.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task NotOwnerCanRestartConnection()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var clientService = Services.GetRequiredService<IClientService>();
        var testHarness = Services.GetRequiredService<ITestHarness>()!;

        // Act
        var brokerId = await PostBroker();
        await AuthorizeClientAsAdmin(client);

        await testHarness.Start();
        var restartResponse = await client.PatchAsync($"{Paths.ClientsPath(brokerId)}/restart", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, restartResponse.StatusCode);
    }
}
