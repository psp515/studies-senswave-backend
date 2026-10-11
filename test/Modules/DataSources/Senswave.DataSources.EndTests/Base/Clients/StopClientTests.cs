using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.DataSources.EndTests.Base.Clients;

[Trait("Collection", "EndTest")]
public class StopClientTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var brokerId = await PostBroker();
        var response = await client.DeleteAsync(Paths.ClientsPath(brokerId));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task FailsToStopNotWorkingClient()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var brokerId = await PostBroker();
        await AuthorizeClientAsUser(client);

        var stopResponse = await client.DeleteAsync(Paths.ClientsPath(brokerId));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, stopResponse.StatusCode);
    }
}
