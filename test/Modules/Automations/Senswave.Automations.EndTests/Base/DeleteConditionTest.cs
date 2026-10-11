using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Automations.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.Automations.EndTests.Base;

[Trait("Collection", "EndTest")]
public class DeleteConditionTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var entities = await SetUpTestEntities();
        var automationConditionId = GetAutomationConditionId(entities.AutomationId);

        // Act
        var response = await client.DeleteAsync($"{Paths.AutomationConditionsPath}/{automationConditionId}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanDeleteConditionFromAutomation()
    {
        // Arrange
        var client = await CreateUser();
        var entities = await SetUpTestEntities();
        var automationConditionId = GetAutomationConditionId(entities.AutomationId);

        // Act
        // TODO: Uncomment after implementing Put Condition
        // await client.PutAsync(
        //     $"{Paths.AutomationResultsPath}/{entities.AutomationId}", 
        //     PutAdditionalResultToAutomationRequest(entities.OperationId2, "newValue").Serialize());
        var response = await client.DeleteAsync($"{Paths.AutomationConditionsPath}/{automationConditionId}");

        // Assert
        AutomationConditionDeleted(entities.AutomationId, automationConditionId);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CanNotDeleteNotExistingConditionFromAutomation()
    {
        // Arrange
        var client = await CreateUser();
        var entities = await SetUpTestEntities();
        var automationConditionId = GetAutomationConditionId(entities.AutomationId);

        // Act
        // await client.PutAsync(
        //     $"{Paths.AutomationResultsPath}/{entities.AutomationId}", 
        //     PutAdditionalResultToAutomationRequest(entities.OperationId2, "newValue").Serialize());
        var response = await client.DeleteAsync($"{Paths.AutomationConditionsPath}/{automationConditionId}");
        var response2 = await client.DeleteAsync($"{Paths.AutomationConditionsPath}/{automationConditionId}");

        // Assert
        AutomationConditionDeleted(entities.AutomationId, automationConditionId);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, response2.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [MemberData(nameof(HomePriviliges))]
    public async Task FriendCanNotDeleteConditionFromAutomation(string privilege)
    {
        // Arrange
        var client = await CreateUser();
        var notOwner = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(notOwner);
        var entities = await SetUpTestEntities();
        await PrepareHomeSharingForAdmin(entities.HomeId, privilege);
        var automationConditionIdId = GetAutomationConditionId(entities.AutomationId);

        // Act
        // await client.PutAsync(
        //     $"{Paths.AutomationResultsPath}/{entities.AutomationId}", 
        //     PutAdditionalResultToAutomationRequest(entities.OperationId2, "newValue").Serialize());
        var response = await notOwner.DeleteAsync($"{Paths.AutomationConditionsPath}/{automationConditionIdId}");

        // Assert
        Assert.Equal(automationConditionIdId, GetAutomationConditionId(entities.AutomationId));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    private void AutomationConditionDeleted(Guid automationId, Guid conditionId)
    {
        var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AutomationsContext>();

        var automation = context.Automations
            .Where(automation => automation.Id == automationId)
            .Include(automation => automation.Conditions)
            .First();

        Assert.NotNull(automation);
        Assert.DoesNotContain(automation.Conditions, condition => condition.Id == conditionId);
    }

    private Guid GetAutomationConditionId(Guid automationId)
    {
        var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AutomationsContext>();

        var automation = context.Automations
            .Where(automation => automation.Id == automationId)
            .Include(automation => automation.Conditions)
            .First();

        Assert.NotNull(automation);
        Assert.NotEmpty(automation.Conditions);

        return automation.Conditions.First().Id;
    }
}