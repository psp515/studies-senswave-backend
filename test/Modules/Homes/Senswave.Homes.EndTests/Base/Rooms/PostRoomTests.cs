using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Net;

namespace Senswave.Homes.EndTests.Base.Rooms;

[Trait("Collection", "EndTest")]
public class PostRoomTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePostRoom()
            .Serialize();

        // Act
        var homeId = await PostHome();
        var response = await client.PostAsync($"{Paths.HomesPath}/{homeId}/rooms", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task IsValidationWorking()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePostRoom(name: "Te").Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var homeId = await PostHome();
        var response = await client.PostAsync($"{Paths.HomesPath}/{homeId}/rooms", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserCanAddRoom()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePostRoom(name: "Testowy pokój").Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var homeId = await PostHome();
        var response = await client.PostAsync($"{Paths.HomesPath}/{homeId}/rooms", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task HomeNameAlreadyUsed()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePostRoom(name: "Testowy pokój").Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var homeId = await PostHome();
        var response = await client.PostAsync($"{Paths.HomesPath}/{homeId}/rooms", request);
        var response2 = await client.PostAsync($"{Paths.HomesPath}/{homeId}/rooms", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task InvalidHomeForUser()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        var request = RequestFactory.CreatePostRoom(name: "Testowy pokój").Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync($"{Paths.HomesPath}/{Guid.NewGuid()}/rooms", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserWithManagePrivilegeCanPostRoom()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(client);
        var homeId = await PostHome();
        await PrepareHomeSharingForAdmin(homeId, _managePrivilege);

        var request = RequestFactory.CreatePostRoom(name: "Testowy pokój").Serialize();

        // Act
        var response = await client.PostAsync($"{Paths.HomesPath}/{homeId}/rooms", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UserWithGetPrivilegeCanNotPostRoom()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(client);
        var homeId = await PostHome();
        await PrepareHomeSharingForAdmin(homeId, _displayPrivilege);

        var request = RequestFactory.CreatePostRoom(name: "Testowy pokój").Serialize();

        // Act
        var response = await client.PostAsync($"{Paths.HomesPath}/{homeId}/rooms", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
