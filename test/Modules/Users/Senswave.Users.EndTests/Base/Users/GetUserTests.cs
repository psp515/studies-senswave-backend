using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.Users.EndTests.Base.Users;

[Trait("Collection", "EndTest")]
public class GetUserTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var response = await client.GetAsync(Paths.UsersPath);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserCanRetriveBasicInformation()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync(Paths.UsersPath);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
