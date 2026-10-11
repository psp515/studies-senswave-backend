using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Dashbaords;

[Trait("Collection", "EndTest")]
public class CreateDashboardTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePostDashboard().Serialize();

        // Act
        var response = await client
            .PostAsync(Paths.DashboardsPath, request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanCreateDashboard()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var request = CreateRequest(device);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client
            .PostAsync(Paths.DashboardsPath, request.Serialize());

        // Arrange
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CanNotCreateDashboardWithDuplicateName()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var request = CreateRequest(device);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client
            .PostAsync(Paths.DashboardsPath, request.Serialize());

        var duplicateResponse = await client
            .PostAsync(Paths.DashboardsPath, request.Serialize());

        // Arrange
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ValidationIsWorking()
    {
        // Arrange
        var admin = CreateUnauthorizedClient();

        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var correctRequest = CreateRequest(device);

        // Act
        await AuthorizeClientAsUser(admin);
        var createdResponse = await admin.PostAsync(Paths.DashboardsPath, correctRequest.Serialize());

        correctRequest["configuration"] = new JsonObject
        {
            ["rows"] = 2,
            ["columns"] = 2,
        };
        var wrongResponse3 = await admin.PostAsync(Paths.DashboardsPath, correctRequest.Serialize());

        // Arrange
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, wrongResponse3.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MalciousCanNotCreateDevice()
    {
        // Arrange
        var malicious = CreateUnauthorizedClient();

        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var request = CreateRequest(device);

        // Act
        await AuthorizeClientAsAdmin(malicious);
        var response = await malicious
            .PostAsync(Paths.DashboardsPath, request.Serialize());

        // Arrange
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FriendCanCreateDashboard()
    {
        // Arrange
        var friend = CreateUnauthorizedClient();

        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var request = CreateRequest(device);
        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(_managePrivilege, device);

        // Act
        await AuthorizeClientAsAdmin(friend);
        var response = await friend.PostAsync(Paths.DashboardsPath, request.Serialize());

        // Arrange
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [MemberData(nameof(NotManageDevicePrivileges))]
    public async Task FriendCanNotCreateDashboard(string privilege)
    {
        // Arrange
        var friend = CreateUnauthorizedClient();

        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);
        var request = CreateRequest(device);
        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(privilege, device);

        // Act
        await AuthorizeClientAsAdmin(friend);
        var response = await friend.PostAsync(Paths.DashboardsPath, request.Serialize());

        // Arrange
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static JsonObject CreateRequest(Guid deviceId) =>
        RequestFactory.CreatePostDashboard(
            deviceId: deviceId,
            name: Guid.NewGuid().ToString(),
            icon: "print-outline",
            configuration: new JsonObject
            {
                ["rows"] = 6,
                ["columns"] = 4
            });
}