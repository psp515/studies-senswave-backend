using Newtonsoft.Json.Linq;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using System.Text.Json.Nodes;

namespace Senswave.Automations.EndTests.Base;

[Trait("Collection", "EndTest")]
public class AutomationStateTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        var entities = await SetUpTestEntities();

        // Act
        var request = Request();
        var response = await client.PutAsync($"{Paths.AutomationPath}/{entities.AutomationId}/state", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserEnableAutomation()
    {
        // Arrange
        var isAutomationEnable = false;
        var client = await CreateUser();

        var entities = await SetUpTestEntities();

        // Act
        var request = Request(false);
        var response = await client.PutAsync($"{Paths.AutomationPath}/{entities.AutomationId}/state", request.Serialize());

        var getResponse = await client.GetAsync($"{Paths.AutomationPath}/{entities.AutomationId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertAutomationIsEnableProperty(entities.AutomationId, getResponse, isAutomationEnable);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MaliciousUserCanNotEnableAutomation()
    {
        // Arrange
        var isAutomationEnable = true;
        var client = CreateUnauthorizedClient(); ;
        var malicious = CreateUnauthorizedClient(); ;
        var entities = await SetUpTestEntities();

        // Act
        await AuthorizeClientAsUser(client);
        await AuthorizeClientAsIntruder(malicious);
        var request = Request(false);
        var response = await malicious.PutAsync($"{Paths.AutomationPath}/{entities.AutomationId}/state", request.Serialize());

        var getResponse = await client.GetAsync($"{Paths.AutomationPath}/{entities.AutomationId}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertAutomationIsEnableProperty(entities.AutomationId, getResponse, isAutomationEnable);
    }


    [Fact]
    public async Task UserWithManagePrivilegeCanEnableAutomation()
    {
        // Arrange
        var isAutomationEnable = false;
        var client = CreateUnauthorizedClient();
        var entities = await SetUpTestEntities();

        // Act
        await AuthorizeClientAsUser(client);
        await PrepareHomeSharingForAdmin(entities.HomeId, _managePrivilege);
        var request = Request(false);
        var response = await client.PutAsync($"{Paths.AutomationPath}/{entities.AutomationId}/state", request.Serialize());

        var getResponse = await client.GetAsync($"{Paths.AutomationPath}/{entities.AutomationId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertAutomationIsEnableProperty(entities.AutomationId, getResponse, isAutomationEnable);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UserWithDisplayPrivilegeCanNotEnableAutomation()
    {
        // Arrange
        var isAutomationEnable = true;
        var client = CreateUnauthorizedClient();
        var malicious = CreateUnauthorizedClient();
        var entities = await SetUpTestEntities();

        // Act
        await AuthorizeClientAsUser(client);
        await AuthorizeClientAsAdmin(malicious);
        await PrepareHomeSharingForAdmin(entities.HomeId, _displayPrivilege);
        var request = Request(false);
        var response = await malicious.PutAsync($"{Paths.AutomationPath}/{entities.AutomationId}/state", request.Serialize());

        var getResponse = await client.GetAsync($"{Paths.AutomationPath}/{entities.AutomationId}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertAutomationIsEnableProperty(entities.AutomationId, getResponse, isAutomationEnable);
    }

    private static async Task AssertAutomationIsEnableProperty(Guid automationId, HttpResponseMessage response, bool isEnabledAnswer)
    {
        var jsonObj = JObject.Parse(await response.Content.ReadAsStringAsync());
        var id = jsonObj["id"]?.ToString();
        var isEnable = jsonObj["isEnabled"]?.ToString();

        Assert.Equal(automationId.ToString(), id);
        Assert.Equal(isEnabledAnswer.ToString(), isEnable);
    }

    private JsonObject Request(bool isEnabled = false) => new()
    {
        ["isEnabled"] = isEnabled
    };
}