using Perf.Core;
using Perf.Core.Campaigns;

namespace Perf.UnitTests;

public class CampaignPlannerTests
{
    private static Campaign Campaign(int rounds = 3) => new(
        new CampaignTarget(new Uri("https://localhost:8443"), new Uri("https://localhost:9443"), true),
        rounds,
        [Transport.Rest, Transport.Grpc, Transport.GrpcWeb],
        [Scenario.S1, Scenario.S4],
        [100, 500],
        [true],
        30, 300,
        new CampaignUsers("user{0}@gmail.com", 1, 100, "x"),
        new CampaignExport(false, false));

    [Fact]
    public void ProducesEveryCombinationOnce()
    {
        var runs = CampaignPlanner.Expand(Campaign(), seed: 42);

        Assert.Equal(3 * 3 * 2 * 2, runs.Count);
        Assert.Equal(runs.Count, runs.Select(r => r.RunId).Distinct().Count());
    }

    [Fact]
    public void KeepsTransportBlocksTogetherWithinARound()
    {
        var round1 = CampaignPlanner.Expand(Campaign(), 42).Where(r => r.Round == 1).ToList();

        var blocks = round1.Select(r => r.Transport).Distinct().ToList();
        Assert.Equal(3, blocks.Count);
        // Each transport's runs are contiguous.
        foreach (var transport in blocks)
        {
            var indexes = round1.Select((r, i) => (r, i)).Where(x => x.r.Transport == transport).Select(x => x.i).ToList();
            Assert.Equal(indexes.Count - 1, indexes[^1] - indexes[0]);
        }
    }

    [Fact]
    public void SameSeedSameOrder_DifferentSeedsEventuallyDiffer()
    {
        var a = CampaignPlanner.Expand(Campaign(), 7).Select(r => r.RunId);
        var b = CampaignPlanner.Expand(Campaign(), 7).Select(r => r.RunId);
        Assert.Equal(a, b);

        var orders = Enumerable.Range(0, 20)
            .Select(seed => string.Join(",", CampaignPlanner.Expand(Campaign(1), seed).Select(r => r.Transport).Distinct()))
            .Distinct();
        Assert.True(orders.Count() > 1);
    }

    [Fact]
    public void RunIdIsReadable()
    {
        var run = CampaignPlanner.Expand(Campaign(1), 1).First(r => r.Transport == Transport.GrpcWeb && r.Scenario == Scenario.S4 && r.Rate == 500);

        Assert.Equal("r1-grpcweb-s4-500-otel", run.RunId);
    }
}
