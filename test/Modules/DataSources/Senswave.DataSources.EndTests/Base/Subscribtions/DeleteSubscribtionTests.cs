using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.DataSources;

namespace Senswave.DataSources.EndTests.Base.Subscribtions;

[Trait("Collection", "EndTest")]
public class DeleteSubscribtionTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var subscriptionId = await PrepareSubscribtion(broker);

        // Act
        var response = await client.DeleteAsync(Paths.SubscribtionPath(broker, subscriptionId));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanDeleteSubscribtion()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var subscriptionId = await PrepareSubscribtion(broker);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.DeleteAsync(Paths.SubscribtionPath(broker, subscriptionId));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task NotOwnerCannotDeleteSubscribtion()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var subscriptionId = await PrepareSubscribtion(broker);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.DeleteAsync(Paths.SubscribtionPath(broker, subscriptionId));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletedSubscribtionIsRemovedFromList()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var topic = $"test/{Guid.NewGuid()}";
        var subscriptionId = await PrepareSubscribtion(broker, topic);

        await AuthorizeClientAsUser(client);

        // Act
        var deleteResponse = await client.DeleteAsync(Paths.SubscribtionPath(broker, subscriptionId));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await client.GetAsync(Paths.SubscribtionsPath(broker));
        var listContent = await listResponse.Content.ReadAsStringAsync();
        var subscriptions = JsonSerializer.Deserialize<SubscribtionsDto>(listContent) ?? new();

        Assert.DoesNotContain(subscriptions.Items, x => x.Id == subscriptionId);
    }

    [Fact]
    public async Task DeleteOneSubscribtionLeavesOthersIntact()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var topicToDelete = $"test/{Guid.NewGuid()}";
        var topicToKeep = $"test/{Guid.NewGuid()}";
        var subscriptionToDelete = await PrepareSubscribtion(broker, topicToDelete);
        var subscriptionToKeep = await PrepareSubscribtion(broker, topicToKeep);

        await AuthorizeClientAsUser(client);

        // Act
        var deleteResponse = await client.DeleteAsync(Paths.SubscribtionPath(broker, subscriptionToDelete));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await client.GetAsync(Paths.SubscribtionsPath(broker));
        var listContent = await listResponse.Content.ReadAsStringAsync();
        var subscriptions = JsonSerializer.Deserialize<SubscribtionsDto>(listContent) ?? new();

        Assert.DoesNotContain(subscriptions.Items, x => x.Id == subscriptionToDelete);
        Assert.Contains(subscriptions.Items, x => x.Topic == topicToKeep);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task DeleteNonExistentSubscribtionReturnsNotFound()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var nonExistentSubscriptionId = Guid.NewGuid();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.DeleteAsync(Paths.SubscribtionPath(broker, nonExistentSubscriptionId));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task DeleteSubscribtionFromNonExistentBrokerReturnsNotFound()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var nonExistentBrokerId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.DeleteAsync(Paths.SubscribtionPath(nonExistentBrokerId, subscriptionId));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
