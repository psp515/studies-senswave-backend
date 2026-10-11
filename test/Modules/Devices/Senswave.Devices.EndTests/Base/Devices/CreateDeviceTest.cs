using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Devices.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using Senswave.TestInfrastructure.TestSetup.Models.Common;
using Senswave.Users.Infrastructure;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Devices;

[Trait("Collection", "EndTest")]
public class CreateDeviceTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        var client = CreateUnauthorizedClient();
        var homeId = await PostHome();
        var request = CreateExampleRequest(homeId, "Freezer300");

        // Act
        var response = await client.PostAsync(Paths.DevicesPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanCreateDevice()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        var request = CreateExampleRequest(home, "Freezer" + Guid.NewGuid().ToString()[..10]);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.DevicesPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task OwnerOfDeviceIsSetCorrectly()
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        await PrepareHomeSharingForAdmin(home);
        var request = CreateExampleRequest(home, "Freezer300")
            .Serialize();

        using var scope = Factory.Server.Services.CreateScope();
        var devicesContext = scope.ServiceProvider.GetRequiredService<DevicesContext>();
        var usersContext = scope.ServiceProvider.GetRequiredService<UsersContext>();

        // Act
        await AuthorizeClientAsAdmin(friend);
        var response = await friend.PostAsync(Paths.DevicesPath, request);
        var content = await response.Content.ReadAsStringAsync();
        var deviceIdResponse = JsonSerializer.Deserialize<IdResponse>(content)!;

        var device = await devicesContext.Devices
            .Include(x => x.HomeReference)
            .Where(x => x.Id == deviceIdResponse.Id)
            .FirstAsync();

        var admin = await usersContext.Users
            .Where(x => x.Email == "admin@gmail.com")
            .FirstAsync();

        var user = await usersContext.Users
            .Where(x => x.Email == "user@gmail.com")
            .FirstAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEqual(admin.Id, device.HomeReference.OwnerId);
        Assert.Equal(user.Id, device.HomeReference.OwnerId);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CannotCreateDeviceWithDuplicateName()
    {
        var client = CreateUnauthorizedClient();
        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        var request = CreateExampleRequest(home, "Freezer" + Guid.NewGuid().ToString()[..10]);

        // Act
        await AuthorizeClientAsUser(client);
        var successAdd = await client.PostAsync(Paths.DevicesPath, request.Serialize());
        var refuseToAdd = await client.PostAsync(Paths.DevicesPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, successAdd.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, refuseToAdd.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MaliciousCannotCreateDevice()
    {
        // Arrange
        var malicious = CreateUnauthorizedClient();
        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        var request = CreateExampleRequest(home, "Freezer300");

        // Act
        await AuthorizeClientAsAdmin(malicious);
        var response = await malicious.PostAsync(Paths.DevicesPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FriendCanCreateDevice()
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(friend);
        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        await PrepareHomeSharingForAdmin(home, _managePrivilege);
        var request = CreateExampleRequest(home, "Freezer" + Guid.NewGuid().ToString()[..10]);

        // Act
        var response = await friend.PostAsync(Paths.DevicesPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task FriendCanNotCreateDevice()
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(friend);
        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        await PrepareHomeSharingForAdmin(home, _displayPrivilege);
        var request = CreateExampleRequest(home, "Freezer" + Guid.NewGuid().ToString()[..10]);

        // Act
        var response = await friend.PostAsync(Paths.DevicesPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static JsonObject CreateExampleRequest(Guid homeId, string name) =>
        RequestFactory.CreatePostDevice(
            homeId: homeId,
            name: name,
            icon: "default-icon");
}