using Senswave.Devices.Application.Dashboards.Features.CreateDashboard;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Dashboards.CreateDashboard;

internal static class CreateDashboardGrpcExtensions
{
    internal static CreateDashboardCommand ToCommand(this Proto.CreateDashboardRequest dto, Guid userId) => new()
    {
        UserId = userId,
        DeviceId = ProtoMapping.ParseGuid(dto.DeviceId, "deviceId"),
        Name = dto.Name,
        Icon = dto.Icon,
        Configuration = dto.Configuration.ToJsonObject("configuration")
    };
}
