using Senswave.TestInfrastructure.TestEnvironments.Base;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Devices;

[Trait("Collection", "EndTest")]
public class DisplayDevicesTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    public static List<object[]> CanDisplayPriviliges => HomePriviliges;

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await Arrange();

        // Act
        var response = await client.GetAsync($"{Paths.DevicesPath}/display?homeId={home}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanGetDevices()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await Arrange();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.DevicesPath}/display?homeId={home}");
        var responseContent = await response.Content.ReadAsStringAsync();
        var devices = JsonNode.Parse(responseContent)!["items"]!.AsArray();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseContent);
        Assert.Equal(4, devices.Count);
        Assert.All(devices, device =>
        {
            Assert.NotNull(device!["presence"]);
            Assert.NotNull(device["presence"]!["type"]);
        });
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task GetDevicesReturnNotFoundIfNoDevicesInHome()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHome();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.DevicesPath}/display?homeId={home}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PaginationWorks()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await Arrange();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.DevicesPath}/display?homeId={home}&page=1&size=3");
        var responseContent = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseContent);
        var devices = JsonNode.Parse(responseContent)!["items"]!.AsArray();
        Assert.Equal(3, devices.Count);
    }

    [Theory]
    [MemberData(nameof(CanDisplayPriviliges))]
    public async Task FriendsCanDisplayDevices(string privilege)
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await Arrange();
        await PrepareHomeSharingForAdmin(home, privilege);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.GetAsync($"{Paths.DevicesPath}/display?homeId={home}");
        var responseContent = await response.Content.ReadAsStringAsync();
        var devices = JsonNode.Parse(responseContent)!["items"]!.AsArray();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseContent);
        Assert.Equal(4, devices.Count);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task IntruderHasNoAccess()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await Arrange();

        // Act
        await AuthorizeClientAsIntruder(client);
        var response = await client.GetAsync($"{Paths.DevicesPath}/display?homeId={home}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<Guid> Arrange()
    {
        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);

        await PostDevice(home);
        await PostDevice(home);
        await PostDevice(home);
        await PostDevice(home);

        return home;
    }
}
