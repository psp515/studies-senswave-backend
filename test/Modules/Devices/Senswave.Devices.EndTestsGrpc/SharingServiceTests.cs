using Senswave.Contracts.Devices.V1;
using HomeSharing = Senswave.Contracts.Homes.V1;

namespace Senswave.Devices.EndTestsGrpc;

[Trait("Collection", "EndTestGrpc")]
public class SharingServiceTests(GrpcTestEnvironment environment) : GrpcFeatureTest(environment)
{
    /// <summary>
    /// SetSharing → GetSharings → DeleteSharing, for a friend the home is already shared with.
    /// </summary>
    [Fact]
    public async Task DeviceSharingLifecycle()
    {
        // Arrange: the friend must be a member of the home before a device can be shared.
        var ownerChannel = await CreateAuthorizedChannelAsUser();
        var friendChannel = await CreateAuthorizedChannelAsAdmin();
        var homeId = await ownerChannel.CreateHomeWithBrokerAsync();
        var deviceId = await ownerChannel.CreateDeviceAsync(homeId);

        var invitation = await new HomeSharing.SharingService.SharingServiceClient(ownerChannel)
            .CreateSharingAsync(new HomeSharing.CreateSharingRequest { HomeId = homeId, FriendEmail = AdminEmail, SharingType = "Display" });
        await new HomeSharing.SharingService.SharingServiceClient(friendChannel)
            .AcceptSharingAsync(new HomeSharing.AcceptSharingRequest { Password = invitation.Password });

        var client = new SharingService.SharingServiceClient(ownerChannel);

        // SetSharing + GetSharings
        await client.SetSharingAsync(new SetSharingRequest { DeviceId = deviceId, FriendEmail = AdminEmail, SharingType = "Action" });
        var sharings = await client.GetSharingsAsync(new GetSharingsRequest { DeviceId = deviceId });
        var sharing = Assert.Single(sharings.Items, x => x.FriendEmail == AdminEmail);

        Assert.Equal("Action", sharing.SharingType);
        Assert.True(sharing.HasSharingId);

        // DeleteSharing
        await client.DeleteSharingAsync(new DeleteSharingRequest { DeviceSharingId = sharing.SharingId });
    }
}
