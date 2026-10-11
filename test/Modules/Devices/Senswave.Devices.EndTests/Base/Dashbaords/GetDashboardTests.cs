using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.Devices.EndTests.Base.Dashbaords;

[Trait("Collection", "EndTest")]
public class GetDashboardTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var response = await client.GetAsync($"{Paths.DashboardsPath}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanGetDashboard()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var dashboard = await PostDashboard(device);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.DashboardsPath}/{dashboard}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
    }

    [Theory]
    [MemberData(nameof(DisplayDevicePrivileges))]
    public async Task FriendCanGetDashboard(string privilege)
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(friend);

        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var dashboard = await PostDashboard(device);
        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(privilege, device);

        // Act
        var response = await friend.GetAsync($"{Paths.DashboardsPath}/{dashboard}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
    }

}
