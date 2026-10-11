using Senswave.TestInfrastructure.TestEnvironments.Base;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Widgets;

[Trait("Collection", "EndTest")]
public class GetWidgetTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (_, _, _, widget) = await Arrange();

        // Act
        var response = await client.GetAsync($"{Paths.WidgetsPath}/{widget}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanGetWidget()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (_, _, _, widget) = await Arrange();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.WidgetsPath}/{widget}");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonObject>(content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.True, json!["configuration"]!["value"]!.GetValueKind());
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MalciousCanNotGetWidget()
    {
        // Arrange
        var malcious = CreateUnauthorizedClient();
        var (_, _, _, widget) = await Arrange();

        // Act
        await AuthorizeClientAsAdmin(malcious);
        var response = await malcious.GetAsync($"{Paths.WidgetsPath}/{widget}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(DevicePrivileges))]
    public async Task FriendCanGetWidget(string privilege)
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        var (home, device, _, widget) = await Arrange();
        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(privilege, device);

        // Act
        await AuthorizeClientAsAdmin(friend);
        var response = await friend.GetAsync($"{Paths.WidgetsPath}/{widget}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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