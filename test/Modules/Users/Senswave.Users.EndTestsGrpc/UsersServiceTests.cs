using Senswave.Contracts.Users.V1;

namespace Senswave.Users.EndTestsGrpc;

[Trait("Collection", "EndTestGrpc")]
public class UsersServiceTests(GrpcTestEnvironment environment) : UsersGrpcFeatureTest(environment)
{
    /// <summary>
    /// GetUser → UpdateSettings → CreateConsents → GetUser (assert changed) → DeleteAccount.
    /// </summary>
    [Fact]
    public async Task UserLifecycle()
    {
        // Arrange
        var email = await RegisterConfirmedUserAsync();
        var channel = await CreateAuthorizedChannelWithoutConsent(email, Password);
        var client = new UsersService.UsersServiceClient(channel);

        // GetUser
        var user = await client.GetUserAsync(new GetUserRequest());

        Assert.Equal(email, user.Email);
        Assert.True(Guid.TryParse(user.Id, out _));
        Assert.False(user.HasActiveConsent);

        // UpdateSettings + CreateConsents
        await client.UpdateSettingsAsync(new UpdateSettingsRequest { Language = "pl", Theme = "dark" });
        await client.CreateConsentsAsync(new CreateConsentsRequest());

        var updated = await client.GetUserAsync(new GetUserRequest());

        Assert.Equal("pl", updated.Language);
        Assert.Equal("dark", updated.Theme, ignoreCase: true);
        Assert.True(updated.HasActiveConsent);

        // DeleteAccount
        await client.DeleteAccountAsync(new DeleteAccountRequest());
    }
}
