using Senswave.Devices.Domain.Devices.Entities;
using Senswave.Devices.Domain.Devices.Enums;
using Senswave.Devices.Domain.Devices.Extensions;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Devices.GetDevice;

internal static class GetDeviceGrpcExtensions
{
    internal static Proto.GetDeviceResponse ToProto(this Device device)
    {
        var response = new Proto.GetDeviceResponse
        {
            Id = device.Id.ToString(),
            Icon = device.Icon,
            Name = device.Name,
            Tile = new Proto.GetDeviceTileDto
            {
                Type = device.Tile.Type.FromDeviceTileType(),
                Configuration = device.Tile.Configuration?.ToStruct(),
            },
            Presence = new Proto.GetDevicePresenceDto
            {
                Type = (device.Presence?.Type ?? DevicePresenceType.Default).FromDevicePresenceType(),
            },
        };

        if (device.RoomReferenceId.HasValue)
            response.RoomId = device.RoomReferenceId.Value.ToString();

        if (device.Tile.SwitchOperationId.HasValue)
            response.Tile.OperationId = device.Tile.SwitchOperationId.Value.ToString();

        if (device.Tile.DisplayableOperationId.HasValue)
            response.Tile.DisplayableOperationId = device.Tile.DisplayableOperationId.Value.ToString();

        if (device.Presence?.OperationId is { } presenceOperationId)
            response.Presence.OperationId = presenceOperationId.ToString();

        return response;
    }
}
