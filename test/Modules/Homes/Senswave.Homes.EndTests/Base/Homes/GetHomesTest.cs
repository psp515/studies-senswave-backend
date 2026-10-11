using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.Homes;
using System.Net;
using System.Text.Json;

namespace Senswave.Homes.EndTests.Base.Homes;

[Trait("Collection", "EndTest")]
public class GetHomesTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndPoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var response = await client.GetAsync($"{Paths.HomesPath}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetHomes()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        await PostHome();
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.HomesPath}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<HomeListDto>(responseContent) ?? new();

        Assert.NotNull(responseContent);
        Assert.True(dto.Homes.Length > 0);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task FailedToGetNextPage()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.HomesPath}?size=3000&page=2");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(HomePriviliges))]
    public async Task FriendCanGetHome(string privilege)
    {
        // Arrange
        var home = await PostHome();
        await PrepareHomeSharingForAdmin(home, privilege);
        var friend = CreateUnauthorizedClient();

        // Act
        await AuthorizeClientAsAdmin(friend);
        var response = await friend.GetAsync($"{Paths.HomesPath}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<HomeListDto>(responseContent) ?? new();

        var ids = dto.Homes.Select(x => x.Id);
        Assert.NotNull(responseContent);
        Assert.True(dto.Homes.Length > 0);
        Assert.Contains(home, ids);
    }
}