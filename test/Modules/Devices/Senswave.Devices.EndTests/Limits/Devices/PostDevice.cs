using Senswave.TestInfrastructure.TestEnvironments.Limit;

namespace Senswave.Devices.EndTests.Limits.Devices;

[Collection("Limits E2E Tests Collection")]
[Trait("Collection", "LimitsEndTests")]
public class PostDevice(LimitTestEnvironment factory) : LimitFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task LimitationWorks()
    {
        // Arrange
        await CleanDevices();
        var client = CreateClient();
        var limit = LimitTestEnvironment.DevicesOptions.Limits.DevicesPerHome;
        var home = await PostHomeWithBroker();

        // Act
        await AuthorizeClientAsUser(client);

        for (int i = 0; i < limit; i++)
        {
            await PostDevice(home);
        }

        var response = await PostDeviceNoChecks(home);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
