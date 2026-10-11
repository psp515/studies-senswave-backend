using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Widgets.Types;

[Trait("Collection", "EndTest")]
public class SliderTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Fact]
    public async Task CanCreateSlider()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostIntegerRangedOperation(device);
        var request = CreateRequest(operation);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task InvalidStep()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostIntegerRangedOperation(device);
        var request = CreateRequest(operation);
        request["configuration"]!["step"] = "/|";

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task InvalidIntegerOperation()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
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
        var client = CreateUnauthorizedClient();
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
    public async Task PresenceGatesSliderAction()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(client);
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var widgetOperation = await PostIntegerRangedOperation(device);
        var presenceOperation = await PostBooleanOperation(device);
        var widgetId = await PostSliderWidget(widgetOperation);
        await PatchDeviceWithBooleanPresence(device, presenceOperation);

        var action = RequestFactory.CreateActionRequest(value: JsonValue.Create(5)).Serialize();

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
        type: "Slider",
        name: Guid.NewGuid().ToString(),
        config: new()
        {
            ["step"] = 1
        });
}
