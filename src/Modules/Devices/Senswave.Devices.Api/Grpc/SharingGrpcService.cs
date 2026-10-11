using Grpc.Core;
using Senswave.Devices.Api.Sharings.DeleteSharing;
using Senswave.Devices.Api.Sharings.GetSharings;
using Senswave.Devices.Api.Sharings.SetSharing;
using Senswave.Devices.Application.ShareDevices.Features.GetDeviceSharings;
using Senswave.Infrastructure.Grpc.Errors;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Grpc;

[Authorize(AuthenticationSchemes = "Identity.Bearer")]
internal sealed class SharingGrpcService(IMediator mediator, IRequestContext requestContext) : Proto.SharingService.SharingServiceBase
{
    public override async Task<Proto.SetSharingResponse> SetSharing(Proto.SetSharingRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.SetSharingResponse();
    }

    public override async Task<Proto.GetSharingsResponse> GetSharings(Proto.GetSharingsRequest request, ServerCallContext context)
    {
        var query = new GetDeviceSharingsQuery
        {
            DeviceId = ProtoMapping.ParseGuid(request.DeviceId, "deviceId"),
            UserId = requestContext.UserId
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }

    public override async Task<Proto.DeleteSharingResponse> DeleteSharing(Proto.DeleteSharingRequest request, ServerCallContext context)
    {
        var command = ProtoMapping.ParseGuid(request.DeviceSharingId, "deviceSharingId").ToDeleteDeviceSharingsCommand(requestContext.UserId);

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DeleteSharingResponse();
    }
}
