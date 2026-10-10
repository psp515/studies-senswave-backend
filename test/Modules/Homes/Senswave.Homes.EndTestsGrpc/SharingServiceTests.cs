using Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.EndTestsGrpc;

[Trait("Collection", "EndTestGrpc")]
public class SharingServiceTests(GrpcTestEnvironment environment) : GrpcFeatureTest(environment)
{
    /// <summary>
    /// CreateSharing → AcceptSharing → GetSharings → LeaveSharing, then CreateSharing → AcceptSharing → DeleteSharing.
    /// </summary>
    [Fact]
    public async Task SharingLifecycle()
    {
        // Arrange
        var ownerChannel = await CreateAuthorizedChannelAsUser();
        var friendChannel = await CreateAuthorizedChannelAsAdmin();
        var owner = new SharingService.SharingServiceClient(ownerChannel);
        var friend = new SharingService.SharingServiceClient(friendChannel);
        var homeId = await ownerChannel.CreateHomeAsync();

        // CreateSharing + AcceptSharing
        var invitation = await owner.CreateSharingAsync(new CreateSharingRequest { HomeId = homeId, FriendEmail = AdminEmail, SharingType = "Manage" });

        Assert.False(string.IsNullOrEmpty(invitation.Password));

        await friend.AcceptSharingAsync(new AcceptSharingRequest { Password = invitation.Password });

        // GetSharings
        var sharings = await owner.GetSharingsAsync(new GetSharingsRequest { HomeId = homeId });
        var sharing = Assert.Single(sharings.Items, x => x.FriendEmail == AdminEmail);

        Assert.Equal("Manage", sharing.SharingType);

        // LeaveSharing
        await friend.LeaveSharingAsync(new LeaveSharingRequest { HomeId = homeId });

        // DeleteSharing
        var secondInvitation = await owner.CreateSharingAsync(new CreateSharingRequest { HomeId = homeId, FriendEmail = AdminEmail, SharingType = "Display" });
        await friend.AcceptSharingAsync(new AcceptSharingRequest { Password = secondInvitation.Password });

        var renewed = await owner.GetSharingsAsync(new GetSharingsRequest { HomeId = homeId });
        var renewedSharing = Assert.Single(renewed.Items, x => x.FriendEmail == AdminEmail);

        await owner.DeleteSharingAsync(new DeleteSharingRequest { HomeSharingId = renewedSharing.SharingId });
    }
}
