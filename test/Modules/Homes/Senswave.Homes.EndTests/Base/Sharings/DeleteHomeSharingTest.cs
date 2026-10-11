using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Devices.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.Homes;
using System.Net;
using System.Text.Json;

namespace Senswave.Homes.EndTests.Base.Sharings;

[Trait("Collection", "EndTest")]
public class DeleteSharingTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var user = CreateUnauthorizedClient();

        // Act 
        var response = await user.DeleteAsync($"{Paths.HomesPath}/sharings/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserCanRemoveHomeSharings()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHome();
        await PrepareHomeSharingForAdmin(home);

        // Act 
        var getResponse = await user.GetAsync($"{Paths.HomesPath}/sharings?homeId={home}");
        var jsonResponse = JsonSerializer.Deserialize<HomeSharingResponse>(await getResponse.Content.ReadAsStringAsync());
        Assert.NotNull(jsonResponse);

        var homeSharingId = jsonResponse.Items[0].SharingId;
        var response = await user.DeleteAsync($"{Paths.HomesPath}/sharings/{homeSharingId}");
        var getResponseAssert = await user.GetAsync($"{Paths.HomesPath}/sharings?homeId={home}");
        var jsonResponseAssert =
            JsonSerializer.Deserialize<HomeSharingResponse>(await getResponseAssert.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponseAssert.StatusCode);
        Assert.NotNull(jsonResponseAssert);
        Assert.Empty(jsonResponseAssert.Items);
    }

    [Fact]
    public async Task DevicesSharingsAreRemoved()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHome();
        await PutBrokerForHome(home);
        await PrepareHomeSharingForAdmin(home);
        var deviceId = await PostDevice(home);
        await PrepareDeviceSharing(_displayPrivilege, deviceId);

        using var scope = Services.CreateScope();
        var devicesContext = scope.ServiceProvider.GetRequiredService<DevicesContext>();

        // Act 

        var deviceSharingExists = await devicesContext.DeviceSharings
            .Where(x => x.DeviceId == deviceId)
            .AnyAsync();

        var getResponse = await user.GetAsync($"{Paths.HomesPath}/sharings?homeId={home}");
        var jsonResponse = JsonSerializer.Deserialize<HomeSharingResponse>(await getResponse.Content.ReadAsStringAsync())!;
        var homeSharingId = jsonResponse.Items[0].SharingId;

        var response = await user.DeleteAsync($"{Paths.HomesPath}/sharings/{homeSharingId}");
        var getResponseAssert = await user.GetAsync($"{Paths.HomesPath}/sharings?homeId={home}");
        var jsonResponseAssert = JsonSerializer.Deserialize<HomeSharingResponse>(await getResponseAssert.Content.ReadAsStringAsync());

        var deviceSharingDoesNotExist = await devicesContext.DeviceSharings
            .Where(x => x.DeviceId == deviceId)
            .AnyAsync();

        // Assert
        Assert.True(deviceSharingExists);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponseAssert.StatusCode);
        Assert.NotNull(jsonResponseAssert);
        Assert.Empty(jsonResponseAssert.Items);
        Assert.NotNull(jsonResponse);
        Assert.False(deviceSharingDoesNotExist);
    }
}