using Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.EndTestsGrpcWeb;

[Trait("Collection", "EndTestGrpcWeb")]
public class HomesGrpcWebTests(GrpcWebTestEnvironment environment) : GrpcWebFeatureTest(environment)
{
    /// <summary>
    /// HomesService: CreateHome → GetHome.
    /// </summary>
    [Fact]
    public async Task CreateThenGetHome()
    {
        // Arrange
        var channel = await CreateAuthorizedChannelAsUser();
        var client = new HomesService.HomesServiceClient(channel);

        // CreateHome
        var created = await client.CreateHomeAsync(new CreateHomeRequest { Name = "My home", Icon = "home", Latitude = 51, Longitude = 17 });

        // GetHome
        var home = await client.GetHomeAsync(new GetHomeRequest { HomeId = created.Id });

        Assert.Equal("My home", home.Name);
        Assert.True(home.IsOwner);
    }

    /// <summary>
    /// RoomsService: CreateRoom → GetRoom.
    /// </summary>
    [Fact]
    public async Task CreateThenGetRoom()
    {
        // Arrange
        var channel = await CreateAuthorizedChannelAsUser();
        var client = new RoomsService.RoomsServiceClient(channel);
        var homeId = await channel.CreateHomeAsync();

        // CreateRoom
        var created = await client.CreateRoomAsync(new CreateRoomRequest { HomeId = homeId, Name = "Kitchen" });

        // GetRoom
        var room = await client.GetRoomAsync(new GetRoomRequest { HomeId = homeId, RoomId = created.Id });

        Assert.Equal("Kitchen", room.Name);
    }

    /// <summary>
    /// SharingService: CreateSharing → AcceptSharing → GetSharings.
    /// </summary>
    [Fact]
    public async Task ShareThenGetSharings()
    {
        // Arrange
        var ownerChannel = await CreateAuthorizedChannelAsUser();
        var owner = new SharingService.SharingServiceClient(ownerChannel);
        var friend = new SharingService.SharingServiceClient(await CreateAuthorizedChannelAsAdmin());
        var homeId = await ownerChannel.CreateHomeAsync();

        // CreateSharing + AcceptSharing
        var invitation = await owner.CreateSharingAsync(new CreateSharingRequest { HomeId = homeId, FriendEmail = AdminEmail, SharingType = "Display" });
        await friend.AcceptSharingAsync(new AcceptSharingRequest { Password = invitation.Password });

        // GetSharings
        var sharings = await owner.GetSharingsAsync(new GetSharingsRequest { HomeId = homeId });

        Assert.Equal("Display", Assert.Single(sharings.Items, x => x.FriendEmail == AdminEmail).SharingType);
    }
}
