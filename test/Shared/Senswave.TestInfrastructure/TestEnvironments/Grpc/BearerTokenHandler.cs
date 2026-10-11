using System.Net.Http.Headers;

namespace Senswave.TestInfrastructure.TestEnvironments.Grpc;

/// <summary>
/// Attaches <c>Authorization: Bearer</c> to every gRPC call made through the channel.
/// </summary>
internal sealed class BearerTokenHandler(string accessToken, HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return base.SendAsync(request, cancellationToken);
    }
}
