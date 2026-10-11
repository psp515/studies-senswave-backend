using Senswave.Devices.Domain.Devices.Enums;
using Senswave.Devices.Domain.Devices.Extensions;
using Senswave.Devices.Domain.Devices.Models;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Devices.DisplayDevice;

internal static class DisplayDeviceGrpcExtensions
{
    internal static Proto.DisplayDeviceDto ToProto(this DisplayDeviceModel model)
    {
        var dto = new Proto.DisplayDeviceDto
        {
            Id = model.Id.ToString(),
            Icon = model.Icon,
            Name = model.Name,
            Tile = new Proto.DeviceTileDto
            {
                Type = model.Tile.Type.FromDeviceTileType(),
                Value = model.Tile.Value.ToProtoValue(),
                Configuration = model.Tile.Configuration?.ToStruct(),
            },
            Presence = new Proto.DevicePresenceDto
            {
                Type = model.Presence.Type.FromDevicePresenceType(),
            },
            CreatedAtUtc = model.CreatedAtUtc.ToTimestamp(),
            UpdatedAtUtc = model.UpdatedAtUtc.ToTimestamp(),
        };

        if (model.RoomId.HasValue)
            dto.RoomId = model.RoomId.Value.ToString();

        if (model.Presence.Type == DevicePresenceType.BooleanOperation)
        {
            if (model.Presence.Value.HasValue)
                dto.Presence.Value = model.Presence.Value.Value;

            dto.Presence.LastSeenAtUtc = model.Presence.LastSeenAtUtc.ToTimestamp();
        }

        return dto;
    }
}
