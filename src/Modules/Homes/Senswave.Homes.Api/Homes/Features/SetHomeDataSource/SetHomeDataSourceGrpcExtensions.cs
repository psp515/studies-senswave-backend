using Senswave.Homes.Application.Homes.Features.AssignHomeDataSource;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.Api.Homes.Features.SetHomeDataSource;

internal static class SetHomeDataSourceGrpcExtensions
{
    internal static AssignHomeDataSourceCommand ToCommand(this Proto.SetHomeDataSourceRequest dto, Guid userId) => new()
    {
        UserId = userId,
        HomeId = ProtoMapping.ParseGuid(dto.HomeId, "homeId"),
        DataSourceId = ProtoMapping.ParseGuid(dto.BrokerId, "brokerId")
    };
}
