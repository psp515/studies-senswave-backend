using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.Devices.EndTests.Base.Sharings;

[Trait("Collection", "EndTest")]
public class DeleteDeviceSharingsTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var authorizedClient = CreateUnauthorizedClient();

        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        await PrepareHomeSharingForAdmin(home);
        var device = await PostDevice(home);
        await PrepareDeviceSharing(_managePrivilege, device);

        // Act
        await AuthorizeClientAsUser(authorizedClient);
        var sharingIds = await GetDeviceSharings(authorizedClient, device);

        var deleteResponse = await client.DeleteAsync($"{Paths.DevicesPath}/sharings/{sharingIds[0]}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task OwnerCanDeleteDeviceSharing()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);

        var device = await PostDevice(home);
        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(_managePrivilege, device);

        var sharingIds = await GetDeviceSharings(client, device);

        // Act
        await AuthorizeClientAsUser(client);
        var deleteResponse = await client.DeleteAsync($"{Paths.DevicesPath}/sharings/{sharingIds[0]}");
        var sharingIdsAfterDelete = await GetDeviceSharings(client, device);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.DoesNotContain(sharingIds[0], sharingIdsAfterDelete);
        Assert.Equal(1, sharingIds.Count - sharingIdsAfterDelete.Count);
    }
}