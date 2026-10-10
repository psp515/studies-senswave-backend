using Senswave.Homes.Domain.Rooms.Entities;
using Proto = Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.Api.Rooms.DisplayRooms;

internal static class DisplayRoomsGrpcExtensions
{
    internal static Proto.DisplayRoomsResponse ToProto(this IEnumerable<Room> rooms) => new()
    {
        Items = { rooms.Select(room => new Proto.RoomDto { Id = room.Id.ToString(), Name = room.Name }) }
    };
}
