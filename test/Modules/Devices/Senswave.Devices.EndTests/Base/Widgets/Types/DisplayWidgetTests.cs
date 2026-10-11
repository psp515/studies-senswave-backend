using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Widgets.Types;

[Trait("Collection", "EndTest")]
public class DisplayWidgetTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Fact]
    public async Task CanCreateDisplay()
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
    public async Task InvalidDisplay()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostIntegerRangedOperation(device);
        var request = CreateRequest(operation);
        request["configuration"]!["unit"] = "/|";

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static JsonObject CreateRequest(Guid operationId)
    => RequestFactory.CreatePostWidget(
        operationId: operationId,
        type: "Display",
        name: Guid.NewGuid().ToString(),
        config: new()
        {
            ["unit"] = "test"
        });
}
