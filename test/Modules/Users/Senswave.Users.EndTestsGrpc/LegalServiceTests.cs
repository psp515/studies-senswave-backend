using Senswave.Contracts.Users.V1;

namespace Senswave.Users.EndTestsGrpc;

[Trait("Collection", "EndTestGrpc")]
public class LegalServiceTests(GrpcTestEnvironment environment) : GrpcFeatureTest(environment)
{
    /// <summary>
    /// GetPrivacyPolicy → GetTermsAndConditions, anonymously.
    /// </summary>
    [Fact]
    public async Task AnyoneCanReadLegalDocuments()
    {
        // Arrange
        var client = new LegalService.LegalServiceClient(CreateChannel());

        // GetPrivacyPolicy
        var privacyPolicy = await client.GetPrivacyPolicyAsync(new GetPrivacyPolicyRequest());

        Assert.Contains("# Privacy Policy", privacyPolicy.Content);
        Assert.False(string.IsNullOrWhiteSpace(privacyPolicy.Version));
        Assert.True(Guid.TryParse(privacyPolicy.Id, out _));
        Assert.NotNull(privacyPolicy.CreatedAtUtc);

        // GetTermsAndConditions
        var terms = await client.GetTermsAndConditionsAsync(new GetTermsAndConditionsRequest());

        Assert.Contains("# Terms and Conditions", terms.Content);
        Assert.False(string.IsNullOrWhiteSpace(terms.Version));
    }
}
