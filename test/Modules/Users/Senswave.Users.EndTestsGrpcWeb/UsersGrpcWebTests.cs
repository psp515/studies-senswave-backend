using Senswave.Contracts.Users.V1;

namespace Senswave.Users.EndTestsGrpcWeb;

[Trait("Collection", "EndTestGrpcWeb")]
public class UsersGrpcWebTests(GrpcWebTestEnvironment environment) : GrpcWebFeatureTest(environment)
{
    /// <summary>
    /// AuthService.Login → UsersService.GetUser with the issued token: token issuance and bearer metadata over gRPC-Web.
    /// </summary>
    [Fact]
    public async Task LoginThenGetUser()
    {
        // Login
        var token = await LoginAsync(UserEmail, UserPassword);

        Assert.Equal("Bearer", token.TokenType);

        // GetUser
        var client = new UsersService.UsersServiceClient(CreateChannel(token.AccessToken));
        var user = await client.GetUserAsync(new GetUserRequest());

        Assert.Equal(UserEmail, user.Email);
    }

    /// <summary>
    /// LegalService.GetPrivacyPolicy, anonymously.
    /// </summary>
    [Fact]
    public async Task AnyoneCanReadPrivacyPolicy()
    {
        var client = new LegalService.LegalServiceClient(CreateChannel());

        var privacyPolicy = await client.GetPrivacyPolicyAsync(new GetPrivacyPolicyRequest());

        Assert.Contains("# Privacy Policy", privacyPolicy.Content);
    }
}
