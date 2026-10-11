using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.Devices.EndTests.Base.Widgets;

[Trait("Collection", "EndTest")]
public class DeleteWidgetTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (_, _, _, widget) = await Arrange();

        // Act
        var result = await client.DeleteAsync($"{Paths.WidgetsPath}/{widget}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task OwnerCanDeleteWidget()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (_, _, _, widget) = await Arrange();

        // Act
        await AuthorizeClientAsUser(client);
        var result = await client.DeleteAsync($"{Paths.WidgetsPath}/{widget}");
        var getResults = await client.GetAsync($"{Paths.WidgetsPath}/{widget}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResults.StatusCode);
    }

    [Fact]
    public async Task DeleteWidgetDoesNotAffectOperation()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (_, _, operation, widget) = await Arrange();

        // Act
        await AuthorizeClientAsUser(client);

        var result = await client
            .DeleteAsync($"{Paths.WidgetsPath}/{widget}");

        var getResult = await client
            .GetAsync($"{Paths.WidgetsPath}/{widget}");

        var operationResult = await client
            .GetAsync($"{Paths.OperationsPath}/{operation}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResult.StatusCode);
        Assert.Equal(HttpStatusCode.OK, operationResult.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MaliciousCanNotDeleteWidget()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var malicious = CreateUnauthorizedClient();
        var (_, _, _, widget) = await Arrange();

        // Act
        await AuthorizeClientAsUser(client);
        await AuthorizeClientAsAdmin(malicious);

        var result = await malicious
            .DeleteAsync($"{Paths.WidgetsPath}/{widget}");

        var getResults = await client
            .GetAsync($"{Paths.WidgetsPath}/{widget}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResults.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ManageDevicePrivileges))]
    public async Task FriendCanDeleteWidget(string privilege)
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var friend = CreateUnauthorizedClient();

        var (home, device, _, widget) = await Arrange();
        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(privilege, device);

        // Act
        await AuthorizeClientAsUser(client);
        await AuthorizeClientAsAdmin(friend);
        var result = await friend.DeleteAsync($"{Paths.WidgetsPath}/{widget}");
        var getResults = await client.GetAsync($"{Paths.WidgetsPath}/{widget}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResults.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [MemberData(nameof(NotManageDevicePrivileges))]
    public async Task FriendCanNotDeleteWidget(string privilege)
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(client);
        var friend = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(friend);

        var (home, device, _, widget) = await Arrange();
        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(privilege, device);

        // Act
        var result = await friend.DeleteAsync($"{Paths.WidgetsPath}/{widget}");
        var getResults = await client.GetAsync($"{Paths.WidgetsPath}/{widget}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResults.StatusCode);
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