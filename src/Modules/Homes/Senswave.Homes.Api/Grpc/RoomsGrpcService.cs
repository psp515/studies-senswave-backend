using Grpc.Core;
using Senswave.Contracts.Common.V1;
using Senswave.Homes.Api.Rooms.CreateRoom;
using Senswave.Homes.Api.Rooms.DisplayRooms;
using Senswave.Homes.Api.Rooms.UpdateRoom;
using Senswave.Homes.Application.Rooms.Features.DeleteRoom;
using Senswave.Homes.Application.Rooms.Features.DisplayRooms;
using Senswave.Homes.Application.Rooms.Features.GetRoom;
using Senswave.Infrastructure.Grpc.Errors;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.Api.Grpc;

[Authorize(AuthenticationSchemes = "Identity.Bearer")]
internal sealed class RoomsGrpcService(IMediator mediator, IRequestContext requestContext) : Proto.RoomsService.RoomsServiceBase
{
    public override async Task<IdResponse> CreateRoom(Proto.CreateRoomRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new IdResponse { Id = result.Data.Id.ToString() };
    }

    public override async Task<Proto.UpdateRoomResponse> UpdateRoom(Proto.UpdateRoomRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.UpdateRoomResponse();
    }

    public override async Task<Proto.DeleteRoomResponse> DeleteRoom(Proto.DeleteRoomRequest request, ServerCallContext context)
    {
        var command = new DeleteRoomCommand
        {
            UserId = requestContext.UserId,
            RoomId = ProtoMapping.ParseGuid(request.RoomId, "roomId"),
            HomeId = ProtoMapping.ParseGuid(request.HomeId, "homeId")
        };

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DeleteRoomResponse();
    }

    public override async Task<Proto.GetRoomResponse> GetRoom(Proto.GetRoomRequest request, ServerCallContext context)
    {
        var query = new GetRoomQuery
        {
            UserId = requestContext.UserId,
            RoomId = ProtoMapping.ParseGuid(request.RoomId, "roomId"),
            HomeId = ProtoMapping.ParseGuid(request.HomeId, "homeId")
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.GetRoomResponse { Id = result.Data.Id.ToString(), Name = result.Data.Name };
    }

    public override async Task<Proto.DisplayRoomsResponse> DisplayRooms(Proto.DisplayRoomsRequest request, ServerCallContext context)
    {
        var query = new DisplayRoomsQuery
        {
            UserId = requestContext.UserId,
            HomeId = ProtoMapping.ParseGuid(request.HomeId, "homeId")
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }
}
