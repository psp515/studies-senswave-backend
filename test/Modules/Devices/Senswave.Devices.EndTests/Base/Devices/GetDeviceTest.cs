using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.Devices;

namespace Senswave.Devices.EndTests.Base.Devices;

[Trait("Collection", "EndTest")]
public class GetDeviceTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        var deviceId = await PostDevice(home);
        var response = await client.GetAsync($"{Paths.DevicesPath}/{deviceId}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserCanGetHisDevice()
    {
        // Arrange
        var admin = CreateUnauthorizedClient();

        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        var deviceId = await PostDevice(home);

        await AuthorizeClientAsUser(admin);

        // Act
        var response = await admin.GetAsync($"{Paths.DevicesPath}/{deviceId}");
        var data = await response.Content.ReadAsStringAsync();
        var deviceResponse = JsonSerializer.Deserialize<GetDeviceResponse>(data)!;

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(deviceId, deviceResponse.Id);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UserCanNotGetNotHisDevice()
    {
        // Arrange
        var admin = CreateUnauthorizedClient();

        // Act
        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        var deviceId = await PostDevice(home);
        await AuthorizeClientAsAdmin(admin);
        var response = await admin.GetAsync($"{Paths.DevicesPath}/{deviceId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [MemberData(nameof(DisplayDevicePrivileges))]
    [Theory]
    public async Task UserWithProperPrivilegeCanGetDevice(string privilege)
    {
        // Arrange
        var friend = CreateUnauthorizedClient();

        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        var deviceId = await PostDevice(home);

        await AuthorizeClientAsUser(friend);
        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(privilege, deviceId);

        // Act
        var response = await friend.GetAsync($"{Paths.DevicesPath}/{deviceId}");
        var data = await response.Content.ReadAsStringAsync();
        var deviceResponse = JsonSerializer.Deserialize<GetDeviceResponse>(data)!;

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(deviceId, deviceResponse.Id);
    }
}