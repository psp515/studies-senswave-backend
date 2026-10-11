using Senswave.TestInfrastructure.TestEnvironments.Base;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Operations;

[Trait("Collection", "EndTest")]
public class DisplayOperationsTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (home, device, operation) = await TestInitialize();

        // Act
        var response = await client.GetAsync($"{Paths.OperationsPath}/display?deviceId={device}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanDisplayOperations()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (home, device, operation) = await TestInitialize();
        await PostBooleanOperation(device);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.OperationsPath}/display?deviceId={device}");
        var responseContent = await response.Content.ReadAsStringAsync();
        var data = JsonNode.Parse(responseContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseContent);
        Assert.Equal(2, data!["items"]!.AsArray().Count());
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task FailedToGetSecondPage()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (home, device, operation) = await TestInitialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.OperationsPath}/display?deviceId={device}&page=2&size=7");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(DisplayDevicePrivileges))]
    public async Task FriendCanDisplay(string privilege)
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        var (home, device, operation) = await TestInitialize();
        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(privilege, device);

        // Act
        await AuthorizeClientAsAdmin(friend);
        var response = await friend.GetAsync($"{Paths.OperationsPath}/display?deviceId={device}");
        var responseContent = await response.Content.ReadAsStringAsync();
        var data = JsonNode.Parse(responseContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseContent);
        Assert.Single(data!["items"]!.AsArray());
    }

    private async Task<(Guid, Guid, Guid)> TestInitialize()
    {
        var brokerId = await PostBroker();
        var homeId = await PostHome();
        await PutBrokerForHome(brokerId, homeId);
        var deviceId = await PostDevice(homeId);
        var operationId = await PostBooleanOperation(deviceId);
        return (homeId, deviceId, operationId);
    }
}