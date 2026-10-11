using Senswave.TestInfrastructure.TestEnvironments.Base;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Widgets;

[Trait("Collection", "EndTest")]
public class DisplayWidgetsTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    public static List<object[]> CanDisplayPriviliges => DevicePrivileges;

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var response = await client.GetAsync($"{Paths.WidgetsPath}/display?deviceId={Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanDisplayWidgets()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (_, device, _, _, _, _) = await Arrange();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.WidgetsPath}/display?deviceId={device}");
        var stringResponse = await response.Content.ReadAsStringAsync();
        var json = JsonNode.Parse(stringResponse)!;

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, json["items"]!.AsArray().Count);
        Assert.Single(json["items"]!.AsArray()![0]!["widgets"]!.AsArray());
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task WidgetsNotFound()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHome();
        await PutBrokerForHome(home);
        var device = await PostDevice(home);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.WidgetsPath}/display?deviceId={device}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(DevicePrivileges))]
    public async Task FriendCanDisplayWidgets(string privilige)
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (home, device, _, _, _, _) = await Arrange();

        await PrepareHomeSharingForAdmin(home, "Display");
        await PrepareDeviceSharing(privilige, device);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.GetAsync($"{Paths.WidgetsPath}/display?deviceId={device}");
        var stringResponse = await response.Content.ReadAsStringAsync();
        var json = JsonNode.Parse(stringResponse)!;

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, json["items"]!.AsArray().Count);
        Assert.Single(json["items"]!.AsArray()![0]!["widgets"]!.AsArray());
    }

    private async Task<(Guid, Guid, Guid, Guid, Guid, Guid)> Arrange()
    {
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);

        var operation = await PostBooleanOperation(device);
        var widget = await PostButtonWidget(operation);

        var operation2 = await PostBooleanOperation(device);
        var widget2 = await PostButtonWidget(operation2);

        return (home, device, operation, widget, operation2, widget2);
    }
}
