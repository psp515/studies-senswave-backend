using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Devices.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.Devices.EndTests.Base.Dashbaords;

[Trait("Collection", "EndTest")]
public class DeleteDashboardWidgetTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var response = await client.DeleteAsync($"{Paths.DashboardsPath}/{Guid.NewGuid()}/widgets/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanDeleteWidgetFromDashboard()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (_, _, dashboard, widget) = await Arrange();
        using var scope = Factory.Server.Services.CreateScope();
        var dashboardContext = scope.ServiceProvider.GetRequiredService<DevicesContext>();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.DeleteAsync($"{Paths.DashboardsPath}/{dashboard}/widgets/{widget}");
        var quereiedDashboard = await dashboardContext.Dashboards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dashboard, default);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull(quereiedDashboard);
        Assert.Empty(quereiedDashboard!.Configuration["positionedWidgets"]!.AsArray());
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MalciousCanNotAct()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (_, _, dashboard, widget) = await Arrange();

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.DeleteAsync($"{Paths.DashboardsPath}/{dashboard}/widgets/{widget}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Theory]
    [MemberData(nameof(ManageDevicePrivileges))]
    public async Task FriendCanDeleteWidgetFromDashboard(string privilege)
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (home, device, dashboard, widget) = await Arrange();
        await PrepareHomeSharingForAdmin(home, _displayPrivilege);
        await PrepareDeviceSharing(privilege, device);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.DeleteAsync($"{Paths.DashboardsPath}/{dashboard}/widgets/{widget}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [MemberData(nameof(NotManageDevicePrivileges))]
    public async Task FriendCanNotDeleteWidgetFromDashboard(string privilege)
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (home, device, dashboard, widget) = await Arrange();
        await PrepareHomeSharingForAdmin(home, _displayPrivilege);
        await PrepareDeviceSharing(privilege, device);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.DeleteAsync($"{Paths.DashboardsPath}/{dashboard}/widgets/{widget}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(Guid, Guid, Guid, Guid)> Arrange()
    {
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var dashboard = await PostDashboard(device);
        var operation = await PostBooleanOperation(device);
        var widget = await PostButtonWidget(operation);
        await SetWidgetOnDashboard(dashboard, widget);

        return (home, device, dashboard, widget);
    }
}
