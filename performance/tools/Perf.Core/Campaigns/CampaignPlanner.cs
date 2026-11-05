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
    /// Rounds in order; within a round, the transport order is shuffled (seeded per round) and each transport's
    /// runs stay together, so transports interleave across rounds and noise spreads evenly.
    /// </summary>
    public static IReadOnlyList<RunSpec> Expand(Campaign campaign, int seed)
    {
        var runs = new List<RunSpec>();

        for (var round = 1; round <= campaign.Rounds; round++)
        {
            var random = new Random(unchecked(seed * 31 + round));
            var transports = Shuffle(campaign.Transports, random);

            foreach (var otel in campaign.Otel)
            foreach (var transport in transports)
            foreach (var scenario in campaign.Scenarios)
            // Knee mode climbs each scenario's rates from the lowest, so the runner can stop at the first saturated step.
            foreach (var rate in campaign.StopAfterSaturated ? campaign.RatesFor(scenario).Order().ToArray() : campaign.RatesFor(scenario))
                runs.Add(new RunSpec(RunId(round, transport, scenario, rate, otel), round, transport, scenario, rate, otel));
        }

        return runs;
    }

    /// <summary>The (round, OTel, transport, scenario) pairs in the same order as <see cref="Expand"/>, without rates.</summary>
    public static IReadOnlyList<RunPair> Pairs(Campaign campaign, int seed)
    {
        var pairs = new List<RunPair>();

        for (var round = 1; round <= campaign.Rounds; round++)
        {
            var random = new Random(unchecked(seed * 31 + round));
            var transports = Shuffle(campaign.Transports, random);

            foreach (var otel in campaign.Otel)
            foreach (var transport in transports)
            foreach (var scenario in campaign.Scenarios)
                pairs.Add(new RunPair(round, transport, scenario, otel));
        }

        return pairs;
    }

    private static Transport[] Shuffle(Transport[] source, Random random)
    {
        var items = (Transport[])source.Clone();
        for (var i = items.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }

        return items;
    }

    public static string RunId(int round, Transport transport, Scenario scenario, int rate, bool otel) =>
        $"r{round}-{transport.ToString().ToLowerInvariant()}-{scenario.ToString().ToLowerInvariant()}-{rate}-{(otel ? "otel" : "nootel")}";
}
