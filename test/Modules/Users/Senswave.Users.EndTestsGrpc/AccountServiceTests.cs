using Senswave.Contracts.Users.V1;

namespace Senswave.Users.EndTestsGrpc;

[Trait("Collection", "EndTestGrpc")]
public class AccountServiceTests(GrpcTestEnvironment environment) : GrpcFeatureTest(environment)
{
    private const string Password = "Abecadło712576!!@#$%$";
    private const string NewPassword = "Zyxwvut712576!!@#$%$";

    /// <summary>
    /// GetInfo → UpdateInfo (password change) → TwoFactor (status query).
    /// </summary>
    [Fact]
    public async Task AccountLifecycle()
    {
        // Arrange: a fresh confirmed user, so the seeded users stay untouched.
        var email = $"{Guid.NewGuid()}@test.com";
        var emails = new EmailCapture(Environment, email);
        var auth = new AuthService.AuthServiceClient(CreateChannel());

        await auth.RegisterAsync(new RegisterRequest { Email = email, Password = Password });
        var (userId, code) = emails.ConfirmationLinkQuery();
        await auth.ConfirmEmailAsync(new ConfirmEmailRequest { UserId = userId, Code = code });

        var channel = await CreateAuthorizedChannelWithoutConsent(email, Password);
        var client = new AccountService.AccountServiceClient(channel);

        // GetInfo
        var info = await client.GetInfoAsync(new GetInfoRequest());

        Assert.Equal(email, info.Email);
        Assert.True(info.IsEmailConfirmed);

        // UpdateInfo
        var updated = await client.UpdateInfoAsync(new UpdateInfoRequest { OldPassword = Password, NewPassword = NewPassword });

        Assert.Equal(email, updated.Email);
        await LoginAsync(email, NewPassword);

        // TwoFactor
        var twoFactor = await client.TwoFactorAsync(new TwoFactorRequest());

        Assert.False(string.IsNullOrEmpty(twoFactor.SharedKey));
        Assert.False(twoFactor.IsTwoFactorEnabled);
    }
}
