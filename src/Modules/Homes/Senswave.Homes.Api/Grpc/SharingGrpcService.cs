using Grpc.Core;
using Senswave.Homes.Api.Sharings.CreateSharing;
using Senswave.Homes.Api.Sharings.DeleteSharing;
using Senswave.Homes.Api.Sharings.GetSharings;
using Senswave.Homes.Application.Sharings.Features.AcceptSharing;
using Senswave.Homes.Application.Sharings.Features.GetSharings;
using Senswave.Homes.Application.Sharings.Features.LeaveSharing;
using Senswave.Infrastructure.Grpc.Errors;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.Api.Grpc;

[Authorize(AuthenticationSchemes = "Identity.Bearer")]
internal sealed class SharingGrpcService(IMediator mediator, IRequestContext requestContext) : Proto.SharingService.SharingServiceBase
{
    public override async Task<Proto.CreateSharingResponse> CreateSharing(Proto.CreateSharingRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }

    public override async Task<Proto.AcceptSharingResponse> AcceptSharing(Proto.AcceptSharingRequest request, ServerCallContext context)
    {
        var command = new AcceptSharingCommand
        {
            UserId = requestContext.UserId,
            Password = request.Password
        };

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.AcceptSharingResponse();
    }

    public override async Task<Proto.GetSharingsResponse> GetSharings(Proto.GetSharingsRequest request, ServerCallContext context)
    {
        var query = new GetSharingsQuery
        {
            HomeId = ProtoMapping.ParseGuid(request.HomeId, "homeId"),
            UserId = requestContext.UserId
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }

    public override async Task<Proto.DeleteSharingResponse> DeleteSharing(Proto.DeleteSharingRequest request, ServerCallContext context)
    {
        var command = ProtoMapping.ParseGuid(request.HomeSharingId, "homeSharingId").ToDeleteSharingCommand(requestContext.UserId);

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DeleteSharingResponse();
    }

    public override async Task<Proto.LeaveSharingResponse> LeaveSharing(Proto.LeaveSharingRequest request, ServerCallContext context)
    {
        var command = new LeaveSharingCommand
        {
            HomeId = ProtoMapping.ParseGuid(request.HomeId, "homeId"),
            UserId = requestContext.UserId
        };

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.LeaveSharingResponse();
    }
}
