using System.Text.Json.Nodes;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using Senswave.TestInfrastructure.TestSetup.Models.Devices;

namespace Senswave.Devices.EndTests.Base.Devices.TileTypes.Display;

[Trait("Collection", "EndTest")]
public class UpdateDeviceTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Fact]
    public async Task CanAssignDisplayTile()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHomeWithBroker();
        var deviceId = await PostDevice(home);
        var operationId = await PostNumberOperation(deviceId);

        var request = RequestFactory.CreateAssignOperationToDevice(
            displayableOperationId: operationId.ToString(),
            type: "Display").Serialize();

        // Act
        var response = await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", request);
        var getResponse = await user.GetAsync($"{Paths.DevicesPath}/{deviceId}");
        var device = JsonSerializer.Deserialize<GetDeviceResponse>(await getResponse.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Display", device!.Tile.Type);
        Assert.Equal(operationId.ToString(), device.Tile.DisplayableOperationId);
    }

    [Fact]
    public async Task CanChangeOperationForDisplayTile()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHomeWithBroker();
        var deviceId = await PostDevice(home);
        var operationId1 = await PostNumberOperation(deviceId);
        var operationId2 = await PostIntegerOperation(deviceId);

        var request1 = RequestFactory.CreateAssignOperationToDevice(
            displayableOperationId: operationId1.ToString(),
            type: "Display").Serialize();

        var request2 = RequestFactory.CreateAssignOperationToDevice(
            displayableOperationId: operationId2.ToString(),
            type: "Display").Serialize();

        // Act
        var response1 = await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", request1);
        var response2 = await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", request2);
        var getResponse = await user.GetAsync($"{Paths.DevicesPath}/{deviceId}");
        var device = JsonSerializer.Deserialize<GetDeviceResponse>(await getResponse.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response1.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, response2.StatusCode);
        Assert.Equal("Display", device!.Tile.Type);
        Assert.Equal(operationId2.ToString(), device.Tile.DisplayableOperationId);
    }

    [Fact]
    public async Task CanOverrideDisplayTileWithDefault()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHomeWithBroker();
        var deviceId = await PostDevice(home);
        var operationId = await PostNumberOperation(deviceId);

        var assignRequest = RequestFactory.CreateAssignOperationToDevice(
            displayableOperationId: operationId.ToString(),
            type: "Display").Serialize();

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
        Assert.Null(device.Tile.DisplayableOperationId);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task BooleanOperationRejectedForDisplayTile()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHomeWithBroker();
        var deviceId = await PostDevice(home);
        var operationId = await PostBooleanOperation(deviceId);

        var request = RequestFactory.CreateAssignOperationToDevice(
            displayableOperationId: operationId.ToString(),
            type: "Display").Serialize();

        // Act
        var response = await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CanAssignDisplayTileWithUnit()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHomeWithBroker();
        var deviceId = await PostDevice(home);
        var operationId = await PostNumberOperation(deviceId);

        var request = RequestFactory.CreateAssignOperationToDevice(
            displayableOperationId: operationId.ToString(),
            type: "Display",
            configuration: new JsonObject { ["unit"] = "°C" }).Serialize();

        // Act
        var response = await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", request);
        var getResponse = await user.GetAsync($"{Paths.DevicesPath}/{deviceId}");
        var device = JsonSerializer.Deserialize<GetDeviceResponse>(await getResponse.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Display", device!.Tile.Type);
        Assert.Equal(operationId.ToString(), device.Tile.DisplayableOperationId);
        Assert.Equal("°C", device.Tile.Configuration?["unit"]?.GetValue<string>());
    }

    [Fact]
    public async Task CanOverrideDisplayTileUnit()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHomeWithBroker();
        var deviceId = await PostDevice(home);
        var operationId = await PostNumberOperation(deviceId);

        var assignRequest = RequestFactory.CreateAssignOperationToDevice(
            displayableOperationId: operationId.ToString(),
            type: "Display",
            configuration: new JsonObject { ["unit"] = "°C" }).Serialize();

        var overrideRequest = RequestFactory.CreateAssignOperationToDevice(
            displayableOperationId: operationId.ToString(),
            type: "Display",
            configuration: new JsonObject { ["unit"] = "°F" }).Serialize();

        // Act
        var assignResponse = await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", assignRequest);
        var overrideResponse = await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", overrideRequest);
        var getResponse = await user.GetAsync($"{Paths.DevicesPath}/{deviceId}");
        var device = JsonSerializer.Deserialize<GetDeviceResponse>(await getResponse.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, assignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, overrideResponse.StatusCode);
        Assert.Equal("Display", device!.Tile.Type);
        Assert.Equal("°F", device.Tile.Configuration?["unit"]?.GetValue<string>());
    }
}
