using System.Net;
using System.Net.Security;

namespace Perf.Client.Http;

internal static class HttpHandlers
{
    public static SocketsHttpHandler Create(ApiTarget target) => new()
    {
        EnableMultipleHttp2Connections = true,
        PooledConnectionLifetime = Timeout.InfiniteTimeSpan,
        SslOptions = target.InsecureTls
            ? new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true }
            : new SslClientAuthenticationOptions(),
    };

    /// <summary>REST and SignalR negotiate are forced to HTTP/2 (Spec E: every transport over HTTP/2).</summary>
    public static HttpClient Http2Client(ApiTarget target) => new(Create(target))
    {
        BaseAddress = target.BaseUri,
        DefaultRequestVersion = HttpVersion.Version20,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
    };
}
