using Senswave.TestInfrastructure.TestEnvironments.Limit;

namespace Senswave.DataSources.EndTests.Limits.Subscribtions;

[Collection("Limits E2E Tests Collection")]
[Trait("Collection", "LimitsEndTests")]
public class CreateSubscribtionTests(LimitTestEnvironment factory) : LimitFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task LimitationWorksPerDataSource()
    {
        // Arrange
        var client = CreateClient();
        await AuthorizeClientAsUser(client);

        await CleanBrokers();
        var brokerId = await PostBroker();
        var limit = LimitTestEnvironment.BrokerOptions.Limits.BrokerSubscribtions;

        // Act
        for (int i = 0; i < limit; i++)
        {
            await PostSubscribtion(brokerId);
        }

        var response = await PostSubscribtionNoChecks(brokerId);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("MaximalNumberOfSubscribtions", content);
    }

    [Fact]
    public async Task LimitIsEnforcedPerDataSourceNotGlobally()
    {
        // Arrange
        var client = CreateClient();
        await AuthorizeClientAsUser(client);

        await CleanBrokers();
        var brokerIdA = await PostBroker();
        var brokerIdB = await PostBroker();
        var limit = LimitTestEnvironment.BrokerOptions.Limits.BrokerSubscribtions;

        // Act — fill up brokerA to the limit
        for (int i = 0; i < limit; i++)
        {
            await PostSubscribtion(brokerIdA);
        }

        // brokerB should still accept subscriptions
        var response = await PostSubscribtionNoChecks(brokerIdB);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
