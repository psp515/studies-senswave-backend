using Senswave.Homes.Application.Homes.Features.UpdateHome;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.Api.Homes.Features.UpdateHome;

internal static class UpdateHomeGrpcExtensions
{
    internal static UpdateHomeCommand ToCommand(this Proto.UpdateHomeRequest dto, Guid userId)
    {
        var command = new UpdateHomeCommand()
        {
            HomeId = ProtoMapping.ParseGuid(dto.HomeId, "homeId"),
            UserId = userId,
            Name = dto.Name,
            Icon = dto.Icon,
        };

        if (dto.HasLatitude)
            command.Latitude = dto.Latitude;

        if (dto.HasLongitude)
            command.Longitude = dto.Longitude;

        return command;
    }
}
