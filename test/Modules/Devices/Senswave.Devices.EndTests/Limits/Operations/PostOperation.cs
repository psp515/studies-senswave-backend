using Senswave.TestInfrastructure.TestEnvironments.Limit;

namespace Senswave.Devices.EndTests.Limits.Operations;

[Collection("Limits E2E Tests Collection")]
[Trait("Collection", "LimitsEndTests")]
public class PostOperation(LimitTestEnvironment factory) : LimitFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task LimitationWorks()
    {
        // Arrange
        await CleanDevices();
        var client = CreateClient();
        var limit = LimitTestEnvironment.DevicesOptions.Limits.OperationsPerDevice;
        var home = await PostHomeWithBroker();
        var device = await PostDevice(home);

        // Act
        for (int i = 0; i < limit; i++)
        {
            await PostBooleanOperation(device, "testTopic");
        }

        await AuthorizeClientAsUser(client);
        var response = await PostBooleanOperationNoChecks(device);
        var responseContent = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.DoesNotContain("Subscribtion", responseContent);
    }
}
