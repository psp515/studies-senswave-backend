using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.DataSources;

namespace Senswave.DataSources.EndTests.Base.Sessions;

[Trait("Collection", "EndTest")]
public class GetSessionsTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Fact]
    public async Task OwnerCanFetchSessions()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        await PrepareSessionWithLogs(broker);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.SessionsPath(broker)}");
        var responseContent = await response.Content.ReadAsStringAsync();
        var sessions = JsonSerializer.Deserialize<SessionsDto>(responseContent) ?? new();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(sessions.Items);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task PaginationFailsToGetEmptyPage()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        await PrepareSessionWithLogs(broker);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.SessionsPath(broker)}?page=2");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ValidatonWorks()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        await PrepareSessionWithLogs(broker);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.SessionsPath(broker)}?page=-1");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReturnsFromLatestToOldest()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();

        for (int i = 0; i < 8; i++)
        {
            var session = await PrepareSessionWithLogs(broker);
            await FinishSession(session);
            await Task.Delay(100);
        }

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.SessionsPath(broker)}?");
        var responseContent = await response.Content.ReadAsStringAsync();
        var sessions = JsonSerializer.Deserialize<SessionsDto>(responseContent) ?? new();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, sessions.Items.Count());

        Assert.True(sessions.Items.First().CreatedAtUtc > sessions.Items.Last().CreatedAtUtc);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task NotOwnerCannotFetchSessions()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.GetAsync($"{Paths.SessionsPath(broker)}");

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
