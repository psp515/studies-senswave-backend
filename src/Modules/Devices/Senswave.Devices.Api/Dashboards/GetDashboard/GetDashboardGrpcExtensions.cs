using Senswave.Devices.Domain.Dashboards.Entities;
using Senswave.Devices.Domain.Dashboards.Extensions;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Dashboards.GetDashboard;

internal static class GetDashboardGrpcExtensions
{
    internal static Proto.GetDashboardResponse ToProto(this Dashboard dashboard) => new()
    {
        Id = dashboard.Id.ToString(),
        Name = dashboard.Name,
        Icon = dashboard.Icon,
        Configuration = dashboard.Configuration.ToStruct(),
        Type = dashboard.Type.FromDashboardType()
    };
}
