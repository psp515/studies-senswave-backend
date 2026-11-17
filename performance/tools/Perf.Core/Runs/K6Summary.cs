using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Perf.Core.Runs;

/// <summary>Reads k6 <c>--summary-export</c> output. Errors are failed checks, which works for every transport.</summary>
public sealed record K6Numbers(long Dropped, double ErrorRate, long Iterations, double? P99Ms);

public static partial class K6Summary
{
    public static K6Numbers Read(string json)
    {
        var metrics = JsonNode.Parse(json)?["metrics"] ?? throw new FormatException("k6 summary has no metrics.");

        var dropped = Metric(metrics, "dropped_iterations")?["count"]?.GetValue<double>() ?? 0;
        var checks = Metric(metrics, "checks");
        var passes = checks?["passes"]?.GetValue<double>() ?? 0;
        var fails = checks?["fails"]?.GetValue<double>() ?? 0;

        var iterations = Metric(metrics, "iterations")?["count"]?.GetValue<double>() ?? 0;

        return new K6Numbers((long)dropped, passes + fails == 0 ? 1.0 : fails / (passes + fails), (long)iterations, Duration(metrics)?["p(99)"]?.GetValue<double>());
    }

    /// <summary>The request duration of the transport under test: both metrics are exported, but only that one has samples.</summary>
    public static JsonNode? Duration(JsonNode? metrics) =>
        Metric(metrics, "grpc_req_duration") is { } grpc && grpc["max"]?.GetValue<double>() > 0 ? grpc : Metric(metrics, "http_req_duration");

    /// <summary>A metric of the measurement phase, or the whole-test metric when there is no such phase (verification).</summary>
    public static JsonNode? Metric(JsonNode? metrics, string name) => metrics?[$"{name}{{scenario:measure}}"] ?? metrics?[name];

    /// <summary>When the measurement phase started, from the marker the k6 scripts log; null when no iteration ran.</summary>
    public static DateTimeOffset? MeasureStart(string output) =>
        MeasureStartMarker().Match(output) is { Success: true } match
            ? DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(match.Groups[1].Value))
            : null;

    [GeneratedRegex(@"PERF_MEASURE_START (\d+)")]
    private static partial Regex MeasureStartMarker();
}
