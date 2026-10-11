using Senswave.TestInfrastructure.TestEnvironments.Limit;

namespace Senswave.DataSources.EndTests.Limits.Brokers;

[Collection("Limits E2E Tests Collection")]
[Trait("Collection", "LimitsEndTests")]
public class PostBrokerTests(LimitTestEnvironment factory) : LimitFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task LimitationWorksForUser()
    {
        // Arrange
        var client = CreateClient();
        var limit = LimitTestEnvironment.BrokerOptions.Limits.Brokers;

        // Act
        await CleanBrokers();
        await AuthorizeClientAsUser(client);

        for (int i = 0; i < limit; i++)
        {
            await PostBroker();
        }

        var response = await PostBrokerNoChecks();

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task LimitationWorksForInstance()
    {
        // Arrange
        var user = CreateClient();
        var admin = CreateClient();
        var limit = LimitTestEnvironment.BrokerOptions.Limits.Brokers;

        // Act
        await CleanBrokers();

        await AuthorizeClientAsUser(user);
        for (int i = 0; i < limit; i++)
            await PostBroker();

        await AuthorizeClientAsAdmin(admin);
        for (int i = 0; i < limit; i++)
            await PostBrokerNoChecksPreLogged(admin);

        var result = await PostBrokerNoChecksPreLogged(admin);
        var content = await result.Content.ReadAsStringAsync();

        // Assert
        Assert.True(LimitTestEnvironment.BrokerOptions.Limits.Brokers * 2 > LimitTestEnvironment.BrokerOptions.Limits.InstanceMaxBrokerClients);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Contains("MaximalNumberOfGlobalBrokers", content);
    }
}
