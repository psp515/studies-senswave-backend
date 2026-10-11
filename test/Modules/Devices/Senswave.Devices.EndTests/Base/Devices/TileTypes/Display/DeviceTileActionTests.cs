using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;

namespace Senswave.Devices.EndTests.Base.Devices.TileTypes.Display;

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
        var operationId = await PostNumberOperation(deviceId);
        await PatchDeviceWithDisplayTile(deviceId, operationId);

        var tileAction = RequestFactory.CreateActionRequest(value: 42.0).Serialize();

        // Act
        var response = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ActionNotSupportedForDisplayTile()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(client);

        var broker = await PostBroker();
        var home = await PostHome();
        await PutBrokerForHome(broker, home);
        var deviceId = await PostDevice(home);
        var operationId = await PostNumberOperation(deviceId);
        await PatchDeviceWithDisplayTile(deviceId, operationId);

        var tileAction = RequestFactory.CreateActionRequest(value: 42.0).Serialize();

        // Act
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
        var operationId = await PostNumberOperation(deviceId);
        await PatchDeviceWithDisplayTile(deviceId, operationId);

        var tileAction = RequestFactory.CreateActionRequest(value: 42.0).Serialize();

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.PostAsync($"{Paths.DevicesPath}/{deviceId}/tile/action", tileAction);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
