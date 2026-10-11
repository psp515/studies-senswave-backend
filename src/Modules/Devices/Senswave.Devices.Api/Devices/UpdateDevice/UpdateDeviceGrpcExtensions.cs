using Senswave.Devices.Application.Devices.Features.UpdateDevice;
using Senswave.Devices.Domain.Devices.Extensions;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Devices.UpdateDevice;

internal static class UpdateDeviceGrpcExtensions
{
    internal static UpdateDeviceCommand ToCommand(this Proto.UpdateDeviceRequest dto, Guid userId) => new()
    {
        DeviceId = ProtoMapping.ParseGuid(dto.DeviceId, "deviceId"),
        UserId = userId,
        // Unset mirrors an omitted REST field (Guid.Empty); an empty string mirrors JSON null.
        RoomId = !dto.HasRoomId ? Guid.Empty : ProtoMapping.ParseOptionalGuid(dto.RoomId, "roomId"),
        Icon = dto.Icon,
        Name = dto.Name,
        TileOperationId = dto.HasOperationId ? ProtoMapping.ParseOptionalGuid(dto.OperationId, "operationId") : null,
        TileDisplayableOperationId = dto.HasDisplayableOperationId ? ProtoMapping.ParseOptionalGuid(dto.DisplayableOperationId, "displayableOperationId") : null,
        TileConfiguration = dto.Configuration is null ? null : dto.Configuration.ToJsonObject("configuration"),
        TileType = dto.Type.ToDeviceTileType(),
        PresenceOperationId = ProtoMapping.ParseGuidOrEmpty(dto.PresenceOperationId, "presenceOperationId"),
        PresenceType = dto.PresenceType.ToDevicePresenceType()
    };
}
