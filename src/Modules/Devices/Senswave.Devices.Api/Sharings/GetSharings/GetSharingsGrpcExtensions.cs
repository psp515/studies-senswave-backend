using Senswave.Devices.Application.ShareDevices.Features.GetDeviceSharings;
using Senswave.Devices.Domain.ShareDevices.Extensions;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Sharings.GetSharings;

internal static class GetSharingsGrpcExtensions
{
    internal static Proto.GetSharingsResponse ToProto(this IEnumerable<DeviceSharingModel> sharings) => new()
    {
        Items = { sharings.Select(ToProto) }
    };

    private static Proto.SharingDto ToProto(DeviceSharingModel model)
    {
        var dto = new Proto.SharingDto
        {
            FriendEmail = model.FriendEmail,
            SharingType = model.SharingType.FromDeviceSharingType()
        };

        if (model.SharingId.HasValue)
            dto.SharingId = model.SharingId.Value.ToString();

        return dto;
    }
}
