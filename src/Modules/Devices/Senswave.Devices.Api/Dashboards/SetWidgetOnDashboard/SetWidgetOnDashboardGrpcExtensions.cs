using Senswave.Devices.Application.Dashboards.Features.SetWidgetOnDashboard;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Dashboards.SetWidgetOnDashboard;

internal static class SetWidgetOnDashboardGrpcExtensions
{
    internal static SetWidgetOnDashboardCommand ToCommand(this Proto.SetWidgetOnDashboardRequest dto, Guid userId) => new()
    {
        UserId = userId,
        DashboardId = ProtoMapping.ParseGuid(dto.DashboardId, "dashboardId"),
        WidgetId = ProtoMapping.ParseGuid(dto.WidgetId, "widgetId"),
        Row = dto.Row,
        RowSpan = dto.RowSpan,
        Column = dto.Column,
        ColumnSpan = dto.ColumnSpan,
    };
}
