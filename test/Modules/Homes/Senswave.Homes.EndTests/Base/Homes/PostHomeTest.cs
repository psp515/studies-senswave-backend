using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Net;

namespace Senswave.Homes.EndTests.Base.Homes;

[Trait("Collection", "EndTest")]
public class PostHomeTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        var request = RequestFactory.CreatePostHome()
            .Serialize();

        // Act
        var response = await client.PostAsync(Paths.HomesPath, request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task IsValidationWorking()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        var request = RequestFactory.CreatePostHome(
            name: Guid.NewGuid().ToString(),
            icon: "ikonka",
            latitude: 220,
            longitude: 190).Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.HomesPath, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task IsValidationWorkingWithoutLocalization()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        var request = RequestFactory.CreatePostHome(
            name: "",
            icon: "default-icon").Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.HomesPath, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IgnoringPartialLocation()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePostHome(
            name: Guid.NewGuid().ToString(),
            icon: "default-icon",
            latitude: 76).Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.HomesPath, request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task UserCanCreateHome()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePostHome(
            name: $"Home {Guid.NewGuid()}",
            icon: "default-icon");

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.HomesPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task UserCanAddHomeWithLocalization()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePostHome(
            name: Guid.NewGuid().ToString(),
            icon: "default-icon",
            latitude: 81,
            longitude: 78).Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.HomesPath, request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task HomeNameAlreadyUsed()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePostHome(
            name: Guid.NewGuid().ToString(),
            icon: "ikonka",
            latitude: 76,
            longitude: 51).Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var createdResponse = await client.PostAsync(Paths.HomesPath, request);
        var response = await client.PostAsync(Paths.HomesPath, request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

}