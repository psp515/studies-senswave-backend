using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Automations.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.Automations.EndTests.Base;

[Trait("Collection", "EndTest")]
public class DeleteAutomationTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var entities = await SetUpTestEntities();

        // Act
        var response = await client.DeleteAsync($"{Paths.AutomationPath}/{entities.AutomationId}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanDeleteAutomation()
    {
        // Arrange
        var client = await CreateUser();
        var entities = await SetUpTestEntities();

        // Act
        var response = await client.DeleteAsync($"{Paths.AutomationPath}/{entities.AutomationId}");
        var getResponse = await client.GetAsync($"{Paths.AutomationPath}/{entities.AutomationId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CanNotDeleteNotExistingAutomation()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.DeleteAsync($"{Paths.AutomationPath}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }


    [Trait("Category", "FailurePath")]
    [Theory]
    [MemberData(nameof(HomePriviliges))]
    public async Task HomeUsersWithPrivilegesCanNotDeleteAutomation(string privilege)
    {
        // Arrange
        var notOwner = CreateUnauthorizedClient();

        var entities = await SetUpTestEntities();

        // Act
        await AuthorizeClientAsAdmin(notOwner);
        await PrepareHomeSharingForAdmin(entities.HomeId, privilege);

        var response = await notOwner.DeleteAsync($"{Paths.AutomationPath}/{entities.AutomationId}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAutomationShouldCascadeDeleteResultsAndConditionsThatBelongsToAutomation()
    {
        // Arrange
        var client = await CreateUser();
        var entities = await SetUpTestEntities();

        // Act
        var response = await client.DeleteAsync($"{Paths.AutomationPath}/{entities.AutomationId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AutomationsContext>();
        var condition = await context.AutomationConditions
            .FirstOrDefaultAsync(x => x.Id == entities.OperationId1);
        var result = await context.AutomationConditions
            .FirstOrDefaultAsync(x => x.Id == entities.OperationId2);

        var homeReference = await context.HomeReferences
            .Where(x => x.Automations.Any(a => a.Id == entities.AutomationId))
            .FirstOrDefaultAsync();

        Assert.Null(homeReference);
        Assert.Null(condition);
        Assert.Null(result);
    }
}