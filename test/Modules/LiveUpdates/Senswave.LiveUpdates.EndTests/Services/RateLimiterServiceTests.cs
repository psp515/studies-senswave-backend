using Microsoft.AspNetCore.SignalR.Client;
using Senswave.LiveUpdates.EndTests.Extensions;
using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.LiveUpdates.EndTests.Services;

[Collection(GlobalUsings.LiveUpdatesCollection)]
[Trait("Collection", "EndTest")]
public class RateLimiterServiceTests(BaseTestEnvironment factory) : BaseSignalRFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task RateLimiterExceeded()
    {
        // Arrange
        var brokerId = await PostBroker();
        var homeId = await PostHome();
        await PutBrokerForHome(brokerId, homeId);

        var client = CreateUnauthorizedClient();
        var connection = await client.ToSignalR(CreateHandler(), useAdmin: false);

        int failsCount = 0;

        // Act
        connection.On<string>("FailedToInitialize", (message) =>
        {
            failsCount += 1;
        });

        await connection.StartAsync();
        await connection.InvokeAsync("Initialize", homeId.ToString());
        await connection.InvokeAsync("Initialize", homeId.ToString());
        await connection.InvokeAsync("Initialize", homeId.ToString());
        await connection.InvokeAsync("Initialize", homeId.ToString());
        await connection.InvokeAsync("Initialize", homeId.ToString());
        await connection.InvokeAsync("Initialize", homeId.ToString());
        await Task.Delay(500);

        // Assert
        Assert.Equal(HubConnectionState.Connected, connection.State);
        Assert.Equal(1, failsCount);
    }
}
