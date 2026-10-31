using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace PerfAgent;

public sealed class BearerTokenMiddleware(RequestDelegate next, IOptions<AgentOptions> options)
{
    private readonly byte[] _expected = Encoding.UTF8.GetBytes("Bearer " + options.Value.Token);

    public async Task Invoke(HttpContext context)
    {
        var header = Encoding.UTF8.GetBytes(context.Request.Headers.Authorization.ToString());

        if (options.Value.Token.Length < 16 || !CryptographicOperations.FixedTimeEquals(header, _expected))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }
}
