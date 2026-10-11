using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;

namespace Senswave.Devices.EndTests.Base.Devices.TileTypes.Switch;

[Trait("Collection", "EndTest")]
public class DeviceTileActionTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var home = await PostHome();
        await PutBrokerForHome(broker, home);
        var deviceId = await PostDevice(home);
        var operationId = await PostBooleanOperation(deviceId);
        await PatchDeviceWithSwitchTile(deviceId, operationId);

        var tileAction = RequestFactory.CreateActionRequest(value: false).Serialize();

        // Act
        var response = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ActionFailsIfNoWorkingBroker()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var home = await PostHome();
        await PutBrokerForHome(broker, home);
        var deviceId = await PostDevice(home);
        var operationId = await PostBooleanOperation(deviceId);
        await PatchDeviceWithSwitchTile(deviceId, operationId);

        var tileAction = RequestFactory.CreateActionRequest(value: false).Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task IfNotOwnerCanNotInvokeAction()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var home = await PostHome();
        await PutBrokerForHome(broker, home);
        var deviceId = await PostDevice(home);
        var operationId = await PostBooleanOperation(deviceId);
        await PatchDeviceWithSwitchTile(deviceId, operationId);

        var tileAction = RequestFactory.CreateActionRequest(value: false).Serialize();

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task TileActionAllowedWithDefaultPresence()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(client);

        var broker = await PostBroker();
        var home = await PostHome();
        await PutBrokerForHome(broker, home);
        var deviceId = await PostDevice(home);
        var operationId = await PostBooleanOperation(deviceId);
        await PatchDeviceWithSwitchTile(deviceId, operationId);

        var tileAction = RequestFactory.CreateActionRequest(value: false).Serialize();

        // Act — presence check passes, fails at broker
        var response = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task TileActionPresenceGatesAccess()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(client);

        var broker = await PostBroker();
        var home = await PostHome();
        await PutBrokerForHome(broker, home);
        var deviceId = await PostDevice(home);
        var tileOperationId = await PostBooleanOperation(deviceId);
        var presenceOperationId = await PostBooleanOperation(deviceId);
        await PatchDeviceWithSwitchTile(deviceId, tileOperationId);
        await PatchDeviceWithBooleanPresence(deviceId, presenceOperationId);

        var tileAction = RequestFactory.CreateActionRequest(value: false).Serialize();

        // Act 1 — no presence values yet → 409
        var response1 = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction);

        // Set presence operation value to true
        await SetPresenceOperationValue(presenceOperationId, true);

        // Act 2 — presence = true → passes presence check, fails at broker → 400
        var response2 = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction);

        // Set presence operation value to false (newer timestamp)
        await SetPresenceOperationValue(presenceOperationId, false, DateTime.UtcNow.AddSeconds(1));

        // Act 3 — presence = false → 409
        var response3 = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response1.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, response3.StatusCode);
    }
}
