using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.Users.Domain.Repositories;
using Senswave.Users.Infrastructure;

namespace Senswave.Users.EndTests.Base.Users;

[Trait("Collection", "EndTest")]
public class CreateConsentsTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    private static string Path => $"{Paths.UsersPath}/consents";

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var response = await client.PostAsync(Path, null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MakeConsents()
    {
        // Arrange
        var (client, email, _) = await CreateClientWithRoleWithoutConsent();
        using var scope = Factory.Server.Services.CreateScope();
        var legalRepository = scope.ServiceProvider.GetRequiredService<IQueryLegalRepository>();
        var context = scope.ServiceProvider.GetRequiredService<UsersContext>();

        // Act
        var lastTerms = await legalRepository.GetLatestTermsAndConditions(default);
        var lastPrivacy = await legalRepository.GetLatestPrivacyPolicy(default);

        var hasNoConsentsBefore = await context.Users.Where(x => x.Email == email)
            .SelectMany(x => x.UserConsents)
            .Where(x => x.TermsAndConditionsId == lastTerms.Id && x.PrivacyPolicyId == lastPrivacy.Id)
            .AnyAsync();

        var response = await client.PostAsync(Path, null);

        var hasConsentsAfter = await context.Users.Where(x => x.Email == email)
            .SelectMany(x => x.UserConsents)
            .Where(x => x.TermsAndConditionsId == lastTerms.Id && x.PrivacyPolicyId == lastPrivacy.Id)
            .AnyAsync();

        // Assert
        Assert.False(hasNoConsentsBefore, email);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(hasConsentsAfter);
    }

    [Fact]
    public async Task DuplicateConsentsIsOk()
    {
        // Arrange
        var (client, _, _) = await CreateClientWithConsent();

        // Act
        var response = await client.PostAsync(Path, null);
        var duplicateResponse = await client.PostAsync(Path, null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, duplicateResponse.StatusCode);
    }
}
