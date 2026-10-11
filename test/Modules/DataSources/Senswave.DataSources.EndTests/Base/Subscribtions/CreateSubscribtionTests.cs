using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using Senswave.TestInfrastructure.TestSetup.Models.Common;

namespace Senswave.DataSources.EndTests.Base.Subscribtions;

[Trait("Collection", "EndTest")]
public class CreateSubscribtionTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var request = RequestFactory.CreatePostSubscribtionRequest($"test/{Guid.NewGuid()}").Serialize();

        // Act
        var response = await client.PostAsync(Paths.SubscribtionsPath(broker), request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanCreateSubscribtion()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var request = RequestFactory.CreatePostSubscribtionRequest($"test/{Guid.NewGuid()}").Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.SubscribtionsPath(broker), request);
        var responseContent = await response.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<IdResponse>(responseContent);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.Id);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task NotOwnerCannotCreateSubscribtion()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var request = RequestFactory.CreatePostSubscribtionRequest($"test/{Guid.NewGuid()}").Serialize();

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.PostAsync(Paths.SubscribtionsPath(broker), request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task TopicAlreadyExistsFails()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var topic = $"test/{Guid.NewGuid()}";
        var request = RequestFactory.CreatePostSubscribtionRequest(topic).Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        await client.PostAsync(Paths.SubscribtionsPath(broker), request);
        var response = await client.PostAsync(Paths.SubscribtionsPath(broker), request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ValidationFailsForInvalidTopic()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var request = RequestFactory.CreatePostSubscribtionRequest("#invalid/start").Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.SubscribtionsPath(broker), request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ReturnsNotFoundForNonExistentBroker()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var nonExistentBrokerId = Guid.NewGuid();
        var request = RequestFactory.CreatePostSubscribtionRequest($"test/{Guid.NewGuid()}").Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.SubscribtionsPath(nonExistentBrokerId), request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
