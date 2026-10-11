using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Automations.Api.Features.PutConditionToAutomation;
using Senswave.Automations.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using System.Text.Json.Nodes;

namespace Senswave.Automations.EndTests.Base;

[Trait("Collection", "EndTest")]
public class PutConditionTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var entities = await SetUpTestEntities();

        // Act
        var request = Request(entities.OperationId2);
        var response = await client.PutAsync($"{Paths.AutomationConditionsPath}/{entities.AutomationId}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserCanPutConditionToAutomation()
    {
        // Arrange
        var client = await CreateUser();
        var entities = await SetUpTestEntities();

        // Act
        var request = Request(entities.OperationId2);
        var response = await client.PutAsync($"{Paths.AutomationConditionsPath}/{entities.AutomationId}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CheckConditionPutted(entities.AutomationId, entities.OperationId2);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UserCanNotPutNotExistingOperation()
    {
        // Arrange
        var client = await CreateUser();
        var entities = await SetUpTestEntities();

        // Act
        var request = Request(Guid.NewGuid());
        var response = await client.PutAsync($"{Paths.AutomationConditionsPath}/{entities.AutomationId}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [MemberData(nameof(HomePriviliges))]
    public async Task MaliciousUserCanNotPutResult(string privilege)
    {
        // Arrange
        var notOwner = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(notOwner);
        var entities = await SetUpTestEntities();
        await PrepareHomeSharingForAdmin(entities.HomeId, privilege);

        // Act
        var request = Request(entities.OperationId2);
        var response = await notOwner.PutAsync($"{Paths.AutomationConditionsPath}/{entities.AutomationId}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private void CheckConditionPutted(Guid automationId, Guid conditionOperationId)
    {
        var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AutomationsContext>();

        var automation = context.Automations
            .Where(automation => automation.Id == automationId)
            .Include(automation => automation.Conditions)
            .First();
        var savedConditionOperationId = automation.Conditions[1].OperationId;
        Assert.NotNull(automation);
        Assert.Contains(automation.Conditions, automation => automation.OperationId == conditionOperationId);
    }


    private static PutConditionRequest Request(Guid operationId) => new()
    {
        OperationId = operationId,
        ConditionType = "NumberCondition",
        ConditionConfiguration = new JsonObject { ["minValue"] = 37, ["maxValue"] = 38 }
    };
}