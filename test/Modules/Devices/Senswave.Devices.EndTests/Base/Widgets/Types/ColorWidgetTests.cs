using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Widgets.Types;

[Trait("Collection", "EndTest")]
public class ColorWidgetTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Fact]
    public async Task CanCreateColorWidget()
    {
        // Arrange
        var client = await AuthorizedUser();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostHexColorOperation(device);
        var request = RequestFactory.CreatePostWidget(
            operationId: operation,
            type: "Color",
            name: Guid.NewGuid().ToString(),
            config: []);

        // Act
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task InvalidOperationForWidget()
    {
        // Arrange
        var client = await AuthorizedUser();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostIntegerOperation(device);
        var request = RequestFactory.CreatePostWidget(
            operationId: operation,
            type: "Color",
            name: Guid.NewGuid().ToString(),
            config: []);
        // Act
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task PresenceGatesColorAction()
    {
        // Arrange
        var client = await AuthorizedUser();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var widgetOperation = await PostHexColorOperation(device);
        var presenceOperation = await PostBooleanOperation(device);
        var widgetId = await PostColorWidget(widgetOperation);
        await PatchDeviceWithBooleanPresence(device, presenceOperation);

        var action = RequestFactory.CreateActionRequest(value: JsonValue.Create("#FF5733")).Serialize();

        // Act 1 — no presence values yet → 409
        var response1 = await client.PostAsync($"{Paths.WidgetsPath}/{widgetId}/action", action);

        // Set presence to true
        await SetPresenceOperationValue(presenceOperation, true);

        // Act 2 — presence = true → passes presence check, fails at broker → 400
        var response2 = await client.PostAsync($"{Paths.WidgetsPath}/{widgetId}/action", action);

        // Set presence to false (newer)
        await SetPresenceOperationValue(presenceOperation, false, DateTime.UtcNow.AddSeconds(1));

        // Act 3 — presence = false → 409
        var response3 = await client.PostAsync($"{Paths.WidgetsPath}/{widgetId}/action", action);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response1.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, response3.StatusCode);
    }
}
