using Senswave.Homes.Application.Rooms.Features.UpdateRoom;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.Api.Rooms.UpdateRoom;

internal static class UpdateRoomGrpcExtensions
{
    /// <summary>As in REST, the room id alone identifies the room; <c>home_id</c> is not part of the command.</summary>
    internal static UpdateRoomCommand ToCommand(this Proto.UpdateRoomRequest dto, Guid userId) => new()
    {
        Name = dto.Name,
        RoomId = ProtoMapping.ParseGuid(dto.RoomId, "roomId"),
        UserId = userId
    };
}
