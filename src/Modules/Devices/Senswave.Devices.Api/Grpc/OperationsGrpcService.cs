using Grpc.Core;
using Senswave.Contracts.Common.V1;
using Senswave.Devices.Api.Operations.CreateOperation;
using Senswave.Devices.Api.Operations.DeleteOperation;
using Senswave.Devices.Api.Operations.DisplayOperations;
using Senswave.Devices.Api.Operations.GetOperation;
using Senswave.Devices.Application.Operations.Features.DisplayOperations;
using Senswave.Devices.Application.Operations.Features.GetOperation;
using Senswave.Infrastructure.Grpc.Errors;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Grpc;

[Authorize(AuthenticationSchemes = "Identity.Bearer")]
internal sealed class OperationsGrpcService(IMediator mediator, IRequestContext requestContext) : Proto.OperationsService.OperationsServiceBase
{
    public override async Task<IdResponse> CreateOperation(Proto.CreateOperationRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new IdResponse { Id = result.Data.Id.ToString() };
    }

    public override async Task<Proto.DeleteOperationResponse> DeleteOperation(Proto.DeleteOperationRequest request, ServerCallContext context)
    {
        var command = ProtoMapping.ParseGuid(request.OperationId, "operationId").ToDeleteOperationCommand(requestContext.UserId);

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DeleteOperationResponse();
    }

    public override async Task<Proto.GetOperationResponse> GetOperation(Proto.GetOperationRequest request, ServerCallContext context)
    {
        var query = new GetOperationQuery
        {
            OperationId = ProtoMapping.ParseGuid(request.OperationId, "operationId"),
            UserId = requestContext.UserId
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }

    public override async Task<Proto.DisplayOperationsResponse> DisplayOperations(Proto.DisplayOperationsRequest request, ServerCallContext context)
    {
        var query = new DisplayOperationsQuery
        {
            UserId = requestContext.UserId,
            DeviceId = ProtoMapping.ParseGuid(request.DeviceId, "deviceId"),
            Page = request.Page.OrDefault(1),
            Size = request.Size.OrDefault(10)
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }
}
