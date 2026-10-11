using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Senswave.Api.RateLimiters.User;
using Senswave.TestInfrastructure.TestEnvironments.Limit;

namespace Senswave.Presentation.Api.EndTests.RateLimiter;

[Collection("Limits E2E Tests Collection")]
[Trait("Collection", "LimitsEndTests")]
[Trait("Category", "RateLimiting")]
public class UserRateLimiterTests(LimitTestEnvironment factory) : LimitFeatureTest(factory)
{
    [Fact]
    public async Task RateLimiterWorks()
    {
        // Arrange 
        var client = CreateClient();

        using var scope = Factory.Server.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<UserRateLimiterOptions>>();

        // Act
        await AuthorizeClientAsAdmin(client);
        var brokerId = await PostBroker();

        var response = await client.GetAsync(Paths.ClientsPath(brokerId));
        for (int i = 0; i < options.Value.TokenLimit; i++)
        {
            response = await client.GetAsync(Paths.ClientsPath(brokerId));
        }

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task RateLimiterRefreshesTokens()
    {
        // Arrange 
        var client = CreateClient();

        using var scope = Factory.Server.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<UserRateLimiterOptions>>();

        // Act
        await AuthorizeClientAsAdmin(client);
        var brokerId = await PostBroker();

        var response = await client.GetAsync(Paths.ClientsPath(brokerId));
        for (int i = 0; i < options.Value.TokenLimit; i++)
        {
            response = await client.GetAsync(Paths.ClientsPath(brokerId));
        }

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);

        var sleepTime = TimeSpan.FromSeconds(options.Value.ReplenishmentPeriodSeconds + 1);
        await Task.Delay(sleepTime);

        var workingResponse = await client.GetAsync(Paths.ClientsPath(brokerId));

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, workingResponse.StatusCode);
    }
}
