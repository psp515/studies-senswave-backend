using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Net;

namespace Senswave.Homes.EndTests.Base.Rooms;

[Trait("Collection", "EndTest")]
public class PatchRoomTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePatchRoom()
            .Serialize();

        // Act
        var homeId = await PostHome();
        var roomId = await PostRoom(homeId);
        var response = await client.PatchAsync($"{Paths.HomesPath}/{homeId}/rooms/{roomId}", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task IsValidationWorking()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePatchRoom(name: "Te").Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var homeId = await PostHome();
        var roomId = await PostRoom(homeId);
        var response = await client.PatchAsync($"{Paths.HomesPath}/{homeId}/rooms/{roomId}", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedUserUpdatesRoom()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePatchRoom(name: "TestRoom123").Serialize();

        // Act
        var homeId = await PostHome();
        var roomId = await PostRoom(homeId);

        await AuthorizeClientAsUser(client);

        var response = await client.PatchAsync($"{Paths.HomesPath}/{homeId}/rooms/{roomId}", request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CannotPatchToNotHisRoom()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePatchRoom(name: "Test Room").Serialize();

        // Act
        await AuthorizeClientAsAdmin(client);
        var homeId = await PostHome();
        var roomId = await PostRoom(homeId);
        var response = await client.PatchAsync($"{Paths.HomesPath}/{homeId}/rooms/{roomId}", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserWithManagePrivilegeCanPatchRoom()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(client);
        var homeId = await PostHome();
        await PrepareHomeSharingForAdmin(homeId, _managePrivilege);
        var roomId = await PostRoom(homeId);

        var request = RequestFactory.CreatePatchRoom(name: "TestRoom123").Serialize();

        // Act
        var response = await client.PatchAsync($"{Paths.HomesPath}/{homeId}/rooms/{roomId}", request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UserWithGetPrivilegeCanNotPatchRoom()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(client);
        var homeId = await PostHome();
        await PrepareHomeSharingForAdmin(homeId, _displayPrivilege);
        var roomId = await PostRoom(homeId);

        var request = RequestFactory.CreatePatchRoom(name: "TestRoom123").Serialize();

        // Act
        var response = await client.PatchAsync($"{Paths.HomesPath}/{homeId}/rooms/{roomId}", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
