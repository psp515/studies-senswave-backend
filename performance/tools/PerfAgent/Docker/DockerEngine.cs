using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace PerfAgent.Docker;

/// <summary>Docker Engine API over the unix socket with plain HttpClient (no SDK, no CLI).</summary>
public sealed class DockerEngine : IDockerEngine, IDisposable
{
    private const string Api = "http://docker/v1.44";
    private readonly HttpClient _http;

    public DockerEngine(IOptions<AgentOptions> options)
    {
        var socket = options.Value.DockerSocket;
        _http = new HttpClient(new SocketsHttpHandler
        {
            ConnectCallback = async (_, ct) =>
            {
                var s = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await s.ConnectAsync(new UnixDomainSocketEndPoint(socket), ct);
                return new NetworkStream(s, ownsSocket: true);
            },
        }) { Timeout = TimeSpan.FromMinutes(2) };
    }

    public async Task<JsonObject> Inspect(string name, CancellationToken ct)
    {
        var response = await Send(HttpMethod.Get, $"/containers/{name}/json", null, ct);
        return (await response.Content.ReadFromJsonAsync<JsonObject>(ct))!;
    }

    public async Task Stop(string name, CancellationToken ct) => await Send(HttpMethod.Post, $"/containers/{name}/stop?t=10", null, ct, allow304: true);

    public async Task Remove(string name, CancellationToken ct) => await Send(HttpMethod.Delete, $"/containers/{name}", null, ct);

    public async Task Create(string name, JsonObject body, CancellationToken ct) =>
        await Send(HttpMethod.Post, $"/containers/create?name={Uri.EscapeDataString(name)}", body, ct);

    public async Task Start(string name, CancellationToken ct) => await Send(HttpMethod.Post, $"/containers/{name}/start", null, ct, allow304: true);

    public async Task<string> Logs(string name, long sinceUnix, CancellationToken ct)
    {
        var response = await Send(HttpMethod.Get, $"/containers/{name}/logs?stdout=1&stderr=1&timestamps=1&since={sinceUnix}", null, ct);
        return DockerLogDemuxer.Demux(await response.Content.ReadAsByteArrayAsync(ct));
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, JsonObject? body, CancellationToken ct, bool allow304 = false)
    {
        using var request = new HttpRequestMessage(method, Api + path) { Content = body is null ? null : JsonContent.Create(body) };
        var response = await _http.SendAsync(request, ct);

        if (response.IsSuccessStatusCode || (allow304 && (int)response.StatusCode == 304))
            return response;

        throw new InvalidOperationException($"Docker {method} {path} -> {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
    }

    public void Dispose() => _http.Dispose();
}
