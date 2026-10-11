using Senswave.TestInfrastructure.TestEnvironments.Limit;
using System.Net;

namespace Senswave.Homes.EndTests.Limits.Homes;

[Collection("Limits E2E Tests Collection")]
[Trait("Collection", "LimitsEndTests")]
public class PostHomes(LimitTestEnvironment factory) : LimitFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task LimitationWorks()
    {
        // Arrange
        var client = CreateClient();
        var limit = LimitTestEnvironment.HomeOptions.Limits.Homes;

        // Act
        await CleanHomes();
        await AuthorizeClientAsUser(client);

        for (int i = 0; i < limit; i++)
        {
            await PostHome(76, 51);
        }

        var response = await PostHomeNoChecks();

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
