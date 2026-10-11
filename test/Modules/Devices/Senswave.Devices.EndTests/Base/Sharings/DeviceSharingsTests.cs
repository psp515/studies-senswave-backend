using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.Devices;

namespace Senswave.Devices.EndTests.Base.Sharings;

[Trait("Collection", "EndTest")]
public class DeviceSharingsTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
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
        await PrepareDeviceSharing(_managePrivilege, device);

        // Act
        var response = await client.GetAsync($"{Paths.DevicesPath}/sharings?deviceId={device}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanGetDeviceSharings()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        await AuthorizeClientAsUser(client);
        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        await PrepareHomeSharingForAdmin(home);
        var device = await PostDevice(home);
        await PrepareDeviceSharing(_managePrivilege, device);

        // Act
        var response = await client.GetAsync($"{Paths.DevicesPath}/sharings?deviceId={device}");
        var responseDto = JsonSerializer.Deserialize<GetDeviceTestResponse>(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseDto);
        Assert.NotEmpty(responseDto.Items);
    }

    [Fact]
    public async Task OwnerGetsNotOverridenDeviceSharings()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        await AuthorizeClientAsUser(client);
        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        await PrepareHomeSharingForAdmin(home, _displayPrivilege);
        var device = await PostDevice(home);

        // Act
        var response = await client.GetAsync($"{Paths.DevicesPath}/sharings?deviceId={device}");
        var responseDto = JsonSerializer.Deserialize<GetDeviceTestResponse>(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseDto);
        Assert.Single(responseDto.Items);
        Assert.Equal(_actionPrivilege, responseDto.Items.First().SharingType);
        Assert.Null(responseDto.Items.First().SharingId);
    }

    [Fact]
    public async Task OwnerGetsCorrectlyOverridenSharing()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        await AuthorizeClientAsUser(client);
        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        await PrepareHomeSharingForAdmin(home, _displayPrivilege);
        var device = await PostDevice(home);
        await PrepareDeviceSharing(_managePrivilege, device);

        // Act
        var response = await client.GetAsync($"{Paths.DevicesPath}/sharings?deviceId={device}");
        var content = await response.Content.ReadAsStringAsync();
        var responseDto = JsonSerializer.Deserialize<GetDeviceTestResponse>(content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseDto);
        Assert.Single(responseDto.Items);
        Assert.Equal(_managePrivilege, responseDto.Items.First().SharingType);
    }
}