using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.DataSources;

namespace Senswave.DataSources.EndTests.Base.Subscribtions;

[Trait("Collection", "EndTest")]
public class GetSubscribtionsTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();

        // Act
        var response = await client.GetAsync(Paths.SubscribtionsPath(broker));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanFetchSubscribtions()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        await PrepareSubscribtion(broker);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync(Paths.SubscribtionsPath(broker));
        var responseContent = await response.Content.ReadAsStringAsync();
        var subscribtions = JsonSerializer.Deserialize<SubscribtionsDto>(responseContent) ?? new();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(subscribtions.Items);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task NotOwnerCannotFetchSubscribtions()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        await PrepareSubscribtion(broker);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.GetAsync(Paths.SubscribtionsPath(broker));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReturnsCorrectSubscribtionData()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var topic = $"sensor/{Guid.NewGuid()}/data";
        await PrepareSubscribtion(broker, topic);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync(Paths.SubscribtionsPath(broker));
        var responseContent = await response.Content.ReadAsStringAsync();
        var subscribtions = JsonSerializer.Deserialize<SubscribtionsDto>(responseContent) ?? new();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.Single(subscribtions.Items);
        Assert.Equal(topic, item.Topic);
        Assert.NotEqual(Guid.Empty, item.Id);
    }

    [Fact]
    public async Task ReturnsMultipleSubscribtions()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();

        for (var i = 0; i < 3; i++)
            await PrepareSubscribtion(broker);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync(Paths.SubscribtionsPath(broker));
        var responseContent = await response.Content.ReadAsStringAsync();
        var subscribtions = JsonSerializer.Deserialize<SubscribtionsDto>(responseContent) ?? new();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, subscribtions.Items.Count());
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task PaginationFailsToGetEmptyPage()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        await PrepareSubscribtion(broker);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.SubscribtionsPath(broker)}?page=2");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ValidationFailsForInvalidPage()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.SubscribtionsPath(broker)}?page=-1");

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

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync(Paths.SubscribtionsPath(nonExistentBrokerId));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PaginationReturnsCorrectPage()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();

        for (var i = 0; i < 5; i++)
            await PrepareSubscribtion(broker);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.SubscribtionsPath(broker)}?page=1&size=3");
        var responseContent = await response.Content.ReadAsStringAsync();
        var subscribtions = JsonSerializer.Deserialize<SubscribtionsDto>(responseContent) ?? new();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, subscribtions.Items.Count());
    }
}
