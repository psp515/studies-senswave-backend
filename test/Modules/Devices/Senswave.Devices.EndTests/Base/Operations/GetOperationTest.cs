using Senswave.TestInfrastructure.TestEnvironments.Base;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Operations;

[Trait("Collection", "EndTest")]
public class GetOperationTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (home, device, operation) = await TestInitialize();

        // Act
        var response = await client.GetAsync($"{Paths.OperationsPath}/{operation}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserCanGetHisOperation()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (home, device, operation) = await TestInitialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.OperationsPath}/{operation}");
        var content = await response.Content.ReadAsStringAsync();
        var data = JsonNode.Parse(content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(data!["configuration"]);
        Assert.True(data["configuration"]!["isJson"]!.GetValue<bool>());
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MaliciousCanNotOperate()
    {
        // Arrange
        var stealer = CreateUnauthorizedClient();
        var (home, device, operation) = await TestInitialize();

        // Act
        await AuthorizeClientAsAdmin(stealer);
        var response = await stealer.GetAsync($"{Paths.OperationsPath}/{operation}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(DisplayDevicePrivileges))]
    public async Task FriendCanOperate(string privilege)
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        var (home, device, operation) = await TestInitialize();

        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(privilege, device);

        // Act
        await AuthorizeClientAsAdmin(friend);
        var response = await friend.GetAsync($"{Paths.OperationsPath}/{operation}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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