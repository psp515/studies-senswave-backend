using Senswave.TestInfrastructure.TestEnvironments.Base;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Operations;

[Trait("Collection", "EndTest")]
public class CreateOperationTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var request = CreateBooleanOperationRequest(device);

        // Act
        var response = await client.PostAsync(Paths.OperationsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ValidationIsWorking()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var emptyNameRequest = CreateBooleanOperationRequest(device);
        emptyNameRequest["name"] = "";

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.OperationsPath, emptyNameRequest.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task TopicCanNotBeSharedBetweenDevices()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var duplicateDevice = await PostDevice(home);

        var request = CreateBooleanOperationRequest(device);
        var duplicateRequest = CreateBooleanOperationRequest(duplicateDevice);
        duplicateRequest["topic"] = request["topic"]!.GetValue<string>();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.OperationsPath, request.Serialize());
        var duplicateResponse = await client.PostAsync(Paths.OperationsPath, duplicateRequest.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task TopicCanNotBeSharedBetweenDevicesExecutedConcurrently()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var duplicateDevice = await PostDevice(home);

        var request = CreateBooleanOperationRequest(device);
        var duplicateRequest = CreateBooleanOperationRequest(duplicateDevice);
        duplicateRequest["topic"] = request["topic"]!.GetValue<string>();

        // Act
        await AuthorizeClientAsUser(client);

        var responseTask = client
            .PostAsync(Paths.OperationsPath, request.Serialize());

        var duplicateResponseTask = client
            .PostAsync(Paths.OperationsPath, duplicateRequest.Serialize());

        var responses = await Task.WhenAll(responseTask, duplicateResponseTask);

        // Assert
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.Conflict || r.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Theory]
    [MemberData(nameof(ManageDevicePrivileges))]
    public async Task FriendCanCreateOperation(string privilege)
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var request = CreateBooleanOperationRequest(device);

        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(privilege, device);

        // Act
        await AuthorizeClientAsAdmin(friend);
        var response = await friend.PostAsync(Paths.OperationsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [MemberData(nameof(NotManageDevicePrivileges))]
    public async Task FriendCanNotCreateOperation(string privilege)
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var request = CreateBooleanOperationRequest(device);

        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(privilege, device);

        // Act
        await AuthorizeClientAsAdmin(friend);
        var response = await friend.PostAsync(Paths.OperationsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static JsonObject CreateBooleanOperationRequest(Guid deviceId) => new()
    {
        ["deviceId"] = deviceId,
        ["name"] = Guid.NewGuid().ToString(),
        ["type"] = "Boolean",
        ["configuration"] = new JsonObject()
        {
            ["jsonNames"] = new JsonArray("value"),
            ["isJson"] = true
        },
        ["topic"] = Guid.NewGuid().ToString()
    };
}