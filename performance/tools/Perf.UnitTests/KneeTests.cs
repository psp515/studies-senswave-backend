using Perf.Core;
using Perf.Core.Campaigns;
using Perf.Core.Knee;
using Perf.Core.Network;
using Perf.Core.Runs;

namespace Perf.UnitTests;

public class KneeTests
{
    private static KneeStep Step(Transport t, Scenario s, int rate, string status) => new(t, s, rate, status, 5, 20, rate);

    [Fact]
    public void KneeIsTheHighestRateOfTheValidPrefix()
    {
        var knees = KneeReport.Knees([
            Step(Transport.Rest, Scenario.S1, 400, RunStatus.Saturated),
            Step(Transport.Rest, Scenario.S1, 100, RunStatus.Valid),
            Step(Transport.Rest, Scenario.S1, 200, RunStatus.Valid),
            Step(Transport.Rest, Scenario.S1, 800, RunStatus.Valid),   // above a failed step: does not count
        ]);

        Assert.Equal(200, Assert.Single(knees).KneeRate);
    }

    [Fact]
    public void KneeIsZeroWhenTheFirstStepFails() =>
        Assert.Equal(0, Assert.Single(KneeReport.Knees([Step(Transport.Grpc, Scenario.S5, 50, RunStatus.Saturated)])).KneeRate);

    [Fact]
    public void SuggestionsUseTheLowestKneeAcrossTransports()
    {
        var suggestion = Assert.Single(KneeReport.Suggest([
            new KneeResult(Transport.Rest, Scenario.S2, 800),
            new KneeResult(Transport.Grpc, Scenario.S2, 1200),
            new KneeResult(Transport.GrpcWeb, Scenario.S2, 1000),
        ]));

        Assert.Equal(800, suggestion.LowestKnee);
        Assert.Equal([200, 600], suggestion.Rates);
    }

    [Theory]
    [InlineData(130, new[] { 30, 90 })]     // rounded down to a multiple of 10
    [InlineData(20, new[] { 10 })]          // at least 10, duplicates removed
    [InlineData(0, new int[0])]             // no valid step: nothing to suggest
    public void SuggestedRatesAreRounded(int knee, int[] expected) =>
        Assert.Equal(expected, Assert.Single(KneeReport.Suggest([new KneeResult(Transport.Rest, Scenario.S1, knee)])).Rates);

    [Fact]
    public void ClimbSkipsHigherStepsOfAStoppedLadderOnly()
    {
        var climb = new KneeClimb();
        var low = new RunSpec("a", 1, Transport.Rest, Scenario.S1, 100, true);
        var high = low with { RunId = "b", Rate = 200 };
        var otherTransport = low with { RunId = "c", Transport = Transport.Grpc, Rate = 200 };

        climb.Record(low, RunStatus.Saturated);

        Assert.True(climb.ShouldSkip(high));
        Assert.False(climb.ShouldSkip(otherTransport));
    }

    [Fact]
    public void ValidStepsKeepTheLadderGoing()
    {
        var climb = new KneeClimb();
        var low = new RunSpec("a", 1, Transport.Rest, Scenario.S1, 100, true);

        climb.Record(low, RunStatus.Valid);

        Assert.False(climb.ShouldSkip(low with { Rate = 200 }));
    }

    [Fact]
    public void PlannerUsesScenarioRatesAndClimbsAscendingInKneeMode()
    {
        var campaign = new Campaign(
            new CampaignTarget(new Uri("https://localhost:8443"), new Uri("https://localhost:9443"), true),
            1, [Transport.Rest], [Scenario.S1, Scenario.S5], [400, 100, 200], [true], 30, 90,
            new CampaignUsers("user{0}@gmail.com", 1, 100, "x"), new CampaignExport(false, false),
            new Dictionary<string, int[]> { ["S5"] = [50, 20] }, StopAfterSaturated: true);

        var runs = CampaignPlanner.Expand(campaign, 1);

        Assert.Equal([100, 200, 400], runs.Where(r => r.Scenario == Scenario.S1).Select(r => r.Rate));
        Assert.Equal([20, 50], runs.Where(r => r.Scenario == Scenario.S5).Select(r => r.Rate));
    }

    [Fact]
    public void RttStatsSummariseSamples()
    {
        var stats = NetworkStats.Rtt([10, 12, 11, 30, 13]);

        Assert.Equal(5, stats.Samples);
        Assert.Equal(10, stats.MinMs);
        Assert.Equal(12, stats.MedianMs);
        Assert.Equal(30, stats.MaxMs);
    }

    [Fact]
    public void MbpsUsesDecimalMegabits() => Assert.Equal(80, NetworkStats.Mbps(10_000_000, TimeSpan.FromSeconds(1)), 3);
}
