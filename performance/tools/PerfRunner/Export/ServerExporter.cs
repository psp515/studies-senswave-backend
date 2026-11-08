using System.Text;
using System.Text.Json;
using Perf.Core;
using Perf.Core.Export;
using Perf.Core.Runs;

namespace PerfRunner.Export;

/// <summary>Writes server/*.csv for every valid or saturated run of a campaign. Re-runnable: overwrites the files.</summary>
public sealed class ServerExporter(PrometheusClient prometheus)
{
    public async Task ExportCampaign(string campaignDir, CancellationToken ct)
    {
        foreach (var metaPath in Directory.GetFiles(campaignDir, "meta.json", SearchOption.AllDirectories))
        {
            var meta = JsonSerializer.Deserialize<RunMeta>(await File.ReadAllTextAsync(metaPath, ct), PerfJson.Options)!;
            if (!RunStatus.HasMeasurement(meta.Status) || meta.MeasureStartUtc is null || meta.MeasureEndUtc is null)
                continue;

            var serverDir = Path.Combine(Path.GetDirectoryName(metaPath)!, "server");
            Directory.CreateDirectory(serverDir);
            var files = new Dictionary<string, StringBuilder>();

            foreach (var query in ExportQueries.For(meta))
            {
                var csv = files.TryGetValue(query.File, out var existing) ? existing : files[query.File] = new StringBuilder(PrometheusCsv.Header);
                var data = query.Range
                    ? await prometheus.Range(query.PromQl, meta.MeasureStartUtc.Value, meta.MeasureEndUtc.Value, 10, ct)
                    : await prometheus.Instant(query.PromQl, meta.MeasureEndUtc.Value, ct);
                PrometheusCsv.Append(csv, query.PromQl, data);
            }

            foreach (var (file, csv) in files)
                await File.WriteAllTextAsync(Path.Combine(serverDir, file), csv.ToString(), ct);

            Console.WriteLine($"exported {meta.RunId}");
        }
    }
}
