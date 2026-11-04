using System.Globalization;
using System.Text;

namespace Perf.Core.Knee;

/// <summary>One measured step of a knee ladder (the final attempt of a run id).</summary>
public sealed record KneeStep(Transport Transport, Scenario Scenario, int Rate, string Status,
    double? ClientP50Ms, double? ClientP99Ms, double? AchievedPerSecond);

public sealed record KneeResult(Transport Transport, Scenario Scenario, int KneeRate);

/// <param name="LowestKnee">The lowest knee across the transports of the scenario.</param>
/// <param name="Rates">The suggested fixed rates: 25% and 75% of <paramref name="LowestKnee"/>.</param>
public sealed record RateSuggestion(Scenario Scenario, int LowestKnee, int[] Rates);

public static class KneeReport
{
    public static readonly double[] Fractions = [0.25, 0.75];

    /// <summary>The knee is the highest rate whose step and every lower step are valid; 0 when the lowest step already failed.</summary>
    public static IReadOnlyList<KneeResult> Knees(IEnumerable<KneeStep> steps) =>
        steps.GroupBy(s => (s.Transport, s.Scenario))
            .Select(g =>
            {
                var knee = 0;
                foreach (var step in g.OrderBy(s => s.Rate))
                {
                    if (step.Status != Runs.RunStatus.Valid)
                        break;
                    knee = step.Rate;
                }

                return new KneeResult(g.Key.Transport, g.Key.Scenario, knee);
            })
            .OrderBy(k => k.Scenario).ThenBy(k => k.Transport)
            .ToList();

    /// <summary>Per scenario, 25% and 75% of the lowest knee across transports, rounded down to a multiple of 10 (at least 10).</summary>
    public static IReadOnlyList<RateSuggestion> Suggest(IEnumerable<KneeResult> knees) =>
        knees.GroupBy(k => k.Scenario)
            .Select(g =>
            {
                var lowest = g.Min(k => k.KneeRate);
                var rates = lowest == 0 ? [] : Fractions.Select(f => Math.Max(10, (int)(lowest * f) / 10 * 10)).Distinct().ToArray();
                return new RateSuggestion(g.Key, lowest, rates);
            })
            .OrderBy(s => s.Scenario)
            .ToList();

    public static string Csv(IEnumerable<KneeStep> steps)
    {
        var csv = new StringBuilder("transport,scenario,rate,status,client_p50_ms,client_p99_ms,achieved_per_s\n");
        foreach (var s in steps.OrderBy(s => s.Scenario).ThenBy(s => s.Transport).ThenBy(s => s.Rate))
            csv.Append(CultureInfo.InvariantCulture, $"{s.Transport},{s.Scenario},{s.Rate},{s.Status},{Num(s.ClientP50Ms)},{Num(s.ClientP99Ms)},{Num(s.AchievedPerSecond)}\n");
        return csv.ToString();
    }

    private static string Num(double? value) => value is { } v ? v.ToString("F2", CultureInfo.InvariantCulture) : "";
}
