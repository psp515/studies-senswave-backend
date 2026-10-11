using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.Homes;
using System.Net;
using System.Text.Json;

namespace Senswave.Homes.EndTests.Base.Rooms;

[Trait("Collection", "EndTest")]
public class GetRoomsTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndPoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHome();
        await PostRoom(home);

        // Act
        var response = await client.GetAsync($"{Paths.RoomsPath(home)}/display");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanGetRoomsInHome()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHome();
        await PostRoom(home);
        await PostRoom(home);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.RoomsPath(home)}/display");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<RoomListDto>(responseContent) ?? new();

        Assert.NotNull(responseContent);
        Assert.Equal(2, dto.Rooms.Length);
    }

    [Theory]
    [MemberData(nameof(HomePriviliges))]
    public async Task RoomsCanBeGetWithProperPriviliges(string privilege)
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        var home = await PostHome();
        var room = await PostRoom(home);

        await PrepareHomeSharingForAdmin(home, privilege);

        // Act
        await AuthorizeClientAsAdmin(friend);
        var response = await friend.GetAsync($"{Paths.RoomsPath(home)}/display");
        var responseContent = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<RoomListDto>(responseContent) ?? new();
        var id = dto.Rooms.
            Select(x => x.Id)
            .FirstOrDefault();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseContent);
        Assert.Single(dto.Rooms);
        Assert.Equal(room, id);
    }
}
