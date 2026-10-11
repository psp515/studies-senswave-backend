using Senswave.Homes.Application.Sharings.Features.GetSharings;
using Senswave.Homes.Domain.Sharings.Extensions;
using Proto = Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.Api.Sharings.GetSharings;

internal static class GetSharingsGrpcExtensions
{
    internal static Proto.GetSharingsResponse ToProto(this IEnumerable<SharingModel> sharings) => new()
    {
        Items =
        {
            sharings.Select(model => new Proto.SharingDto
            {
                SharingId = model.SharingId.ToString(),
                SharingType = model.SharingType.FromHomeSharingType(),
                FriendEmail = model.FriendEmail
            })
        }
    };
}
