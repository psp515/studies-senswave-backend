using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.DataSources.EndTests.Base.Brokers;

[Trait("Collection", "EndTest")]
public class GetBrokerTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var id = await PostBroker();
        var response = await client.GetAsync($"{Paths.BrokersPath}/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserCanGetHisBroker()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        await AuthorizeClientAsUser(client);
        var id = await PostBroker();
        var response = await client.GetAsync($"{Paths.BrokersPath}/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UserCanNotGetNotHisBroker()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var stealer = CreateUnauthorizedClient();


        // Act
        var id = await PostBroker();
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.BrokersPath}/{id}");
        await AuthorizeClientAsAdmin(stealer);
        var stealerResponse = await stealer.GetAsync($"{Paths.BrokersPath}/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, stealerResponse.StatusCode);
    }
}

