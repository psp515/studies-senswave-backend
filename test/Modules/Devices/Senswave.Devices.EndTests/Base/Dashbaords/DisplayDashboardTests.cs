using Senswave.TestInfrastructure.TestEnvironments.Base;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Dashbaords;

[Trait("Collection", "EndTest")]
public class DisplayDashboardTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var response = await client.GetAsync($"{Paths.DashboardsPath}/{Guid.NewGuid()}/display");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanDisplayDashboard()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var dashboard = await PostDashboard(device);
        var operation = await PostBooleanOperation(device);
        var widget = await PostButtonWidget(operation);
        await SetWidgetOnDashboard(dashboard, widget);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.DashboardsPath}/{dashboard}/display");
        var responseContent = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonObject>(responseContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseContent);
        Assert.NotNull(data);
        Assert.True(data["configuration"]!["rows"]!.GetValue<int>() > 0);
        Assert.True(data["configuration"]!["columns"]!.GetValue<int>() > 0);
        Assert.Single(data["configuration"]!["positionedWidgets"]!.AsArray());
        Assert.Single(data["configuration"]!["calculatedWidgets"]!.AsArray());
        Assert.Equal(widget, data["configuration"]!["positionedWidgets"]!.AsArray()[0]!.AsObject()["widgetId"]!.GetValue<Guid>());
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MalciousCanNotDisplayDashboard()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var dashboard = await PostDashboard(device);
        var operation = await PostBooleanOperation(device);
        var widget = await PostButtonWidget(operation);
        await SetWidgetOnDashboard(dashboard, widget);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.GetAsync($"{Paths.DashboardsPath}/{dashboard}/display");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Theory]
    [MemberData(nameof(DisplayDevicePrivileges))]
    public async Task FriendCanDisplayDashboard(string privilege)
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var dashboard = await PostDashboard(device);
        var operation = await PostBooleanOperation(device);
        var widget = await PostButtonWidget(operation);
        await SetWidgetOnDashboard(dashboard, widget);

        await PrepareHomeSharingForAdmin(home, _displayPrivilege);
        await PrepareDeviceSharing(privilege, device);

        // Act
        await AuthorizeClientAsAdmin(friend);
        var response = await friend.GetAsync($"{Paths.DashboardsPath}/{dashboard}/display");
        var responseContent = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseContent);
    }
}
