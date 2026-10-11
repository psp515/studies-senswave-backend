using Senswave.Devices.Domain.Dashboards.Extensions;
using Senswave.Devices.Domain.Dashboards.Models;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Dashboards.DisplayDashboard;

internal static class DisplayDashboardGrpcExtensions
{
    internal static Proto.DisplayDashboardResponse ToProto(this DisplayDashboardModel model) => new()
    {
        Type = model.Type.FromDashboardType(),
        Configuration = model.Configuration.ToStruct()
    };
}
