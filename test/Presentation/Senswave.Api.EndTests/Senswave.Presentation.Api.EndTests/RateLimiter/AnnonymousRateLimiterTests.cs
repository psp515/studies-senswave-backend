using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Senswave.Api.RateLimiters.Anonymous;
using Senswave.TestInfrastructure.TestEnvironments.Limit;
using System.Text.Json.Nodes;

namespace Senswave.Presentation.Api.EndTests.RateLimiter;

[Collection("Limits E2E Tests Collection")]
[Trait("Collection", "LimitsEndTests")]
[Trait("Category", "RateLimiting")]
public class AnnonymousRateLimiterTests(LimitTestEnvironment factory) : LimitFeatureTest(factory)
{
    [Fact]
    public async Task RateLimiterWorks()
    {
        // Arrange 
        var path = "api/v1/auth/login";
        var content = new JsonObject
        {
            ["email"]= "email@gmail.com",
            ["password"]= "Valid123!234234",
        }.Serialize();

        var client = CreateClient();
        using var scope = Factory.Server.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<AnonymousRateLimiterOptions>>();

        // Act
        var response = await client.PostAsync(path, content);
        var ctt = await response.Content.ReadAsStringAsync();

        for (int i = 0; i < options.Value.TokenLimit * 5; i++)
        {
            response = await client.PostAsync(path, content);
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
        var options = scope.ServiceProvider.GetRequiredService<IOptions<AnonymousRateLimiterOptions>>();

        var path = "api/v1/auth/login";
        var content = new JsonObject
        {
            ["email"]= "email@gmail.com",
            ["password"]= "Valid123!234234",
        }.Serialize();

        // Act
        var blockResponse = await client.PostAsync(path, content);
        var ctt = await blockResponse.Content.ReadAsStringAsync();

        for (int i = 0; i < options.Value.TokenLimit * 5; i++)
        {
            blockResponse = await client.PostAsync(path, content);
        }

        var sleepTime = TimeSpan.FromSeconds(options.Value.ReplenishmentPeriodSeconds + 1);
        await Task.Delay(sleepTime);

        var workingResponse = await client.PostAsync(path, content);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, blockResponse.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, workingResponse.StatusCode);
    }
}
