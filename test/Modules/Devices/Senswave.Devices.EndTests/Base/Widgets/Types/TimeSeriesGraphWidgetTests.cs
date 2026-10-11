using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Widgets.Types;

[Trait("Collection", "EndTest")]
public class TimeSeriesGraphWidgetTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Fact]
    public async Task CanCreateWithIntegerOperation()
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
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CanCreateWithNumberOperation()
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
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task InvalidUnit()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostIntegerRangedOperation(device);
        var request = CreateRequest(operation);
        request["configuration"]!["displayUnit"] = "/|";

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task InvalidinitialNumberOfData()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostIntegerRangedOperation(device);
        var request = CreateRequest(operation);
        request["configuration"]!["initialNumberOfData"] = 0;

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task InvalidDisplayType()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostIntegerRangedOperation(device);
        var request = CreateRequest(operation);
        request["configuration"]!["displayType"] = "unknown";

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task BooleanOperationNotSupported()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostBooleanOperation(device);
        var request = CreateRequest(operation);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static JsonObject CreateRequest(Guid operationId)
        => RequestFactory.CreatePostWidget(
            operationId: operationId,
            type: "TimeSeriesGraph",
            name: Guid.NewGuid().ToString(),
            config: new()
            {
                ["displayUnit"] = "%",
                ["defaultDisplayTimeRangeSeconds"] = 3600,
                ["initialNumberOfData"] = 100,
                ["displayType"] = "lines"
            });
}
