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
    public void EachScenarioAndRateRunsAllTransportsBackToBack()
    {
        var runs = CampaignPlanner.Expand(Campaign(), 42);

        foreach (var group in runs.Chunk(3))
        {
            Assert.Single(group.Select(r => (r.Round, r.Scenario, r.Rate)).Distinct());
            Assert.Equal(3, group.Select(r => r.Transport).Distinct().Count());
        }
    }

    [Fact]
    public void TransportsRunInTheCampaignOrderInEveryGroup()
    {
        var campaign = Campaign();

        Assert.All(CampaignPlanner.Expand(campaign, 42).Chunk(3), group => Assert.Equal(campaign.Transports, group.Select(r => r.Transport)));
    }

    [Fact]
    public void OrderDoesNotDependOnTheSeed() =>
        Assert.Equal(CampaignPlanner.Expand(Campaign(), 1).Select(r => r.RunId), CampaignPlanner.Expand(Campaign(), 99).Select(r => r.RunId));

    [Fact]
    public void RunIdIsReadable()
    {
        var run = CampaignPlanner.Expand(Campaign(1), 1).First(r => r.Transport == Transport.GrpcWeb && r.Scenario == Scenario.S4 && r.Rate == 500);

        Assert.Equal("r1-grpcweb-s4-500-otel", run.RunId);
    }
}
