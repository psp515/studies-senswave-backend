using Senswave.Homes.Api.Homes.Shared;
using Senswave.Homes.Domain.Homes.Entities;
using Proto = Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.Api.Homes.Features.GetHomes;

internal static class GetHomesGrpcExtensions
{
    internal static Proto.GetHomesResponse ToProto(this IEnumerable<Home> homes, Guid userId) => new()
    {
        Items = { homes.Select(home => home.ToProto(userId)) }
    };

    private static Proto.HomeDto ToProto(this Home home, Guid userId)
    {
        var dto = new Proto.HomeDto
        {
            Id = home.Id.ToString(),
            Name = home.Name,
            Icon = home.Icon,
            IsOwner = home.OwnerId == userId,
            Location = home.Location.ToProto(),
        };

        if (home.DataSourceReference is not null)
            dto.DataSourceId = home.DataSourceReference.DataSourceId.ToString();

        return dto;
    }
}
