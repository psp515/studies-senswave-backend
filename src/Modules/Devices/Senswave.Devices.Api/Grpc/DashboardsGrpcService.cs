using Grpc.Core;
using Senswave.Contracts.Common.V1;
using Senswave.Devices.Api.Dashboards.CreateDashboard;
using Senswave.Devices.Api.Dashboards.DisplayDashboard;
using Senswave.Devices.Api.Dashboards.DisplayDashboards;
using Senswave.Devices.Api.Dashboards.GetDashboard;
using Senswave.Devices.Api.Dashboards.SetWidgetOnDashboard;
using Senswave.Devices.Api.Dashboards.UpdateDashboard;
using Senswave.Devices.Application.Dashboards.Features.DeleteDashboard;
using Senswave.Devices.Application.Dashboards.Features.DeleteDashboardWidget;
using Senswave.Devices.Application.Dashboards.Features.DisplayDashboard;
using Senswave.Devices.Application.Dashboards.Features.DisplayDashboards;
using Senswave.Devices.Application.Dashboards.Features.GetDashboard;
using Senswave.Infrastructure.Grpc.Errors;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Grpc;

[Authorize(AuthenticationSchemes = "Identity.Bearer")]
internal sealed class DashboardsGrpcService(IMediator mediator, IRequestContext requestContext) : Proto.DashboardsService.DashboardsServiceBase
{
    public override async Task<IdResponse> CreateDashboard(Proto.CreateDashboardRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new IdResponse { Id = result.Data.Id.ToString() };
    }

    public override async Task<Proto.UpdateDashboardResponse> UpdateDashboard(Proto.UpdateDashboardRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.UpdateDashboardResponse();
    }

    public override async Task<Proto.DeleteDashboardResponse> DeleteDashboard(Proto.DeleteDashboardRequest request, ServerCallContext context)
    {
        var command = new DeleteDashboardCommand
        {
            UserId = requestContext.UserId,
            DashboardId = ProtoMapping.ParseGuid(request.DashboardId, "dashboardId")
        };

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DeleteDashboardResponse();
    }

    public override async Task<Proto.GetDashboardResponse> GetDashboard(Proto.GetDashboardRequest request, ServerCallContext context)
    {
        var query = new GetDashboardQuery
        {
            UserId = requestContext.UserId,
            DashboardId = ProtoMapping.ParseGuid(request.DashboardId, "dashboardId")
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }

    public override async Task<Proto.DisplayDashboardResponse> DisplayDashboard(Proto.DisplayDashboardRequest request, ServerCallContext context)
    {
        var query = new DisplayDashboardQuery
        {
            UserId = requestContext.UserId,
            DashboardId = ProtoMapping.ParseGuid(request.DashboardId, "dashboardId")
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }

    public override async Task<Proto.DisplayDashboardsResponse> DisplayDashboards(Proto.DisplayDashboardsRequest request, ServerCallContext context)
    {
        var query = new DisplayDashboardsQuery
        {
            UserId = requestContext.UserId,
            DeviceId = ProtoMapping.ParseGuid(request.DeviceId, "deviceId")
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }

    public override async Task<Proto.SetWidgetOnDashboardResponse> SetWidgetOnDashboard(Proto.SetWidgetOnDashboardRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.SetWidgetOnDashboardResponse();
    }

    public override async Task<Proto.DeleteDashboardWidgetResponse> DeleteDashboardWidget(Proto.DeleteDashboardWidgetRequest request, ServerCallContext context)
    {
        var command = new DeleteDashboardWidgetCommand
        {
            UserId = requestContext.UserId,
            DashboardId = ProtoMapping.ParseGuid(request.DashboardId, "dashboardId"),
            WidgetId = ProtoMapping.ParseGuid(request.WidgetId, "widgetId")
        };

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DeleteDashboardWidgetResponse();
    }
}
