using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.DataSources.EndTests.Base.Brokers;

[Trait("Collection", "EndTest")]
public class DeleteBroker(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var guid = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"{Paths.BrokersPath}/{guid}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserCanDeleteBroker()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        await AuthorizeClientAsUser(client);
        var id = await PostBroker();

        var response = await client.DeleteAsync($"{Paths.BrokersPath}/{id}");
        var getResponse = await client.GetAsync($"{Paths.BrokersPath}/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}