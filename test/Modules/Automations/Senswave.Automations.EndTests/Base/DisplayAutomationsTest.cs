using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.Automations;

namespace Senswave.Automations.EndTests.Base;

[Trait("Collection", "EndTest")]
public class DisplayAutomationsTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        var client = CreateUnauthorizedClient();
        var entities = await SetUpTestEntities();

        // Act
        var response = await client.GetAsync($"{Paths.AutomationPath}/display?homeId={entities.HomeId}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserCanGetAutomations()
    {
        var client = await CreateUser();
        var entities = await SetUpTestEntities();

        // Act
        var response = await client.GetAsync($"{Paths.AutomationPath}/display?homeId={entities.HomeId}");
        var idResponses = JsonSerializer.Deserialize<GetAutomationsResponse>(await response.Content.ReadAsStringAsync());
        Assert.NotNull(idResponses);
        var automationIds = idResponses.Items.Select(x => x.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(entities.AutomationId, automationIds);
    }

    [Theory]
    [MemberData(nameof(HomePriviliges))]
    public async Task FriendWithProperPrivilegeCanGetAutomations(string privilege)
    {
        var friend = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(friend);
        var entities = await SetUpTestEntities();
        await PrepareHomeSharingForAdmin(entities.HomeId, privilege);

        // Act
        var response = await friend.GetAsync($"{Paths.AutomationPath}/display?homeId={entities.HomeId}");
        var idResponses = JsonSerializer.Deserialize<GetAutomationsResponse>(await response.Content.ReadAsStringAsync());
        Assert.NotNull(idResponses);
        var automationIds = idResponses.Items.Select(x => x.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(entities.AutomationId, automationIds);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MaliciousUserCanGetAutomations()
    {
        var malicious = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(malicious);
        var entities = await SetUpTestEntities();

        // Act
        var response = await malicious.GetAsync($"{Paths.AutomationPath}/display?homeId={entities.HomeId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

}