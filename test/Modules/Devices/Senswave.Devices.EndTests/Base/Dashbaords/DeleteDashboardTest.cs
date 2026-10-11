using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.Devices.EndTests.Base.Dashbaords;

[Trait("Collection", "EndTest")]
public class DeleteDashboardTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var response = await client.DeleteAsync($"{Paths.DashboardsPath}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserCanDeleteDashboard()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (_, _, dashboard) = await Arrange();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.DeleteAsync($"{Paths.DashboardsPath}/{dashboard}");
        var getDashboardResponse = await client.GetAsync($"{Paths.DashboardsPath}/{dashboard}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getDashboardResponse.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MaliciousCanNotDeleteDashboard()
    {
        // Arrange
        var malicious = CreateUnauthorizedClient();
        var client = CreateUnauthorizedClient();

        var (_, _, dashboard) = await Arrange();

        // Act
        await AuthorizeClientAsAdmin(malicious);
        var response = await malicious.DeleteAsync($"{Paths.DashboardsPath}/{dashboard}");

        await AuthorizeClientAsUser(client);
        var getDashboardResponse = await client.GetAsync($"{Paths.DashboardsPath}/{dashboard}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getDashboardResponse.StatusCode);
    }

    [Fact]
    public async Task FriendCanRemoveDashboard()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (home, device, dashboard) = await Arrange();
        await PrepareHomeSharingForAdmin(home, _displayPrivilege);
        await PrepareDeviceSharing(_managePrivilege, device);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.DeleteAsync($"{Paths.DashboardsPath}/{dashboard}");
        var getDashboardResponse = await client.GetAsync($"{Paths.DashboardsPath}/{dashboard}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getDashboardResponse.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [MemberData(nameof(NotManageDevicePrivileges))]
    public async Task FriendCanNotDeleteDashbaord(string privilege)
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(client);
        var (home, device, dashboard) = await Arrange();
        await PrepareHomeSharingForAdmin(home, _managePrivilege);
        await PrepareDeviceSharing(privilege, device);

        // Act
        var response = await client.DeleteAsync($"{Paths.DashboardsPath}/{Guid.NewGuid()}");
        var getDashboardResponse = await client.GetAsync($"{Paths.DashboardsPath}/{dashboard}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getDashboardResponse.StatusCode);
    }

    private async Task<(Guid, Guid, Guid)> Arrange()
    {
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var dashboard = await PostDashboard(device);

        return (home, device, dashboard);
    }
}