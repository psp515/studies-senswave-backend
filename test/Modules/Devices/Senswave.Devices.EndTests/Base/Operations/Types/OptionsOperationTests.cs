using Senswave.TestInfrastructure.TestEnvironments.Base;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Operations.Types;

[Trait("Collection", "EndTest")]
public class OptionsOperationTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Fact]
    public async Task CanCreateOptionOperation()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var request = new JsonObject()
        {
            ["deviceId"] = device,
            ["name"] = Guid.NewGuid().ToString(),
            ["type"] = "Options",
            ["configuration"] = new JsonObject()
            {
                ["jsonNames"] = new JsonArray("value"),
                ["isJson"] = true,
                ["options"] = new JsonArray(
                    new JsonObject
                    {
                        ["name"] = "Option1",
                        ["value"] = "Value"
                    },
                    new JsonObject
                    {
                        ["name"] = "Option2",
                        ["value"] = 12.31
                    }
                )
            },
            ["topic"] = Guid.NewGuid().ToString()
        };

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.OperationsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task InvalidJsonOperation()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var request = new JsonObject()
        {
            ["deviceId"] = device,
            ["name"] = Guid.NewGuid().ToString(),
            ["type"] = "Options",
            ["configuration"] = new JsonObject()
            {
                ["jsonNames"] = new JsonArray("value"),
                ["isJson"] = true,
                ["options"] = new JsonArray(
                    new JsonObject
                    {
                        ["name"] = "Option2",
                        ["value"] = 12.323
                    },
                    new JsonObject
                    {
                        ["name"] = "Option2",
                        ["value"] = 12.31
                    }
                )
            },
            ["topic"] = Guid.NewGuid().ToString()
        };

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.OperationsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task InvalidOperation()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var request = new JsonObject()
        {
            ["deviceId"] = device,
            ["name"] = Guid.NewGuid().ToString(),
            ["type"] = "Options",
            ["configuration"] = new JsonObject()
            {
                ["jsonNames"] = new JsonArray("value"),
                ["isJson"] = false,
                ["options"] = new JsonArray(
                    new JsonObject
                    {
                        ["name"] = "Option2",
                        ["value"] = 12.323
                    },
                    new JsonObject
                    {
                        ["name"] = "Option2",
                        ["value"] = 12.31
                    }
                )
            },
            ["topic"] = Guid.NewGuid().ToString()
        };

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.OperationsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
