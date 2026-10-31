using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Perf.Core;
using Perf.Core.Runs;

namespace PerfRunner.Export;

/// <summary>Optional: up to 500 traces per valid run from the Grafana Cloud Tempo HTTP API, one JSON trace per line.</summary>
public sealed class TraceExporter(string tempoUrl, string user, string token)
{
    public async Task ExportCampaign(string campaignDir, CancellationToken ct)
    {
        using var http = new HttpClient { BaseAddress = new Uri(tempoUrl.TrimEnd('/') + "/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{token}")));

        foreach (var metaPath in Directory.GetFiles(campaignDir, "meta.json", SearchOption.AllDirectories))
        {
            var meta = JsonSerializer.Deserialize<RunMeta>(await File.ReadAllTextAsync(metaPath, ct), PerfJson.Options)!;
            if (meta.Status != "valid" || meta.MeasureStartUtc is null || meta.MeasureEndUtc is null)
                continue;

            var q = Uri.EscapeDataString($"{{ resource.run_id = \"{meta.RunId}\" }}");
            var search = JsonNode.Parse(await http.GetStringAsync(
                $"api/search?q={q}&start={meta.MeasureStartUtc.Value.ToUnixTimeSeconds()}&end={meta.MeasureEndUtc.Value.ToUnixTimeSeconds()}&limit=500", ct))!;

            var serverDir = Path.Combine(Path.GetDirectoryName(metaPath)!, "server");
            Directory.CreateDirectory(serverDir);
            await using var output = File.CreateText(Path.Combine(serverDir, "traces.jsonl"));
            foreach (var trace in search["traces"]?.AsArray() ?? [])
                await output.WriteLineAsync(await http.GetStringAsync($"api/traces/{trace!["traceID"]!.GetValue<string>()}", ct));
        }
    }
}
