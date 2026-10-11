using Senswave.TestInfrastructure.TestEnvironments.Limit;

namespace Senswave.Devices.EndTests.Limits.Dashbaords;

[Collection("Limits E2E Tests Collection")]
[Trait("Collection", "LimitsEndTests")]
public class PostDashboard(LimitTestEnvironment factory) : LimitFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task LimitationWorks()
    {
        // Arrange
        await CleanDevices();
        var client = CreateClient();
        var limit = LimitTestEnvironment.DevicesOptions.Limits.DashboardsPerDevice;
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);

        // Act
        for (int i = 0; i < limit; i++)
        {
            await PostDashboard(device);
        }

        await AuthorizeClientAsUser(client);
        var response = await PostDashboardNoChecks(device);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
