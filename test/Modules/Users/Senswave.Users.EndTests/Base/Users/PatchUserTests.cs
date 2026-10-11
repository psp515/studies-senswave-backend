using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.Users.EndTests.Base.Users;

[Trait("Collection", "EndTest")]
public class PatchUserTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = new
        {
            Language = "pl",
            Theme = "dark"
        }.Serialize();

        // Act
        var response = await client.PatchAsync($"{Paths.UsersPath}/settings", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserCanUpdateHisProfile()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = new
        {
            Language = "pl",
            Theme = "dark"
        }.Serialize();


        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PatchAsync($"{Paths.UsersPath}/settings", request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task CanUpdateJustTheme()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = new
        {
            Theme = "dark"
        }.Serialize();


        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PatchAsync($"{Paths.UsersPath}/settings", request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task CanUpdateToHighContrast()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = new
        {
            Theme = "HighContrast"
        }.Serialize();


        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PatchAsync($"{Paths.UsersPath}/settings", request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task FailsWhenOneArgumentIsInvalid()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = new
        {
            Theme = "dark",
            Language = "invalid"
        }.Serialize();


        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PatchAsync($"{Paths.UsersPath}/settings", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ValidationIsWorking()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = new
        {
            Language = "234sd",
            Theme = "LDSFGS"
        }.Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PatchAsync($"{Paths.UsersPath}/settings", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
