using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Dashbaords;

[Trait("Collection", "EndTest")]
public class UpdateDashboardTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        var request = ToRequest(Guid.NewGuid());

        // Act
        var response = await user.PatchAsync($"{Paths.DashboardsPath}/{Guid.NewGuid()}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanUpdateDashboard()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var dashboard = await PostDashboard(device);
        var request = ToRequest(device);

        // Act
        await AuthorizeClientAsUser(user);
        var response = await user.PatchAsync($"{Paths.DashboardsPath}/{dashboard}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MaliciousUserCanNotUpdateDashboard()
    {
        // Arrange
        var malicious = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var dashboard = await PostDashboard(device);

        var request = ToRequest(device);

        // Act
        await AuthorizeClientAsAdmin(malicious);
        var response = await malicious.PatchAsync($"{Paths.DashboardsPath}/{dashboard}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task DuplicateDashboardNameNotAllowed()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var dashboard = await PostDashboard(device);
        var dashboardWithTheSameName = await PostDashboard(device);
        var request = ToRequest(device);

        // Act
        await AuthorizeClientAsUser(user);
        var response = await user.PatchAsync($"{Paths.DashboardsPath}/{dashboard}", request.Serialize());
        var wrongResponse = await user.PatchAsync($"{Paths.DashboardsPath}/{dashboardWithTheSameName}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, wrongResponse.StatusCode);
    }

    [Fact]
    public async Task FriendCanPatchDashboard()
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var dashboard = await PostDashboard(device);
        await PrepareHomeSharingForAdmin(home, _displayPrivilege);
        await PrepareDeviceSharing(_managePrivilege, device);
        var request = ToRequest(device);

        // Act
        await AuthorizeClientAsAdmin(friend);
        var response = await friend.PatchAsync($"{Paths.DashboardsPath}/{dashboard}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [MemberData(nameof(NotManageDevicePrivileges))]
    public async Task FriendCanNotPatchDashboard(string privilege)
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var dashboard = await PostDashboard(device);
        await PrepareHomeSharingForAdmin(home, _displayPrivilege);
        await PrepareDeviceSharing(privilege, device);
        var request = ToRequest(device);

        // Act
        await AuthorizeClientAsAdmin(friend);
        var response = await friend.PatchAsync($"{Paths.DashboardsPath}/{dashboard}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    private static JsonObject ToRequest(Guid deviceId) =>
        RequestFactory.CreatePatchDashboard(deviceId, Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

}