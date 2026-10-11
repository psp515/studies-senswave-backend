using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Devices.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Devices;

[Trait("Collection", "EndTest")]
public class DisplayDeviceTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    public static List<object[]> CanDisplayPriviliges => HomePriviliges;

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var device = await Arrange();

        // Act
        var response = await client.GetAsync($"{Paths.DevicesPath}/{device}/display");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanGetDevice()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var deviceId = await Arrange();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.DevicesPath}/{deviceId}/display");
        var responseContent = await response.Content.ReadAsStringAsync();
        var device = JsonNode.Parse(responseContent)!.AsObject();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseContent);
        Assert.Equal(deviceId.ToString(), device["id"]!.ToString());
    }

    [Fact]
    public async Task FriendCanGetDeviceCreatedAfterSharing()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        await PostDevice(home);
        await PrepareHomeSharingForAdmin(home);
        var deviceId = await PostDevice(home);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.GetAsync($"{Paths.DevicesPath}/{deviceId}/display");
        var responseContent = await response.Content.ReadAsStringAsync();
        var device = JsonNode.Parse(responseContent)!.AsObject();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseContent);
        Assert.Equal(deviceId.ToString(), device["id"]!.ToString());
    }

    [Theory]
    [MemberData(nameof(CanDisplayPriviliges))]
    public async Task FriendsCanDisplayDevices(string privilege)
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        await PostDevice(home);
        var deviceId = await PostDevice(home);
        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(privilege, deviceId);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.GetAsync($"{Paths.DevicesPath}/{deviceId}/display");
        var responseContent = await response.Content.ReadAsStringAsync();
        var device = JsonNode.Parse(responseContent)!.AsObject();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseContent);
        Assert.Equal(deviceId.ToString(), device["id"]!.ToString());
    }

    [Theory]
    [MemberData(nameof(HomePriviliges))]
    public async Task PriviligeIsDeduced(string privilege)
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        await PostDevice(home);
        var deviceId = await PostDevice(home);
        await PrepareHomeSharingForAdmin(home, privilege);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.GetAsync($"{Paths.DevicesPath}/{deviceId}/display");
        var responseContent = await response.Content.ReadAsStringAsync();
        var device = JsonNode.Parse(responseContent)!.AsObject();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseContent);
        Assert.Equal(deviceId.ToString(), device["id"]!.ToString());
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task IntruderHasNoAccess()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var deviceId = await Arrange();

        // Act
        await AuthorizeClientAsIntruder(client);
        var response = await client.GetAsync($"{Paths.DevicesPath}/{deviceId}/display");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<Guid> Arrange()
    {
        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);

        await PostDevice(home);
        return await PostDevice(home);
    }
}
