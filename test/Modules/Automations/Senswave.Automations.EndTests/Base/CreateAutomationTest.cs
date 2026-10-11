using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Automations.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.Automations;
using Senswave.TestInfrastructure.TestSetup.Models.Common;
using System.Text.Json.Nodes;

namespace Senswave.Automations.EndTests.Base;

[Trait("Collection", "EndTest")]
public class CreateAutomationTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        var client = CreateUnauthorizedClient();

        var request = await SetUpTestAndCreateAutomationRequest();

        // Act
        var response = await client.PostAsync(Paths.AutomationPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserCanPostAutomation()
    {
        var client = CreateUnauthorizedClient(); ;
        await AuthorizeClientAsUser(client);

        var request = await SetUpTestAndCreateAutomationRequest();

        // Act
        var response = await client.PostAsync(Paths.AutomationPath, request.Serialize());
        var responseDto = JsonSerializer.Deserialize<IdResponse>(await response.Content.ReadAsStringAsync());
        Assert.NotNull(responseDto);

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AutomationsContext>();
        var automation = await context.Automations
            .FirstOrDefaultAsync(x => x.Id == responseDto!.Id);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(automation);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CanNotHaveTwoAutomationsWithTheSameNameAmongOneHome()
    {
        var client = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(client);

        var entities = await SetUpTestEntities();
        var request = await SetUpTestAndCreateAutomationRequest(entities);

        // Act
        var response = await client.PostAsync(Paths.AutomationPath, request.Serialize());
        var copyPost = await client.PostAsync(Paths.AutomationPath, request.Serialize());

        var responseDto = JsonSerializer.Deserialize<IdResponse>(await response.Content.ReadAsStringAsync());
        Assert.NotNull(responseDto);

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AutomationsContext>();
        var automation = await context.Automations
            .FirstOrDefaultAsync(x => x.Id == responseDto!.Id);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, copyPost.StatusCode);
        Assert.NotNull(automation);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CanNotExistsAutomationWithBadConditionConfiguration()
    {
        var client = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(client);

        var request = await SetUpTestAndCreateAutomationRequest();
        request.Conditions[0].ConditionConfiguration = new JsonObject
        {
            ["definitelyWrongConfiguration"] = 25
        };

        // Act
        var serialized = request.Serialize();
        var response = await client.PostAsync(Paths.AutomationPath, serialized);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(_managePrivilege, _actionPrivilege)]
    [InlineData(_managePrivilege, _managePrivilege)]
    public async Task FriendWithProperPrivilegeCanPostAutomation(string homePrivilege, string privilege)
    {
        var friend = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(friend);

        var entities = await SetUpTestEntities();

        await PrepareHomeSharingForAdmin(entities.HomeId, homePrivilege);
        await PrepareDeviceSharing(privilege, entities.DeviceId);

        var request = await SetUpTestAndCreateAutomationRequest(entities);

        // Act
        var response = await friend.PostAsync(Paths.AutomationPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }


    [Trait("Category", "FailurePath")]
    [Theory]
    [InlineData(_displayPrivilege, _displayPrivilege)]
    [InlineData(_displayPrivilege, _actionPrivilege)]
    [InlineData(_displayPrivilege, _managePrivilege)]
    [InlineData(_managePrivilege, _displayPrivilege)]
    public async Task FriendWithNonProperPrivilegeCanNotPostAutomation(string homePrivilege, string devicePrivilege)
    {
        var friend = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(friend);

        var entities = await SetUpTestEntities();

        await PrepareHomeSharingForAdmin(entities.HomeId, homePrivilege);
        await PrepareDeviceSharing(devicePrivilege, entities.DeviceId);

        var request = await SetUpTestAndCreateAutomationRequest(entities);

        // Act
        var response = await friend.PostAsync(Paths.AutomationPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<CreateAutomationRequest> SetUpTestAndCreateAutomationRequest(BaseUserEntities? entities = null)
    {
        if (entities is null)
            entities = await SetUpTestEntities();

        return new CreateAutomationRequest
        {
            HomeId = entities.Value.HomeId,
            Name = Guid.NewGuid().ToString()[1..30],
            Icon = "gear",
            ConditionConnector = "And",
            Conditions = [CreateCondition(entities.Value.OperationId1)],
            Results = [CreateResult(entities.Value.OperationId2)]
        };

    }

}