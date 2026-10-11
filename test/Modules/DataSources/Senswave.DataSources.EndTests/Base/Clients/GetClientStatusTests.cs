using Senswave.DataSources.Domain.Brokers.Clients.Enums;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.DataSources;

namespace Senswave.DataSources.EndTests.Base.Clients;

[Trait("Collection", "EndTest")]
public class GetClientStatusTests(BaseTestEnvironment apiFactory) : BaseFeatureTest(apiFactory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var brokerId = await PostBroker();
        var response = await client.GetAsync(Paths.ClientsPath(brokerId));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldReturnNotFoundForRandom()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync(Paths.ClientsPath(Guid.NewGuid()));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CanGetStatus()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var brokerId = await PostBroker();
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync(Paths.ClientsPath(brokerId));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var status = JsonSerializer.Deserialize<GetClientResponse>(content);
        Assert.NotNull(status);
        Assert.NotNull(status!.ConnectionStatus);
        Assert.Equal(ClientState.NotStarted.ToString(), status!.ConnectionStatus);
    }

    [Fact]
    public async Task CanGetStatusWithValidSession()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var brokerId = await PostBroker();
        await PrepareSessionWithLogs(brokerId);
        var session = await PrepareSessionWithLogs(brokerId);
        await AuthorizeClientAsUser(client);

        var response = await client.GetAsync(Paths.ClientsPath(brokerId));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var status = JsonSerializer.Deserialize<GetClientResponse>(content);
        Assert.NotNull(status);
        Assert.NotNull(status!.ConnectionStatus);
        Assert.Equal(ClientState.NotStarted.ToString(), status!.ConnectionStatus);
        Assert.Equal(session, status.LatestSessionId);
    }
}
