using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Widgets;

[Trait("Collection", "EndTest")]
public class CreateWidgetTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePostWidget().Serialize();

        // Act
        var response = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanCreateWidget()
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
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CanNotCreateWidgetWithDuplicateName()
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
        var duplicateResponse = await client.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MalciousCannotCreateWidget()
    {
        // Arrange
        var malicious = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostBooleanOperation(device);
        var request = CreateRequest(operation);

        // Act
        await AuthorizeClientAsAdmin(malicious);
        var response = await malicious.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ValidationWorks()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostBooleanOperation(device);

        var badRequest = CreateRequest(operation);
        badRequest["type"]= "WRONG";
        badRequest["configuration"] = null;
        badRequest["name"] = $"{Guid.NewGuid()}{Guid.NewGuid()}{Guid.NewGuid()}{Guid.NewGuid()}";

        // Act
        await AuthorizeClientAsUser(client);
        var badResponse = await client.PostAsync(Paths.WidgetsPath, badRequest.Serialize());
        var content = await badResponse.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, badResponse.StatusCode);
        Assert.NotEmpty(content);
    }

    [Theory]
    [MemberData(nameof(ManageDevicePrivileges))]
    public async Task FriendCanCreateWidget(string privilege)
    {
        // Arrange
        var friend = CreateUnauthorizedClient();

        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostBooleanOperation(device);
        var request = CreateRequest(operation);
        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(privilege, device);

        // Act
        await AuthorizeClientAsAdmin(friend);
        var response = await friend.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [MemberData(nameof(NotManageDevicePrivileges))]
    public async Task FriednCanNotCreateWidget(string privilege)
    {
        // Arrange
        var friend = CreateUnauthorizedClient();

        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var operation = await PostBooleanOperation(device);
        var request = CreateRequest(operation);
        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(privilege, device);

        // Act
        await AuthorizeClientAsAdmin(friend);
        var response = await friend.PostAsync(Paths.WidgetsPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    private static JsonObject CreateRequest(Guid operationId)
        => RequestFactory.CreatePostWidget(
            operationId: operationId,
            type: "Button",
            name: "Widget" + Guid.NewGuid(),
            config: new()
            {
                ["value"] = true
            });
}