using Senswave.Users.Application.Users.UpdateSettings;
using Senswave.Users.Domain.Extensions;
using Proto = Senswave.Contracts.Users.V1;

namespace Senswave.Users.Api.Users.UpdateSettings;

internal static class UpdateSettingsGrpcExtensions
{
    public static UpdateSettingsCommand ToCommand(this Proto.UpdateSettingsRequest dto, Guid user) => new()
    {
        UserId = user,
        Language = dto.Language.ToLanguageEnum(),
        Theme = dto.Theme.ToThemeEnum(),
    };
}
