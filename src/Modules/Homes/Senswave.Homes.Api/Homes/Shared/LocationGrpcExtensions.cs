using Senswave.Homes.Domain.Homes.ValueObjects;
using Proto = Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.Api.Homes.Shared;

internal static class LocationGrpcExtensions
{
    /// <summary>REST always serialises a location (default 0/0), so the proto does too.</summary>
    internal static Proto.LocationDto ToProto(this Location? location) => new()
    {
        Longitude = location?.Longitude ?? 0,
        Latitude = location?.Latitude ?? 0
    };
}
