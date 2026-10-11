using Microsoft.AspNetCore.Authentication.BearerToken;
using Senswave.Users.Domain.Entity;

namespace Senswave.Users.Infrastructure.Tokens;

/// <summary>
/// Issues bearer tokens without <c>SignInManager.SignInAsync</c>, whose bearer handler writes JSON straight into the
/// HTTP response. Tokens are protected with the same protectors as the Identity API, so they are interchangeable with REST tokens.
/// </summary>
public interface IAccessTokenIssuer
{
    Task<AccessTokenResponse> IssueAsync(User user, CancellationToken cancellationToken);

    Task<Result<AccessTokenResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}
