using Senswave.Contracts.Users.V1;

namespace Senswave.Users.EndTestsGrpc;

[Trait("Collection", "EndTestGrpc")]
public class AccountServiceTests(GrpcTestEnvironment environment) : UsersGrpcFeatureTest(environment)
{
    private const string NewPassword = "Zyxwvut712576!!@#$%$";

    /// <summary>
    /// GetInfo → UpdateInfo (password change) → TwoFactor (status query).
    /// </summary>
    [Fact]
    public async Task AccountLifecycle()
    {
        // Arrange
        var email = await RegisterConfirmedUserAsync();
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
