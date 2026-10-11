using Senswave.TestInfrastructure.TestEnvironments.Limit;

namespace Senswave.Automations.EndTests.Limits;

[Collection("Limits E2E Tests Collection")]
[Trait("Collection", "LimitsEndTests")]
public class PostAutomations(LimitTestEnvironment factory) : LimitFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task LimitationWorks()
    {
        // Arrange
        await CleanAutomations();
        var client = CreateClient();
        var home = await PostHomeWithBroker();
        var limits = LimitTestEnvironment.AutomationOptions.Limits.AutomationsPerHome;

        // Act
        for (var i = 0; i < limits; i++)
        {
            await PostAutomation(home);
        }

        await AuthorizeClientAsUser(client);
        var response = await PostAutomationNoChecks(home);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
