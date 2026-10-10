using Senswave.Contracts.Users.V1;

namespace Senswave.Users.EndTestsGrpc;

[Trait("Collection", "EndTestGrpc")]
public class AuthServiceTests(GrpcTestEnvironment environment) : GrpcFeatureTest(environment)
{
    private const string Password = "Abecadło712576!!@#$%$";
    private const string NewPassword = "Zyxwvut712576!!@#$%$";

    /// <summary>
    /// Register → ConfirmEmail → Login → Refresh → ResendConfirmationEmail → ForgotPassword → ResetPassword → Login.
    /// </summary>
    [Fact]
    public async Task AuthLifecycle()
    {
        // Arrange
        var email = $"{Guid.NewGuid()}@test.com";
        var emails = new EmailCapture(Environment, email);
        var client = new AuthService.AuthServiceClient(CreateChannel());

        // Register + ConfirmEmail
        await client.RegisterAsync(new RegisterRequest { Email = email, Password = Password });
        var (userId, code) = emails.ConfirmationLinkQuery();
        await client.ConfirmEmailAsync(new ConfirmEmailRequest { UserId = userId, Code = code });

        // Login
        var login = await client.LoginAsync(new LoginRequest { Email = email, Password = Password });

        Assert.Equal("Bearer", login.TokenType);
        Assert.False(string.IsNullOrEmpty(login.AccessToken));
        Assert.False(string.IsNullOrEmpty(login.RefreshToken));
        Assert.True(login.ExpiresIn > 0);

        // Refresh
        var refreshed = await client.RefreshAsync(new RefreshRequest { RefreshToken = login.RefreshToken });

        Assert.False(string.IsNullOrEmpty(refreshed.AccessToken));
        Assert.False(string.IsNullOrEmpty(refreshed.RefreshToken));

        // ResendConfirmationEmail
        await client.ResendConfirmationEmailAsync(new ResendConfirmationEmailRequest { Email = email });

        // ForgotPassword + ResetPassword
        await client.ForgotPasswordAsync(new ForgotPasswordRequest { Email = email });
        var resetCode = emails.ResetCode();
        await client.ResetPasswordAsync(new ResetPasswordRequest { Email = email, ResetCode = resetCode, NewPassword = NewPassword });

        // Login with the new password
        var relogin = await client.LoginAsync(new LoginRequest { Email = email, Password = NewPassword });

        Assert.False(string.IsNullOrEmpty(relogin.AccessToken));
    }
}
