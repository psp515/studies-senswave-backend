using Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.EndTestsGrpc;

[Trait("Collection", "EndTestGrpc")]
public class RoomsServiceTests(GrpcTestEnvironment environment) : GrpcFeatureTest(environment)
{
    /// <summary>
    /// CreateRoom → GetRoom → DisplayRooms → UpdateRoom → DeleteRoom.
    /// </summary>
    [Fact]
    public async Task RoomLifecycle()
    {
        // Arrange
        var channel = await CreateAuthorizedChannelAsUser();
        var client = new RoomsService.RoomsServiceClient(channel);
        var homeId = await channel.CreateHomeAsync();

        // CreateRoom
        var created = await client.CreateRoomAsync(new CreateRoomRequest { HomeId = homeId, Name = "Kitchen" });
        var roomId = created.Id;

        // GetRoom + DisplayRooms
        var room = await client.GetRoomAsync(new GetRoomRequest { HomeId = homeId, RoomId = roomId });
        var rooms = await client.DisplayRoomsAsync(new DisplayRoomsRequest { HomeId = homeId });

        Assert.Equal("Kitchen", room.Name);
        Assert.Contains(rooms.Items, x => x.Id == roomId);

        // UpdateRoom
        await client.UpdateRoomAsync(new UpdateRoomRequest { HomeId = homeId, RoomId = roomId, Name = "Living room" });
        var updated = await client.GetRoomAsync(new GetRoomRequest { HomeId = homeId, RoomId = roomId });

        Assert.Equal("Living room", updated.Name);

        // DeleteRoom
        await client.DeleteRoomAsync(new DeleteRoomRequest { HomeId = homeId, RoomId = roomId });
    }
}
