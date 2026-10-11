using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Widgets.Types;

[Trait("Collection", "EndTest")]
public class ButtonWidgetTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Fact]
    public async Task CanCreateIntegerButton()
    {
        // Arrange
        var client = await AuthorizedUser();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostIntegerRangedOperation(device);

        var request = RequestFactory.CreatePostWidget(
            operationId: operation,
            type: "Button",
            name: Guid.NewGuid().ToString(),
            config: new()
            {
                ["value"] = 42
            });


        // Act
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CanCreateBooleanButton()
    {
        // Arrange
        var client = await AuthorizedUser();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostBooleanOperation(device);
        var request = RequestFactory.CreatePostWidget(
            operationId: operation,
            type: "Button",
            name: Guid.NewGuid().ToString(),
            config: new()
            {
                ["value"] = true
            });
        // Act
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());
        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task OptionButton()
    {
        // Arrange
        var client = await AuthorizedUser();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostOptionsOperation(device);
        var request = RequestFactory.CreatePostWidget(
            operationId: operation,
            type: "Button",
            name: Guid.NewGuid().ToString(),
            config: new()
            {
                ["value"] = JsonValue.Create("Option3")
            });

        // Act
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    }

    [Fact]
    public async Task HexColorButton()
    {
        // Arrange
        var client = await AuthorizedUser();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostHexColorOperation(device);
        var request = RequestFactory.CreatePostWidget(
            operationId: operation,
            type: "Button",
            name: Guid.NewGuid().ToString(),
            config: new()
            {
                ["value"] = "#FF5733"
            });

        // Act
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task NumberButton()
    {
        // Arrange
        var client = await AuthorizedUser();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostNumberOperation(device);
        var request = RequestFactory.CreatePostWidget(
            operationId: operation,
            type: "Button",
            name: Guid.NewGuid().ToString(),
            config: new()
            {
                ["value"] = 3.14
            });
        // Act
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task TextButton()
    {
        // Arrange
        var client = await AuthorizedUser();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostTextOperation(device);
        var request = RequestFactory.CreatePostWidget(
            operationId: operation,
            type: "Button",
            name: Guid.NewGuid().ToString(),
            config: new()
            {
                ["value"] = "Hello World"
            });

        // Act
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task PresenceGatesButtonAction()
    {
        // Arrange
        var client = await AuthorizedUser();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var widgetOperation = await PostBooleanOperation(device);
        var presenceOperation = await PostBooleanOperation(device);
        var widgetId = await PostButtonWidget(widgetOperation);
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
}
