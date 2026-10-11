using Senswave.Devices.Application.ShareDevices.Features.SetDeviceSharing;
using Senswave.Devices.Domain.ShareDevices.Extensions;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Sharings.SetSharing;

internal static class SetSharingGrpcExtensions
{
    internal static SetDeviceSharingsCommand ToCommand(this Proto.SetSharingRequest dto, Guid userId) => new()
    {
        UserId = userId,
        FriendEmail = dto.FriendEmail,
        DeviceId = ProtoMapping.ParseGuid(dto.DeviceId, "deviceId"),
        SharingType = dto.SharingType.ToDeviceSharingType()
    };
}
