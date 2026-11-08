using System.Text.Json;
using System.Text.Json.Nodes;
using Perf.Core;
using Perf.Core.Knee;
using Perf.Core.Runs;

namespace PerfRunner;

/// <summary>Reads a knee campaign's results, writes knee.csv and prints each knee and the suggested fixed rates.</summary>
public static class KneeCommand
{
    public static async Task Run(string campaignDir, CancellationToken ct)
    {
        var finals = new Dictionary<string, (RunMeta Meta, string Dir)>();
        foreach (var metaPath in Directory.GetFiles(campaignDir, "meta.json", SearchOption.AllDirectories))
        {
            var meta = JsonSerializer.Deserialize<RunMeta>(await File.ReadAllTextAsync(metaPath, ct), PerfJson.Options)!;
            if (!finals.TryGetValue(meta.RunId, out var known) || meta.Attempt > known.Meta.Attempt)
                finals[meta.RunId] = (meta, Path.GetDirectoryName(metaPath)!);
        }

        var steps = new List<KneeStep>();
        foreach (var (meta, dir) in finals.Values)
        {
            var (p50, p99, achieved) = await ClientNumbers(dir, meta, ct);
            steps.Add(new KneeStep(meta.Transport, meta.Scenario, meta.Rate, meta.Status, p50, p99, achieved));
        }

        await File.WriteAllTextAsync(Path.Combine(campaignDir, "knee.csv"), KneeReport.Csv(steps), ct);

        var campaign = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(campaignDir, "campaign.json"), ct));
        var knees = KneeReport.Knees(steps, campaign?["campaign"]?["knee"]?["max"]?.GetValue<int>());
        foreach (var knee in knees)
            Console.WriteLine($"{knee.Scenario,-4} {knee.Transport,-8} knee {(knee.AtLeast ? ">= " : "")}{knee.KneeRate}/s");

        var suggestions = KneeReport.Suggest(knees);
        foreach (var s in suggestions)
            Console.WriteLine(s.Rates.Length == 0
                ? $"{s.Scenario}: no valid step on some transport; lower the first rate and repeat"
                : $"{s.Scenario}: lowest knee {(s.AtLeast ? ">= " : "")}{s.LowestKnee}/s -> rates [{string.Join(", ", s.Rates)}]{(s.AtLeast ? " (lower bound: not really 25% and 80% of the capacity)" : "")}");

        var scenarioRates = suggestions.Where(s => s.Rates.Length > 0).ToDictionary(s => s.Scenario.ToString(), s => s.Rates);
        await File.WriteAllTextAsync(Path.Combine(campaignDir, "suggested-scenario-rates.json"),
            JsonSerializer.Serialize(new { scenarioRates }, PerfJson.Options), ct);
        Console.WriteLine($"knee.csv and suggested-scenario-rates.json written to {campaignDir}");
    }

    private static async Task<(double? P50, double? P99, double? Achieved)> ClientNumbers(string dir, RunMeta meta, CancellationToken ct)
    {
        var window = meta.MeasureStartUtc is { } start && meta.MeasureEndUtc is { } end ? (end - start).TotalSeconds : 0;

        var k6 = Path.Combine(dir, "k6-summary.json");
        if (File.Exists(k6))
        {
            var metrics = JsonNode.Parse(await File.ReadAllTextAsync(k6, ct))?["metrics"];
            // k6's own rate divides by the whole test, warm-up included.
            var duration = K6Summary.Duration(metrics);
            return (Read(duration, "p(50)"), Read(duration, "p(99)"),
                Read(K6Summary.Metric(metrics, "iterations"), "count") is { } count && window > 0 ? count / window : null);
        }

        var stream = Path.Combine(dir, "stream-summary.json");
        if (File.Exists(stream))
        {
            var summary = JsonNode.Parse(await File.ReadAllTextAsync(stream, ct));
            var delivered = Read(summary, "delivered");
            return (Read(summary?["latencyMs"], "p50"), Read(summary?["latencyMs"], "p99"),
                delivered is { } d && window > 0 ? d / window : null);
        }

        return (null, null, null);
    }

    private static double? Read(JsonNode? node, string name) =>
        node?[name] is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;
}
