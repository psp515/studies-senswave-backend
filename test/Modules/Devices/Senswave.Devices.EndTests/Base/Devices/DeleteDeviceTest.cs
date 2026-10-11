using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.Devices.EndTests.Base.Devices;

[Trait("Collection", "EndTest")]
public class DeleteDeviceTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var items = await Arrange();

        // Act
        var response = await client.DeleteAsync($"{Paths.DevicesPath}/{items["device"]}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanDeleteDevice()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(client);
        var items = await Arrange(postDashboard: false, postOperation: false);

        // Act
        var response = await client.DeleteAsync($"{Paths.DevicesPath}/{items["device"]}");
        var getResponse = await client.GetAsync($"{Paths.DevicesPath}/{items["device"]}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CannotDeleteDeviceWithOperationDependency()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(client);
        var items = await Arrange(postDashboard: false, postOperation: true);

        // Act
        var response = await client.DeleteAsync($"{Paths.DevicesPath}/{items["device"]}");
        var getResponse = await client.GetAsync($"{Paths.DevicesPath}/{items["device"]}");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CannotDeleteDeviceWithDashboardDependency()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(client);
        var items = await Arrange(postDashboard: true, postOperation: false);

        // Act
        var response = await client.DeleteAsync($"{Paths.DevicesPath}/{items["device"]}");
        var getResponse = await client.GetAsync($"{Paths.DevicesPath}/{items["device"]}");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MaliciousUserCanNotDeleteDevice()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var malicious = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(client);
        await AuthorizeClientAsAdmin(malicious);

        var items = await Arrange(postDashboard: false, postOperation: false);

        // Act
        var response = await malicious.DeleteAsync($"{Paths.DevicesPath}/{items["device"]}");
        var getResponse = await client.GetAsync($"{Paths.DevicesPath}/{items["device"]}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    private async Task<Dictionary<string, Guid>> Arrange(bool postDashboard = true, bool postOperation = true)
    {
        var items = new Dictionary<string, Guid>
        {
            {
                "home", await PostHome()
            },
            {
                "broker", await PostBroker()
            }
        };

        await PutBrokerForHome(items["broker"], items["home"]);
        items["device"] = await PostDevice(items["home"]);
        if (postDashboard)
            items["dashboard"] = await PostDashboard(items["device"]);

        if (postOperation)
            items["operation"] = await PostBooleanOperation(items["device"]);

        return items;
    }
}