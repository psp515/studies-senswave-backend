using Newtonsoft.Json.Linq;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.Automations;


namespace Senswave.Automations.EndTests.Base;

[Trait("Collection", "EndTest")]
public class PatchAutomationTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var automationName = "New-Automation-Name";
        var client = CreateUnauthorizedClient();
        var entities = await SetUpTestEntities();

        // Act
        var request = Request(automationName);
        var response = await client.PatchAsync($"{Paths.AutomationPath}/{entities.AutomationId}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserCanPatchAutomation()
    {
        // Arrange
        var automationName = "New-Automation-Name";
        var client = CreateUnauthorizedClient();
        var entities = await SetUpTestEntities();

        // Act
        await AuthorizeClientAsUser(client);
        var request = Request(automationName);
        var successPatch = await client.PatchAsync($"{Paths.AutomationPath}/{entities.AutomationId}", request.Serialize());
        var getResponse = await client.GetAsync($"{Paths.AutomationPath}/{entities.AutomationId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, successPatch.StatusCode);
        await AssertNameChanged(getResponse, entities.AutomationId, automationName);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MaliciousCanNotPatchAutomation()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var malicious = CreateUnauthorizedClient();
        var entities = await SetUpTestEntities();

        // Act
        await AuthorizeClientAsUser(client);
        await AuthorizeClientAsIntruder(malicious);
        var request = Request("automationName");
        var response = await malicious.PatchAsync($"{Paths.AutomationPath}/{entities.AutomationId}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UserWithoutPrivilegesCannotPatchAutomation()
    {
        // Arrange
        var automationName = "New-Automation-Name";
        var client = CreateUnauthorizedClient();
        var entities = await SetUpTestEntities();
        await PrepareHomeSharingForAdmin(entities.HomeId, _displayPrivilege);
        var request = Request(automationName);

        // Act
        await AuthorizeClientAsAdmin(client);
        var patch = await client.PatchAsync($"{Paths.AutomationPath}/{entities.AutomationId}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, patch.StatusCode);
    }

    [Fact]
    public async Task FriendWithProperPrivilegeCanPatchAutomation()
    {
        // Arrange
        var automationName = "New-Automation-Name";
        var client = CreateUnauthorizedClient();
        var entities = await SetUpTestEntities();

        // Act
        await AuthorizeClientAsAdmin(client);
        await PrepareHomeSharingForAdmin(entities.HomeId, _managePrivilege);
        var request = Request(automationName);
        var successPatch = await client.PatchAsync($"{Paths.AutomationPath}/{entities.AutomationId}", request.Serialize());
        var getResponse = await client.GetAsync($"{Paths.AutomationPath}/{entities.AutomationId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, successPatch.StatusCode);
        await AssertNameChanged(getResponse, entities.AutomationId, automationName);
    }

    private static async Task AssertNameChanged(HttpResponseMessage getResponse, Guid automationId, string newName)
    {
        var jsonObj = JObject.Parse(await getResponse.Content.ReadAsStringAsync());
        var id = jsonObj["id"]?.ToString();
        var name = jsonObj["name"]?.ToString();

        Assert.Equal(automationId.ToString(), id);
        Assert.Equal(newName, name);
    }

    private static PatchAutomationRequest Request(string name) => new()
    {
        Name = name
    };
}