using Senswave.Homes.Application.Homes.Features.CreateHome;
using Proto = Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.Api.Homes.Features.CreateHome;

internal static class CreateHomeGrpcExtensions
{
    internal static CreateHomeCommand ToCommand(this Proto.CreateHomeRequest dto, Guid userId)
    {
        var command = new CreateHomeCommand()
        {
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
