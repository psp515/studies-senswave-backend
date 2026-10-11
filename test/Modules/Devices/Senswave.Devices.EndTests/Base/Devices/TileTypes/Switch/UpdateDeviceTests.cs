using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using Senswave.TestInfrastructure.TestSetup.Models.Devices;

namespace Senswave.Devices.EndTests.Base.Devices.TileTypes.Switch;

[Trait("Collection", "EndTest")]
public class UpdateDeviceTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ValidatesDeviceTile()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHomeWithBroker();
        var deviceId = await PostDevice(home);
        var operationId = await PostBooleanOperation(deviceId);

        var request = RequestFactory.CreatePatchDevice(
            roomId: default,
            name: "",
            icon: "",
            type: "Switches",
            operationId: operationId);

        // Act
        var response = await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", request.Serialize());
        var getResponse = await user.GetAsync($"{Paths.DevicesPath}/{deviceId}");
        var device = JsonSerializer.Deserialize<GetDeviceResponse>(await getResponse.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Default", device!.Tile.Type);
    }

    [Fact]
    public async Task CanAssignSwitchTile()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHomeWithBroker();
        var deviceId = await PostDevice(home);
        var operationId = await PostBooleanOperation(deviceId);

        var request = RequestFactory.CreateAssignOperationToDevice(
            operationId: operationId.ToString(),
            type: "Switch").Serialize();

        // Act
        var response = await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", request);
        var getResponse = await user.GetAsync($"{Paths.DevicesPath}/{deviceId}");
        var device = JsonSerializer.Deserialize<GetDeviceResponse>(await getResponse.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Switch", device!.Tile.Type);
        Assert.Equal(operationId.ToString(), device.Tile.OperationId);
    }

    [Fact]
    public async Task CanChangeOperationForSwitchTile()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHomeWithBroker();
        var deviceId = await PostDevice(home);
        var operationId1 = await PostBooleanOperation(deviceId);
        var operationId2 = await PostBooleanOperation(deviceId);

        var request1 = RequestFactory.CreateAssignOperationToDevice(
            operationId: operationId1.ToString(),
            type: "Switch").Serialize();

        var request2 = RequestFactory.CreateAssignOperationToDevice(
            operationId: operationId2.ToString(),
            type: "Switch").Serialize();

        // Act
        var response1 = await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", request1);
        var response2 = await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", request2);
        var getResponse = await user.GetAsync($"{Paths.DevicesPath}/{deviceId}");
        var device = JsonSerializer.Deserialize<GetDeviceResponse>(await getResponse.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response1.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, response2.StatusCode);
        Assert.Equal("Switch", device!.Tile.Type);
        Assert.Equal(operationId2.ToString(), device.Tile.OperationId);
    }

    [Fact]
    public async Task CanOverrideSwitchTileWithDefault()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHomeWithBroker();
        var deviceId = await PostDevice(home);
        var operationId = await PostBooleanOperation(deviceId);

        var assignRequest = RequestFactory.CreateAssignOperationToDevice(
            operationId: operationId.ToString(),
            type: "Switch").Serialize();

        var revokeRequest = RequestFactory.CreateAssignOperationToDevice(
            type: "Default").Serialize();

        // Act
        var assignResponse = await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", assignRequest);
        var revokeResponse = await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", revokeRequest);
        var getResponse = await user.GetAsync($"{Paths.DevicesPath}/{deviceId}");
        var device = JsonSerializer.Deserialize<GetDeviceResponse>(await getResponse.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, assignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);
        Assert.Equal("Default", device!.Tile.Type);
        Assert.Null(device.Tile.OperationId);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task NonBooleanOperationRejectedForSwitchTile()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHomeWithBroker();
        var deviceId = await PostDevice(home);
        var operationId = await PostNumberOperation(deviceId);

        var request = RequestFactory.CreateAssignOperationToDevice(
            operationId: operationId.ToString(),
            type: "Switch").Serialize();

        // Act
        var response = await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
