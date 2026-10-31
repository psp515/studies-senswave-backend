namespace Perf.Core.Campaigns;

public sealed record RunSpec(string RunId, int Round, Transport Transport, Scenario Scenario, int Rate, bool Otel);

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
            var random = new Random(HashCode.Combine(seed, round));
            var transports = campaign.Transports.OrderBy(_ => random.Next()).ToArray();

            foreach (var otel in campaign.Otel)
            foreach (var transport in transports)
            foreach (var scenario in campaign.Scenarios)
            foreach (var rate in campaign.Rates)
                runs.Add(new RunSpec(RunId(round, transport, scenario, rate, otel), round, transport, scenario, rate, otel));
        }

        return runs;
    }

    private static string RunId(int round, Transport transport, Scenario scenario, int rate, bool otel) =>
        $"r{round}-{transport.ToString().ToLowerInvariant()}-{scenario.ToString().ToLowerInvariant()}-{rate}-{(otel ? "otel" : "nootel")}";
}
