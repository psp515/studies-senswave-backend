using Senswave.TestInfrastructure.TestEnvironments.Base;
using System.Net;

namespace Senswave.Homes.EndTests.Base.Rooms;

[Trait("Collection", "EndTest")]
public class DeleteRoomTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndPoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var response = await client.DeleteAsync($"{Paths.RoomsPath(Guid.NewGuid())}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanDeleteRoom()
    {
        // Arrange
        var client = await AuthorizedUser();

        var home = await PostHome();
        var room = await PostRoom(home);

        // Act
        var response = await client.DeleteAsync($"{Paths.RoomsPath(home)}/{room}");
        var roomsResponse = await client.GetAsync($"{Paths.RoomsPath(home)}/display");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, roomsResponse.StatusCode);

    }

    [Fact]
    public async Task FirendWithManageRoleCanDeleteRoom()
    {
        // Arrange
        var client = await AuthorizedUser();

        var home = await PostHome();
        var room = await PostRoom(home);
        await PrepareHomeSharingForAdmin(home, "Manage");

        var friend = await AuthorizedAdmin();

        // Act
        var response = await friend.DeleteAsync($"{Paths.RoomsPath(home)}/{room}");
        var roomsResponse = await client.GetAsync($"{Paths.RoomsPath(home)}/display");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, roomsResponse.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task FriendWithDisplayRoleCanNotDeleteRoom()
    {
        // Arrange
        var client = await AuthorizedUser();

        var home = await PostHome();
        var room = await PostRoom(home);
        await PrepareHomeSharingForAdmin(home, "Display");

        var friend = await AuthorizedAdmin();

        // Act
        var response = await friend.DeleteAsync($"{Paths.RoomsPath(home)}/{room}");
        var roomsResponse = await client.GetAsync($"{Paths.RoomsPath(home)}/display");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, roomsResponse.StatusCode);
    }
}
