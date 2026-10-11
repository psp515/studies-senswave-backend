using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Widgets.Types;

[Trait("Collection", "EndTest")]
public class RadioTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Fact]
    public async Task CanCreateRadio()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostOptionsOperation(device);
        var request = CreateRequest(operation);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task InvalidOption()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostOptionsOperation(device);
        var request = CreateRequest(operation);
        request["configuration"]!["options"] = new JsonArray(
            new JsonObject
            {
                ["displayName"] = "Option 1",
                ["icon"] = "Icon1",
                ["optionName"] = "Option1"
            },
            new JsonObject
            {
                ["displayName"] = "Option 2",
                ["icon"] = "Icon2",
                ["optionName"] = "Option213"
            }
        );

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task LackOfOptions()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostOptionsOperation(device);
        var request = CreateRequest(operation);
        request["configuration"]!["options"] = new JsonArray();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task PresenceGatesRadioAction()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(client);
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var widgetOperation = await PostOptionsOperation(device);
        var presenceOperation = await PostBooleanOperation(device);
        var widgetId = await PostRadioWidget(widgetOperation);
        await PatchDeviceWithBooleanPresence(device, presenceOperation);

        var action = RequestFactory.CreateActionRequest(value: JsonValue.Create("Option2")).Serialize();

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
        type: "Radio",
        name: Guid.NewGuid().ToString(),
        config: new()
        {
            ["options"] = new JsonArray(
                new JsonObject
                {
                    ["displayName"] = "Option 1",
                    ["icon"] = "Icon1",
                    ["optionName"] = "Option1"
                },
                new JsonObject
                {
                    ["displayName"] = "Option 2",
                    ["icon"] = "Icon1",
                    ["optionName"] = "Option2"
                },
                new JsonObject
                {
                    ["displayName"] = "Option 3",
                    ["icon"] = "Icon31",
                    ["optionName"] = "Option3"
                }
            )
        });
}
