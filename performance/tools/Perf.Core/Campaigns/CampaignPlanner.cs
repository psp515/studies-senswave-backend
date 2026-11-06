namespace Perf.Core.Campaigns;

public sealed record RunSpec(string RunId, int Round, Transport Transport, Scenario Scenario, int Rate, bool Otel);

/// <summary>One transport and scenario of a round: the unit of a knee search.</summary>
public sealed record RunPair(int Round, Transport Transport, Scenario Scenario, bool Otel)
{
    public RunSpec At(int rate) => new(CampaignPlanner.RunId(Round, Transport, Scenario, rate, Otel), Round, Transport, Scenario, rate, Otel);
}

public static class CampaignPlanner
{
    /// <summary>
    /// Rounds in order. Within a round the runs are interleaved: for every scenario and rate, the transports run back to
    /// back in the campaign's fixed order. So the transports compared at the same scenario and rate are measured within
    /// minutes of each other, under the same conditions. <paramref name="seed"/> is kept for the record only.
    /// </summary>
    public static IReadOnlyList<RunSpec> Expand(Campaign campaign, int seed) =>
        // Ladder knee mode climbs each scenario's rates from the lowest, so the runner can stop at the first saturated step.
        Interleave(campaign, seed, scenario => campaign.StopAfterSaturated ? campaign.RatesFor(scenario).Order().ToArray() : campaign.RatesFor(scenario))
            .Select(x => new RunSpec(RunId(x.Round, x.Transport, x.Scenario, x.Rate, x.Otel), x.Round, x.Transport, x.Scenario, x.Rate, x.Otel))
            .ToList();

    /// <summary>
    /// The (round, OTel, scenario, transport) pairs of a knee search, interleaved like <see cref="Expand"/>: per scenario the
    /// transports follow each other in the campaign's order. With a single rate the order equals <see cref="Expand"/>.
    /// </summary>
    public static IReadOnlyList<RunPair> Pairs(Campaign campaign, int seed) =>
        Interleave(campaign, seed, _ => [0])
            .Select(x => new RunPair(x.Round, x.Transport, x.Scenario, x.Otel))
            .ToList();

    private static IEnumerable<(int Round, bool Otel, Scenario Scenario, int Rate, Transport Transport)> Interleave(
        Campaign campaign, int seed, Func<Scenario, int[]> rates)
    {
        for (var round = 1; round <= campaign.Rounds; round++)
        foreach (var otel in campaign.Otel)
        foreach (var scenario in campaign.Scenarios)
        foreach (var rate in rates(scenario))
        foreach (var transport in campaign.Transports)
            yield return (round, otel, scenario, rate, transport);
    }

    public static string RunId(int round, Transport transport, Scenario scenario, int rate, bool otel) =>
        $"r{round}-{transport.ToString().ToLowerInvariant()}-{scenario.ToString().ToLowerInvariant()}-{rate}-{(otel ? "otel" : "nootel")}";
}
