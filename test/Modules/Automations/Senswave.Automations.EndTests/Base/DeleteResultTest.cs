using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Automations.Api.Features.PutResultToAutomation;
using Senswave.Automations.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.Automations.EndTests.Base;

[Trait("Collection", "EndTest")]
public class DeleteResultTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var entities = await SetUpTestEntities();
        var automationResultId = GetAutomationResultId(entities.AutomationId);

        // Act
        var response = await client.DeleteAsync($"{Paths.AutomationResultsPath}/{automationResultId}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task OwnerCanDeleteResultFromAutomation()
    {
        // Arrange
        var client = await CreateUser();
        var entities = await SetUpTestEntities();
        var automationResultId = GetAutomationResultId(entities.AutomationId);

        // Act
        await client.PutAsync(
            $"{Paths.AutomationResultsPath}/{entities.AutomationId}",
            PutAdditionalResultToAutomationRequest(entities.OperationId2, "newValue").Serialize());
        var response = await client.DeleteAsync($"{Paths.AutomationResultsPath}/{automationResultId}");

        // Assert
        AutomationResultDeleted(entities.AutomationId, automationResultId);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [MemberData(nameof(HomePriviliges))]
    public async Task FriendCanNotDeleteResultFromAutomation(string privilege)
    {
        // Arrange
        var client = await CreateUser();
        var notOwner = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(notOwner);
        var entities = await SetUpTestEntities();
        await PrepareHomeSharingForAdmin(entities.HomeId, privilege);
        var automationResultId = GetAutomationResultId(entities.AutomationId);
        var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AutomationsContext>();

        // Act
        var response = await notOwner.DeleteAsync($"{Paths.AutomationResultsPath}/{automationResultId}");

        var automationResultExists = await context.Automations
            .Where(x => x.Results.Any(x => x.Id == automationResultId))
            .AnyAsync();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(automationResultExists);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CanNotDeleteLastResultFromAutomation()
    {
        // Arrange
        var client = await CreateUser();
        var entities = await SetUpTestEntities();
        var automationResultId = GetAutomationResultId(entities.AutomationId);

        // Act
        var response = await client.DeleteAsync($"{Paths.AutomationResultsPath}/{automationResultId}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CanNotDeleteNotExistingResultFromAutomation()
    {
        // Arrange
        var client = await CreateUser();
        var entities = await SetUpTestEntities();
        var automationResultId = GetAutomationResultId(entities.AutomationId);

        // Act
        await client.PutAsync(
            $"{Paths.AutomationResultsPath}/{entities.AutomationId}",
            PutAdditionalResultToAutomationRequest(entities.OperationId2, "newValue").Serialize());
        var response = await client.DeleteAsync($"{Paths.AutomationResultsPath}/{automationResultId}");
        var response2 = await client.DeleteAsync($"{Paths.AutomationResultsPath}/{automationResultId}");

        // Assert
        AutomationResultDeleted(entities.AutomationId, automationResultId);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, response2.StatusCode);
    }

    private Guid GetAutomationResultId(Guid automationId)
    {
        var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AutomationsContext>();

        var automation = context.Automations
            .Where(automation => automation.Id == automationId)
            .Include(automation => automation.Results)
            .First();

        Assert.NotNull(automation);
        Assert.NotEmpty(automation.Results);

        return automation.Results.First().Id;
    }

    private void AutomationResultDeleted(Guid automationId, Guid resultId)
    {
        var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AutomationsContext>();

        var automation = context.Automations
            .Where(automation => automation.Id == automationId)
            .Include(automation => automation.Results)
            .First();

        Assert.NotNull(automation);
        Assert.DoesNotContain(automation.Results, result => result.Id == resultId);
    }

    private static PutResultRequest PutAdditionalResultToAutomationRequest(Guid operationId, string value) => new()
    {
        OperationId = operationId,
        Value = value
    };

}