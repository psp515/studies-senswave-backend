using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Widgets.Types;

[Trait("Collection", "EndTest")]
public class SwitchTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Fact]
    public async Task CanCreateSwitch()
    {
        // Arrange
        var client = await CreateUser();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostBooleanOperation(device);
        var request = CreateRequest(operation);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task InvalidIntegerOperation()
    {
        // Arrange
        var client = await CreateUser();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostIntegerOperation(device);
        var request = CreateRequest(operation);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task InvalidNumberOperation()
    {
        // Arrange
        var client = await CreateUser();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostNumberOperation(device);
        var request = CreateRequest(operation);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task InvalidTextOperation()
    {
        // Arrange
        var client = await CreateUser();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostTextOperation(device);
        var request = CreateRequest(operation);

        // Act
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task PresenceGatesSwitchAction()
    {
        // Arrange
        var client = await CreateUser();
        await AuthorizeClientAsUser(client);
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var widgetOperation = await PostBooleanOperation(device);
        var presenceOperation = await PostBooleanOperation(device);
        var widgetId = await PostSwitchWidget(widgetOperation);
        await PatchDeviceWithBooleanPresence(device, presenceOperation);

        var action = RequestFactory.CreateActionRequest(value: "").Serialize();

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

    private static JsonObject CreateRequest(Guid operationId) => RequestFactory.CreatePostWidget(
        operationId: operationId,
        type: "Switch",
        name: Guid.NewGuid().ToString(),
        config: []);
}
