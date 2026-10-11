using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.Homes;
using System.Net;
using System.Text.Json;

namespace Senswave.Homes.EndTests.Base.Sharings;

[Trait("Collection", "EndTest")]
public class HomeSharingsTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        var home = await PostHome();
        await PrepareHomeSharingForAdmin(home);

        // Act
        var response = await user.GetAsync($"{Paths.HomesPath}/sharings?homeId={home}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserCanGetSharingForHome()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHome();
        await PrepareHomeSharingForAdmin(home);

        // Act
        var response = await user.GetAsync($"{Paths.HomesPath}/sharings?homeId={home}");
        var content = await response.Content.ReadAsStringAsync();
        var jsonResponse = JsonSerializer.Deserialize<HomeSharingResponse>(content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(jsonResponse);
        Assert.Single(jsonResponse.Items);
    }
}