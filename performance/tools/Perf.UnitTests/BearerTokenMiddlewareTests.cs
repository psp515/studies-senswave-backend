using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PerfAgent;

namespace Perf.UnitTests;

public class BearerTokenMiddlewareTests
{
    private const string Token = "0123456789abcdef0123456789abcdef";

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
    [InlineData("Bearer wrong-token-0123456789wrong-token")]
    [InlineData("0123456789abcdef0123456789abcdef")]
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

    [Theory]
    [InlineData("change-me-to-a-long-random-string")]
    [InlineData("0123456789abcdef0123456789abcde")]
    [InlineData("")]
    public async Task RejectsEverythingForTemplateOrShortTokens(string configured)
    {
        var (status, next) = await Call(configured, "Bearer " + configured);

        Assert.Equal(401, status);
        Assert.False(next);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("change-me-to-a-long-random-string", false)]
    [InlineData("0123456789abcdef0123456789abcde", false)]
    [InlineData("0123456789abcdef0123456789abcdef", true)]
    public void TokenPolicyDescribesProblem(string? token, bool acceptable) =>
        Assert.Equal(acceptable, AgentTokenPolicy.Problem(token) is null);

    [Fact]
    public async Task PassesThroughWithCorrectToken()
    {
        var (status, next) = await Call(Token, "Bearer " + Token);

        Assert.Equal(200, status);
        Assert.True(next);
    }
}
