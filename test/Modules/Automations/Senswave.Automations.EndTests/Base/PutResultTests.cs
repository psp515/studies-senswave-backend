using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Automations.Api.Features.PutResultToAutomation;
using Senswave.Automations.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.Automations.EndTests.Base;

[Trait("Collection", "EndTest")]
public class PutResultTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var entities = await SetUpTestEntities();

        // Act
        var request = Request(entities.OperationId2, "newValueToSend");
        var response = await client.PutAsync($"{Paths.AutomationResultsPath}/{entities.AutomationId}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserCanPutResultToAutomation()
    {
        // Arrange
        const string valueToSend = "newValueToSend124";
        var client = await CreateUser();
        var entities = await SetUpTestEntities();

        // Act
        var request = Request(entities.OperationId2, valueToSend);
        var response = await client.PutAsync($"{Paths.AutomationResultsPath}/{entities.AutomationId}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CheckResultPutted(entities.AutomationId, valueToSend);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UserCanNotPutNotExistingOperation()
    {
        // Arrange
        const string valueToSend = "newValueToSend124";
        var client = await CreateUser();
        var entities = await SetUpTestEntities();

        // Act
        var request = Request(Guid.NewGuid(), valueToSend);
        var response = await client.PutAsync($"{Paths.AutomationResultsPath}/{entities.AutomationId}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [MemberData(nameof(HomePriviliges))]
    public async Task MaliciousUserCanNotPutResult(string privilege)
    {
        // Arrange
        const string valueToSend = "newValueToSend124";
        var notOwner = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(notOwner);
        var entities = await SetUpTestEntities();
        await PrepareHomeSharingForAdmin(entities.HomeId, privilege);

        // Act
        var request = Request(entities.OperationId2, valueToSend);
        var response = await notOwner.PutAsync($"{Paths.AutomationResultsPath}/{entities.AutomationId}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static PutResultRequest Request(Guid operationId, string value) => new()
    {
        OperationId = operationId,
        Value = value
    };

    private void CheckResultPutted(Guid automationId, string valueToSend)
    {
        var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AutomationsContext>();

        var automation = context.Automations
            .Where(automation => automation.Id == automationId)
            .Include(automation => automation.Results)
            .First();
        string a = automation.Results[1].ValueToSend.ToJsonString();
        Assert.NotNull(automation);
        Assert.Contains(automation.Results, result => result.ValueToSend.GetValue<string>() == valueToSend);
    }
}