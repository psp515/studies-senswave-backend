using Senswave.Devices.Application.Devices.Features.CreateDevice;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Devices.CreateDevice;

internal static class CreateDeviceGrpcExtensions
{
    internal static CreateDeviceCommand ToCommand(this Proto.CreateDeviceRequest dto, Guid userId) => new()
    {
        HomeId = ProtoMapping.ParseGuid(dto.HomeId, "homeId"),
        RoomId = dto.ToRoomId(),
        Icon = dto.Icon,
        Name = dto.Name,
        UserId = userId,
    };

    /// <summary>Unset mirrors an omitted REST field (<see cref="Guid.Empty"/>); an empty string mirrors JSON <c>null</c>.</summary>
    private static Guid? ToRoomId(this Proto.CreateDeviceRequest dto) =>
        !dto.HasRoomId ? Guid.Empty : ProtoMapping.ParseOptionalGuid(dto.RoomId, "roomId");
}
