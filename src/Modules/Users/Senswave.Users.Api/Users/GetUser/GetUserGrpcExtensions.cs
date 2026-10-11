using Senswave.Users.Application.Users.GetUser;
using Senswave.Users.Domain.Extensions;
using Proto = Senswave.Contracts.Users.V1;

namespace Senswave.Users.Api.Users.GetUser;

internal static class GetUserGrpcExtensions
{
    public static Proto.GetUserResponse ToProto(this UserDto dto) => new()
    {
        Id = dto.User.Id.ToString(),
        Email = dto.User.Email!,
        Language = dto.User.Language.ToLanguageString(),
        Theme = dto.User.Theme.ToThemeString(),
        HasActiveConsent = dto.HasActiveConsent
    };
}
