using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.DataSources;

namespace Senswave.DataSources.EndTests.Base.Brokers;

[Trait("Collection", "EndTest")]
public class GetBrokers(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var response = await client.GetAsync(Paths.BrokersPath);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBrokerList()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await PostBroker();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync(Paths.BrokersPath);
        var responseContent = await response.Content.ReadAsStringAsync();
        var brokerConnections = JsonSerializer.Deserialize<BrokerListDto>(responseContent) ?? new();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseContent);
        Assert.True(brokerConnections.Items.Any());
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task FailsToGetSecondPage()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.BrokersPath}?size=3000&page=2");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
