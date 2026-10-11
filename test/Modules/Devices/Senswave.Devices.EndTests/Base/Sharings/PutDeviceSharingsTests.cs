using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;

namespace Senswave.Devices.EndTests.Base.Sharings;

[Trait("Collection", "EndTest")]
public class PutDeviceSharingsTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHome();
        var broker = await PostBroker();

        await PutBrokerForHome(broker, home);
        await PrepareHomeSharingForAdmin(home);
        var device = await PostDevice(home);
        var request = RequestFactory.CreatePostDeviceSharing(
            deviceId: device,
            friendEmail: "admin@gmail.com",
            sharingType: _displayPrivilege);

        // Act
        var response = await client.PutAsync($"{Paths.DevicesPath}/sharings", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanOverriderSharing()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHome();
        var broker = await PostBroker();

        await PutBrokerForHome(broker, home);
        await PrepareHomeSharingForAdmin(home);
        var device = await PostDevice(home);
        var request = RequestFactory.CreatePostDeviceSharing(
          deviceId: device,
          friendEmail: "admin@gmail.com",
          sharingType: _displayPrivilege);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PutAsync($"{Paths.DevicesPath}/sharings", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task HomeMustBeShared()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHome();
        var broker = await PostBroker();

        await PutBrokerForHome(broker, home);

        var device = await PostDevice(home);
        var request = RequestFactory.CreatePostDeviceSharing(
            deviceId: device,
            friendEmail: "admin@gmail.com",
            sharingType: _displayPrivilege);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PutAsync($"{Paths.DevicesPath}/sharings", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task IntruderCannotCreateDeviceSharing()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHome();
        var broker = await PostBroker();

        await PutBrokerForHome(broker, home);
        await PrepareHomeSharingForAdmin(home);
        var device = await PostDevice(home);
        var request = RequestFactory.CreatePostDeviceSharing(
            deviceId: device,
            friendEmail: "admin@gmail.com",
            sharingType: _displayPrivilege);

        // Act
        await AuthorizeClientAsIntruder(client);
        var response = await client.PutAsync($"{Paths.DevicesPath}/sharings", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [MemberData(nameof(DevicePrivileges))]
    public async Task FriendCannotCreateDeviceSharing(string privilage)
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        await PrepareHomeSharingForAdmin(home);
        var device = await PostDevice(home);
        await PrepareDeviceSharing(privilage, device);

        var request = RequestFactory.CreatePostDeviceSharing(
            deviceId: device,
            friendEmail: "intruder@gmail.com",
            sharingType: _displayPrivilege);

        // Act
        await AuthorizeClientAsAdmin(client);
        var response = await client.PutAsync($"{Paths.DevicesPath}/sharings", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}