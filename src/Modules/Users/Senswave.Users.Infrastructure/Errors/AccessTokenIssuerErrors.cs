namespace Senswave.Users.Infrastructure.Errors;

public class AccessTokenIssuerErrors
{
    public readonly static Error InvalidRefreshToken = Error.Failure("InvalidRefreshToken", "The refresh token is invalid or expired.");
}
