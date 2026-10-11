using Senswave.DataSources.Domain.Brokers.Sessions.Enums;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.DataSources;

namespace Senswave.DataSources.EndTests.Base.Sessions;

[Trait("Collection", "EndTest")]
public class GetSessionTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Fact]
    public async Task OwnerCanFetchSession()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var session = await PrepareSessionWithLogs(broker);

        // Act
        await AuthorizeClientAsUser(client);

        var response = await client.GetAsync($"{Paths.SessionsPath(broker)}/{session}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        var sessions = JsonSerializer.Deserialize<SessionDto>(responseContent) ?? new();
        Assert.True(sessions.Logs.Count() > 1);
        Assert.Contains(sessions.Logs, x => x.EventType == SessionEventType.Connected.ToString());
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task NotOwnerCannotFetchSession()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var session = await PrepareSessionWithLogs(broker);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.GetAsync($"{Paths.SessionsPath(broker)}/{session}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();

        // Act
        var response = await client.GetAsync($"{Paths.SessionsPath(broker)}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
