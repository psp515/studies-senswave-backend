using Senswave.TestInfrastructure.TestEnvironments.Base;
using System.Net;

namespace Senswave.Homes.EndTests.Base.Homes;

[Trait("Collection", "EndTest")]
public class DeleteHomeTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var id = await PostHome();
        var path = $"{Paths.HomesPath}/{id}";
        var response = await client.DeleteAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CanNotDeleteNotExistingHome()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var randomGuid = Guid.NewGuid();

        // Act
        await AuthorizeClientAsUser(client);
        var path = $"{Paths.HomesPath}/{randomGuid}";
        var response = await client.DeleteAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserCanDeleteExistingHome()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var id = await PostHome();
        await AuthorizeClientAsUser(client);
        var response = await client.DeleteAsync($"{Paths.HomesPath}/{id}");
        var getResponse = await client.GetAsync($"{Paths.HomesPath}/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, getResponse.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UserCanNotDeleteAnotherUserHome()
    {
        // Arrange
        var admin = CreateUnauthorizedClient();
        var client = CreateUnauthorizedClient();

        // Act
        var id = await PostHome();
        await AuthorizeClientAsAdmin(admin);
        await AuthorizeClientAsUser(client);
        var adminResponse = await admin.DeleteAsync($"{Paths.HomesPath}/{id}");
        var clientResponse = await client.DeleteAsync($"{Paths.HomesPath}/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, clientResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, adminResponse.StatusCode);

    }
}