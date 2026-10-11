using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.Devices;

namespace Senswave.Devices.EndTests.Base.Dashbaords;

[Trait("Collection", "EndTest")]
public class DisplayDashboardsTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var response = await client.GetAsync($"{Paths.DashboardsPath}/display?deviceId={Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanDisplayDashboards()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        await PostDashboard(device);
        await PostDashboard(device);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.DashboardsPath}/display?deviceId={device}");
        var responseContent = await response.Content.ReadAsStringAsync();
        var items = JsonSerializer.Deserialize<DashboardListDto>(responseContent) ?? new();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content);
        Assert.Equal(2, items.Dashboards.Count());
    }

    [Theory]
    [MemberData(nameof(DisplayDevicePrivileges))]
    public async Task FriendCanDisplayDashboards(string privilege)
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(friend);


        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        await PostDashboard(device);
        await PostDashboard(device);
        await PrepareHomeSharingForAdmin(home, _displayPrivilege);
        await PrepareDeviceSharing(privilege, device);

        // Act
        var response = await friend.GetAsync($"{Paths.DashboardsPath}/display?deviceId={device}");
        var responseContent = await response.Content.ReadAsStringAsync();
        var items = JsonSerializer.Deserialize<DashboardListDto>(responseContent) ?? new();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content);
        Assert.Equal(2, items.Dashboards.Count());
    }
}
