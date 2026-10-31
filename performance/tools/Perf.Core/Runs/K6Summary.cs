using System.Text.Json.Nodes;

namespace Perf.Core.Runs;

/// <summary>Reads k6 <c>--summary-export</c> output. Errors are failed checks, which works for every transport.</summary>
public static class K6Summary
{
    public static (long Dropped, double ErrorRate) Read(string json)
    {
        var metrics = JsonNode.Parse(json)?["metrics"] ?? throw new FormatException("k6 summary has no metrics.");

        var dropped = metrics["dropped_iterations"]?["count"]?.GetValue<double>() ?? 0;
        var passes = metrics["checks"]?["passes"]?.GetValue<double>() ?? 0;
        var fails = metrics["checks"]?["fails"]?.GetValue<double>() ?? 0;

        return ((long)dropped, passes + fails == 0 ? 1.0 : fails / (passes + fails));
    }
}
