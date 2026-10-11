using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.Common;

namespace Senswave.Automations.EndTests.Base;

[Trait("Collection", "EndTest")]
public class GetAutomationTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var entities = await SetUpTestEntities();

        // Act
        var response = await client.GetAsync($"{Paths.AutomationPath}/{entities.AutomationId}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserCanGetAutomation()
    {
        // Arrange
        var client = await CreateUser();
        var entities = await SetUpTestEntities();

        // Act
        var response = await client.GetAsync($"{Paths.AutomationPath}/{entities.AutomationId}");
        var automationDto = JsonSerializer.Deserialize<IdResponse>(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(automationDto);
        Assert.Equal(entities.AutomationId, automationDto.Id);
    }


    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UserCanNotGetNotExistingAutomation()
    {
        // Arrange
        var client = await CreateUser();

        // Act
        var response = await client.GetAsync($"{Paths.AutomationPath}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MaliciousUserCanNotGetAutomation()
    {
        // Arrange
        var client = await CreateUser();
        var entities = await SetUpTestEntities();

        // Act
        await AuthorizeClientAsIntruder(client);
        var response = await client.GetAsync($"{Paths.AutomationPath}/{entities.AutomationId}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Theory]
    [MemberData(nameof(HomePriviliges))]
    public async Task UserWithProperPrivilegeCanGetAutomation(string privilege)
    {
        // Arrange
        var client = await CreateUser();
        var entities = await SetUpTestEntities();

        // Act
        await PrepareHomeSharingForAdmin(entities.HomeId, privilege);
        await AuthorizeClientAsAdmin(client);
        var response = await client.GetAsync($"{Paths.AutomationPath}/{entities.AutomationId}");
        var automationDto = JsonSerializer.Deserialize<IdResponse>(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(automationDto);
        Assert.Equal(entities.AutomationId, automationDto.Id);
    }


}