using Senswave.TestInfrastructure.TestEnvironments.Base;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Senswave.Homes.EndTests.Base.Rooms;

[Trait("Collection", "EndTest")]
public class GetRoomTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndPoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHome();
        var room = await PostRoom(home);

        // Act
        var response = await client.GetAsync(Paths.RoomPath(home, room));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanGetRoom()
    {
        // Arrange
        var client = await CreateUser();
        var home = await PostHome();
        var room = await PostRoom(home);

        // Act
        var response = await client.GetAsync(Paths.RoomPath(home, room));
        var responseContent = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<JsonObject>(responseContent)!;

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseContent);
        Assert.Equal(room.ToString(), dto["id"]!.ToString());
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task IntruderCannotGetRoom()
    {
        // Arrange
        var client = await CreateIntruder();
        var home = await PostHome();
        var room = await PostRoom(home);

        // Act
        var response = await client.GetAsync(Paths.RoomPath(home, room));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(HomePriviliges))]
    public async Task RoomsCanBeGetWithProperPriviliges(string privilege)
    {
        // Arrange
        var friend = await CreateAdmin();
        var home = await PostHome();
        var room = await PostRoom(home);

        await PrepareHomeSharingForAdmin(home, privilege);

        // Act
        var response = await friend.GetAsync(Paths.RoomPath(home, room));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
