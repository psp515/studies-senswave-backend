using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PerfAgent;

namespace Perf.UnitTests;

public class BearerTokenMiddlewareTests
{
    private const string Token = "0123456789abcdef0123";

    private static async Task<(int Status, bool NextCalled)> Call(string configured, string? header)
    {
        var nextCalled = false;
        var middleware = new BearerTokenMiddleware(_ => { nextCalled = true; return Task.CompletedTask; },
            Options.Create(new AgentOptions { Token = configured }));
        var context = new DefaultHttpContext();
        if (header is not null)
            context.Request.Headers.Authorization = header;

        await middleware.Invoke(context);

        return (context.Response.StatusCode, nextCalled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer wrong-token-0123456789")]
    [InlineData("0123456789abcdef0123")]
    public async Task RejectsMissingOrWrongToken(string? header)
    {
        var (status, next) = await Call(Token, header);

        Assert.Equal(401, status);
        Assert.False(next);
    }

    [Fact]
    public async Task RejectsEverythingWhenConfiguredTokenIsTooShort()
    {
        var (status, next) = await Call("short", "Bearer short");

        Assert.Equal(401, status);
        Assert.False(next);
    }

    [Fact]
    public async Task PassesThroughWithCorrectToken()
    {
        var (status, next) = await Call(Token, "Bearer " + Token);

        Assert.Equal(200, status);
        Assert.True(next);
    }
}
