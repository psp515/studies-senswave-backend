using Microsoft.Extensions.DependencyInjection;
using Senswave.Devices.Domain.Widgets.Repositories;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Widgets;

[Trait("Collection", "EndTest")]
public class StateTests(BaseTestEnvironment environment) : BaseFeatureTest(environment)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (_, _, _, widget) = await Arrange();

        var request = new JsonObject()
        {
            ["enabled"] = false
        };

        // Act
        var response = await client.PutAsync($"{Paths.WidgetsPath}/{widget}/state", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SwitchingStateWorks()
    {
        // Arrange
        var client = await CreateUser();
        var (_, _, _, widget) = await Arrange();
        var request = new JsonObject()
        {
            ["enabled"] = false
        };

        using var scope = Factory.Server.Services.CreateScope();
        var repository = scope.ServiceProvider.GetService<IWidgetQueryRepository>()!;

        // Act
        var firstUpdateResponse = await client.PutAsync($"{Paths.WidgetsPath}/{widget}/state", request.Serialize());
        var firstUpdateWidget = await repository.GetWidgetWithOperation(widget, default);

        request["enabled"] = true;
        var secondUpdateResponse = await client.PutAsync($"{Paths.WidgetsPath}/{widget}/state", request.Serialize());
        var secondUpdateWidget = await repository.GetWidgetWithOperation(widget, default);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, firstUpdateResponse.StatusCode);
        Assert.False(firstUpdateWidget!.Enabled);
        Assert.Equal(HttpStatusCode.NoContent, secondUpdateResponse.StatusCode);
        Assert.True(secondUpdateWidget!.Enabled);
    }

    [Theory]
    [MemberData(nameof(ManageDevicePrivileges))]
    public async Task FriendCanDeleteWidget(string privilege)
    {
        // Arrange
        var friend = await CreateAdmin();
        var (home, device, _, widget) = await Arrange();
        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(privilege, device);

        var request = new JsonObject()
        {
            ["enabled"] = false
        };

        // Act
        var result = await friend.PutAsync($"{Paths.WidgetsPath}/{widget}/state", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [MemberData(nameof(NotManageDevicePrivileges))]
    public async Task FriendCanNotDeleteWidget(string privilege)
    {
        // Arrange
        var friend = await CreateAdmin();
        var (home, device, _, widget) = await Arrange();
        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(privilege, device);

        var request = new JsonObject()
        {
            ["enabled"] = false
        };

        // Act
        var result = await friend.PutAsync($"{Paths.WidgetsPath}/{widget}/state", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    private async Task<(Guid, Guid, Guid, Guid)> Arrange()
    {
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostBooleanOperation(device);
        var widget = await PostButtonWidget(operation);

        return (home, device, operation, widget);
    }
}
