using Senswave.Devices.Domain.Dashboards.Entities;
using Senswave.Devices.Domain.Dashboards.Extensions;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Dashboards.DisplayDashboards;

internal static class DisplayDashboardsGrpcExtensions
{
    internal static Proto.DisplayDashboardsResponse ToProto(this IEnumerable<Dashboard> dashboards) => new()
    {
        Items =
        {
            dashboards.Select(dashboard => new Proto.DashboardDto
            {
                Id = dashboard.Id.ToString(),
                Name = dashboard.Name,
                Icon = dashboard.Icon,
                Type = dashboard.Type.FromDashboardType()
            })
        }
    };
}
