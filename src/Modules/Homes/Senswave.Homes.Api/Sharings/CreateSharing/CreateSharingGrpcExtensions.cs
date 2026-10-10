using Senswave.Homes.Application.Sharings.Features.CreateSharing;
using Senswave.Homes.Domain.Sharings.Extensions;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Homes.V1;

namespace Senswave.Homes.Api.Sharings.CreateSharing;

internal static class CreateSharingGrpcExtensions
{
    internal static CreateSharingCommand ToCommand(this Proto.CreateSharingRequest dto, Guid ownerId) => new()
    {
        OwnerId = ownerId,
        HomeId = ProtoMapping.ParseGuid(dto.HomeId, "homeId"),
        FriendEmail = dto.FriendEmail,
        SharingType = dto.SharingType.ToHomeSharingType()
    };

    internal static Proto.CreateSharingResponse ToProto(this CreateSharingDto dto) => new()
    {
        InvitationId = dto.Id.ToString(),
        Password = dto.Password,
        CreatedUtc = dto.CreatedAtUtc.ToTimestamp(),
        ExpiresAtUtc = dto.ExpiresAtUtc.ToTimestamp()
    };
}
