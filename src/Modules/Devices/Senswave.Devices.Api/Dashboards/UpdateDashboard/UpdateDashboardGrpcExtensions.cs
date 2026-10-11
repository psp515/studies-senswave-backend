using Senswave.Devices.Application.Dashboards.Features.UpdateDashboard;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Dashboards.UpdateDashboard;

internal static class UpdateDashboardGrpcExtensions
{
    internal static UpdateDashboardCommand ToCommand(this Proto.UpdateDashboardRequest dto, Guid userId) => new()
    {
        DashboardId = ProtoMapping.ParseGuid(dto.DashboardId, "dashboardId"),
        UserId = userId,
        Name = dto.Name,
        Icon = dto.Icon,
    };
}
