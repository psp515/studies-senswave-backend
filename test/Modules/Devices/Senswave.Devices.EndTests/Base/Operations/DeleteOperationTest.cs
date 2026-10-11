using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Devices.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.Devices.EndTests.Base.Operations;

[Trait("Collection", "EndTest")]
public class DeleteOperationTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (home, device, operation) = await TestInitialize();

        // Act
        var response = await client.DeleteAsync($"{Paths.OperationsPath}/{operation}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanDeleteOperation()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (home, device, operation) = await TestInitialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.DeleteAsync($"{Paths.OperationsPath}/{operation}");
        var checkExistenceResponse = await client.GetAsync($"{Paths.OperationsPath}/{operation}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, checkExistenceResponse.StatusCode);
    }

    [Fact]
    public async Task DataReferenceIsRemoved()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (_, _, operation) = await TestInitialize();
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DevicesContext>();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.DeleteAsync($"{Paths.OperationsPath}/{operation}");
        var checkExistenceResponse = await client.GetAsync($"{Paths.OperationsPath}/{operation}");
        var dataReferenceExists = await context.DataReferences.AnyAsync(x => x.Operations.Any(x => x.Id == operation), default);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, checkExistenceResponse.StatusCode);
        Assert.False(dataReferenceExists);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CannotDeleteOperationWhenUsedByWidget()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var (home, device, operation) = await TestInitialize();
        var dashboardId = await PostDashboard(device);
        await PostButtonWidget(operation);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.DeleteAsync($"{Paths.OperationsPath}/{operation}");
        var checkExistenceResponse = await client.GetAsync($"{Paths.OperationsPath}/{operation}");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, checkExistenceResponse.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CannotDeleteOperationWhenUsedByDeviceTile()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var home = await PostHome();
        await PutBrokerForHome(broker, home);
        var device = await PostDevice(home);
        var operation = await PostBooleanOperation(device);
        await PatchDeviceWithSwitchTile(device, operation);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.DeleteAsync($"{Paths.OperationsPath}/{operation}");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CannotDeleteOperationWhenUsedByDeviceTileAndWidget()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var home = await PostHome();
        await PutBrokerForHome(broker, home);
        var device = await PostDevice(home);
        var operation = await PostBooleanOperation(device);
        await PatchDeviceWithSwitchTile(device, operation);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.DeleteAsync($"{Paths.OperationsPath}/{operation}");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task FailsOnInvalidOperationGuid()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.DeleteAsync($"{Paths.OperationsPath}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MaliciousCanNotOperate()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var malicious = CreateUnauthorizedClient();
        var (home, device, operation) = await TestInitialize();

        // Act
        await AuthorizeClientAsIntruder(malicious);
        var response = await malicious.DeleteAsync($"{Paths.OperationsPath}/{operation}");

        await AuthorizeClientAsUser(client);
        var checkExistenceResponse = await client.GetAsync($"{Paths.OperationsPath}/{operation}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, checkExistenceResponse.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ManageDevicePrivileges))]
    public async Task FriendCanOperate(string privilege)
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        var (home, device, operation) = await TestInitialize();

        // Act
        await AuthorizeClientAsAdmin(friend);
        await PrepareHomeSharingForAdmin(home, privilege);

        var response = await friend.DeleteAsync($"{Paths.OperationsPath}/{operation}");
        var checkExistenceResponse = await friend.GetAsync($"{Paths.OperationsPath}/{operation}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, checkExistenceResponse.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [MemberData(nameof(NotManageDevicePrivileges))]
    public async Task FriendCanNotOperate(string privilege)
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var friend = CreateUnauthorizedClient();
        var (home, device, operation) = await TestInitialize();

        // Act
        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(privilege, device);

        await AuthorizeClientAsAdmin(friend);
        var response = await friend.DeleteAsync($"{Paths.OperationsPath}/{operation}");

        await AuthorizeClientAsUser(client);
        var checkExistenceResponse = await client.GetAsync($"{Paths.OperationsPath}/{operation}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, checkExistenceResponse.StatusCode);
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