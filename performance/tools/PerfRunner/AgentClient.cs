using System.Net.Http.Headers;
using System.Net.Http.Json;
using Perf.Core;
using Perf.Core.Agent;
using Perf.Core.Campaigns;

namespace PerfRunner;

public sealed class AgentClient : IDisposable
{
    private readonly HttpClient _http;

    public AgentClient(CampaignTarget target, string token)
    {
        var handler = new SocketsHttpHandler();
        if (target.InsecureTls)
            handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        _http = new HttpClient(handler) { BaseAddress = target.Agent, Timeout = TimeSpan.FromMinutes(150) };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public Task<AgentStatus> Status(CancellationToken ct) => Read<AgentStatus>(_http.GetAsync("status", ct), ct);
    public Task<AgentStatus> Activate(ActivateRequest request, CancellationToken ct) => Read<AgentStatus>(_http.PostAsJsonAsync("activate", request, PerfJson.Options, ct), ct);
    public Task<AgentStatus> Stop(CancellationToken ct) => Read<AgentStatus>(_http.PostAsync("stop", null, ct), ct);
    public async Task<string> Prepare(CancellationToken ct) => (await Read<PrepareResult>(_http.PostAsync("prepare", null, ct), ct)).SeedLog;
    public async Task<string> Logs(Transport transport, DateTimeOffset since, CancellationToken ct) =>
        await _http.GetStringAsync($"logs/{transport}?sinceUnix={since.ToUnixTimeSeconds()}", ct);

    private static async Task<T> Read<T>(Task<HttpResponseMessage> call, CancellationToken ct)
    {
        var response = await call;
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Agent {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(PerfJson.Options, ct))!;
    }

    private sealed record PrepareResult(string SeedLog);

    public void Dispose() => _http.Dispose();
}
