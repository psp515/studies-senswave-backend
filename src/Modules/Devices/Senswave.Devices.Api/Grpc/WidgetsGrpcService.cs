using Grpc.Core;
using Senswave.Contracts.Common.V1;
using Senswave.Devices.Api.Widgets.Features.CreateWidget;
using Senswave.Devices.Api.Widgets.Features.DeleteWidget;
using Senswave.Devices.Api.Widgets.Features.DisplayWidgets;
using Senswave.Devices.Api.Widgets.Features.GetWidget;
using Senswave.Devices.Application.Widgets.Features.Action;
using Senswave.Devices.Application.Widgets.Features.DisplayWidgets;
using Senswave.Devices.Application.Widgets.Features.GetWidget;
using Senswave.Devices.Application.Widgets.Features.State;
using Senswave.Infrastructure.Grpc.Errors;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Grpc;

[Authorize(AuthenticationSchemes = "Identity.Bearer")]
internal sealed class WidgetsGrpcService(IMediator mediator, IRequestContext requestContext) : Proto.WidgetsService.WidgetsServiceBase
{
    public override async Task<IdResponse> CreateWidget(Proto.CreateWidgetRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new IdResponse { Id = result.Data.Id.ToString() };
    }

    public override async Task<Proto.DeleteWidgetResponse> DeleteWidget(Proto.DeleteWidgetRequest request, ServerCallContext context)
    {
        var command = ProtoMapping.ParseGuid(request.WidgetId, "widgetId").ToDeleteWidgetCommand(requestContext.UserId);

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DeleteWidgetResponse();
    }

    public override async Task<Proto.GetWidgetResponse> GetWidget(Proto.GetWidgetRequest request, ServerCallContext context)
    {
        var query = new GetWidgetQuery
        {
            UserId = requestContext.UserId,
            WidgetId = ProtoMapping.ParseGuid(request.WidgetId, "widgetId")
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }

    public override async Task<Proto.DisplayWidgetsResponse> DisplayWidgets(Proto.DisplayWidgetsRequest request, ServerCallContext context)
    {
        var query = new DisplayWidgetsQuery
        {
            UserId = requestContext.UserId,
            DeviceId = ProtoMapping.ParseGuid(request.DeviceId, "deviceId")
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }

    public override async Task<Proto.ActionResponse> Action(Proto.ActionRequest request, ServerCallContext context)
    {
        var command = new WidgetActionCommand
        {
            WidgetId = ProtoMapping.ParseGuid(request.WidgetId, "widgetId"),
            UserId = requestContext.UserId,
            Value = request.Value.ToJsonValue("value")
        };

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.ActionResponse();
    }

    public override async Task<Proto.StateResponse> State(Proto.StateRequest request, ServerCallContext context)
    {
        var command = new StateCommand
        {
            Enabled = request.Enabled,
            UserId = requestContext.UserId,
            WidgetId = ProtoMapping.ParseGuid(request.WidgetId, "widgetId")
        };

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.StateResponse();
    }
}
