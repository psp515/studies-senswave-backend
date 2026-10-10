using Senswave.Homes.Application.Rooms.Features.CreateRoom;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.Api.Rooms.CreateRoom;

internal static class CreateRoomGrpcExtensions
{
    internal static CreateRoomCommand ToCommand(this Proto.CreateRoomRequest dto, Guid userId) => new()
    {
        HomeId = ProtoMapping.ParseGuid(dto.HomeId, "homeId"),
        Name = dto.Name,
        UserId = userId
    };
}
