using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace PerfRunner.Export;

/// <summary>Grafana Cloud Prometheus HTTP API (read token: metrics:read).</summary>
public sealed class PrometheusClient : IDisposable
{
    private readonly HttpClient _http;

    public PrometheusClient(string baseUrl, string user, string token)
    {
        // The query base (GRAFANA_CLOUD_PROM_URL without /push), e.g. https://prometheus-prod-XX-prod-eu-west-2.grafana.net/api/prom
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{token}")));
    }

    public Task<JsonNode> Instant(string query, DateTimeOffset time, CancellationToken ct) =>
        Get($"api/v1/query?query={Uri.EscapeDataString(query)}&time={time.ToUnixTimeSeconds()}", ct);

    public Task<JsonNode> Range(string query, DateTimeOffset start, DateTimeOffset end, int stepSeconds, CancellationToken ct) =>
        Get($"api/v1/query_range?query={Uri.EscapeDataString(query)}&start={start.ToUnixTimeSeconds()}&end={end.ToUnixTimeSeconds()}&step={stepSeconds}", ct);

    private async Task<JsonNode> Get(string path, CancellationToken ct)
    {
        var response = await _http.GetAsync(path, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Prometheus {(int)response.StatusCode}: {body}");
        return JsonNode.Parse(body)!["data"]!;
    }

    public void Dispose() => _http.Dispose();
}
