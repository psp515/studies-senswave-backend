using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Devices.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Dashbaords;

[Trait("Collection", "EndTest")]
public class SetWidgetOnDashboardTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = Request(Guid.NewGuid());

        // Act
        var response = await client.PutAsync($"{Paths.DashboardsPath}/{Guid.NewGuid()}/widgets", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanSetWidgetOnDashboard()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (_, _, dashboard, widget) = await Arrange();
        var request = Request(widget);
        using var scope = Factory.Server.Services.CreateScope();
        var dashboardContext = scope.ServiceProvider.GetRequiredService<DevicesContext>();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PutAsync($"{Paths.DashboardsPath}/{dashboard}/widgets", request.Serialize());
        var quereiedDashboard = await dashboardContext.Dashboards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dashboard, default);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull(quereiedDashboard);
        Assert.Equal(widget, quereiedDashboard!.Configuration["positionedWidgets"]!.AsArray()[0]!["widgetId"]!.GetValue<Guid>());
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task WidgetDoesNotExist()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (_, _, dashboard, _) = await Arrange();
        var request = Request(Guid.NewGuid());

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PutAsync($"{Paths.DashboardsPath}/{dashboard}/widgets", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ValidationWorks()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (_, _, dashboard, _) = await Arrange();
        var request = Request(Guid.NewGuid());
        request["rowSpan"] = 0;

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PutAsync($"{Paths.DashboardsPath}/{dashboard}/widgets", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MalciousCanNotAct()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (_, _, dashboard, widget) = await Arrange();
        var request = Request(widget);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.PutAsync($"{Paths.DashboardsPath}/{dashboard}/widgets", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ManageDevicePrivileges))]
    public async Task FriendCanSetWidgetOnDashboard(string privilege)
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (home, device, dashboard, widget) = await Arrange();
        await PrepareHomeSharingForAdmin(home, _displayPrivilege);
        await PrepareDeviceSharing(privilege, device);
        var request = Request(widget);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.PutAsync($"{Paths.DashboardsPath}/{dashboard}/widgets", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [MemberData(nameof(NotManageDevicePrivileges))]
    public async Task FriendCanNotSetWidgetOnDashboard(string privilege)
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (home, device, dashboard, widget) = await Arrange();
        await PrepareHomeSharingForAdmin(home, _displayPrivilege);
        await PrepareDeviceSharing(privilege, device);
        var request = Request(widget);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.PutAsync($"{Paths.DashboardsPath}/{dashboard}/widgets", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static JsonObject Request(Guid widgetId) => new()
    {
        ["widgetId"] = widgetId,
        ["row"] = 0,
        ["rowSpan"] = 1,
        ["column"] = 0,
        ["columnSpan"] = 2
    };

    private async Task<(Guid, Guid, Guid, Guid)> Arrange()
    {
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var dashboard = await PostDashboard(device);
        var operation = await PostBooleanOperation(device);
        var widget = await PostButtonWidget(operation);

        return (home, device, dashboard, widget);
    }
}
