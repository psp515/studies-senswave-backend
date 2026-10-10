using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Senswave.Users.Domain.Entity;
using Senswave.Users.Infrastructure.Errors;
using System.Security.Claims;

namespace Senswave.Users.Infrastructure.Tokens;

/// <summary>
/// Mirrors <c>BearerTokenHandler.HandleSignInAsync</c> and the Identity API <c>/refresh</c> endpoint.
/// </summary>
internal sealed class AccessTokenIssuer(
    SignInManager<User> signInManager,
    IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
    TimeProvider timeProvider) : IAccessTokenIssuer
{
    private static readonly string Scheme = IdentityConstants.BearerScheme;

    public async Task<AccessTokenResponse> IssueAsync(User user, CancellationToken cancellationToken)
    {
        var principal = await signInManager.CreateUserPrincipalAsync(user);

        return Issue(principal);
    }

    public async Task<Result<AccessTokenResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var options = bearerTokenOptions.Get(Scheme);

        var refreshTicket = options.RefreshTokenProtector.Unprotect(refreshToken);

        if (refreshTicket?.Properties?.ExpiresUtc is not { } expiresUtc ||
            timeProvider.GetUtcNow() >= expiresUtc ||
            await signInManager.ValidateSecurityStampAsync(refreshTicket.Principal) is not { } user)
        {
            return Result<AccessTokenResponse>.Failure(AccessTokenIssuerErrors.InvalidRefreshToken);
        }

        var principal = await signInManager.CreateUserPrincipalAsync(user);

        return Result<AccessTokenResponse>.Success(Issue(principal));
    }

    private AccessTokenResponse Issue(ClaimsPrincipal principal)
    {
        var options = bearerTokenOptions.Get(Scheme);
        var utcNow = timeProvider.GetUtcNow();

        var bearerProperties = new AuthenticationProperties
        {
            ExpiresUtc = utcNow + options.BearerTokenExpiration
        };

        var refreshProperties = new AuthenticationProperties
        {
            ExpiresUtc = utcNow + options.RefreshTokenExpiration
        };

        return new AccessTokenResponse
        {
            AccessToken = options.BearerTokenProtector.Protect(
                new AuthenticationTicket(principal, bearerProperties, $"{Scheme}:AccessToken")),
            ExpiresIn = (long)options.BearerTokenExpiration.TotalSeconds,
            RefreshToken = options.RefreshTokenProtector.Protect(
                new AuthenticationTicket(principal, refreshProperties, $"{Scheme}:RefreshToken")),
        };
    }
}
