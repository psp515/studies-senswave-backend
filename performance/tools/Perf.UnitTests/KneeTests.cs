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
        Assert.Equal([200, 640], suggestion.Rates);
    }

    [Theory]
    [InlineData(130, new[] { 30, 100 })]    // rounded down to a multiple of 10
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

    /// <summary>Runs a search against a server that sustains everything up to <paramref name="capacity"/>.</summary>
    private static (List<int> Steps, KneeSearch Search) Search(int capacity, int start = 50, int max = 4000, double precision = 0.1)
    {
        var search = new KneeSearch(start, max, precision);
        var steps = new List<int>();
        while (search.Next is { } rate)
        {
            steps.Add(rate);
            search.Record(rate, rate <= capacity);
        }
        return (steps, search);
    }

    [Fact]
    public void DoublesThenBisectsThenRepeatsTheKnee()
    {
        var (steps, search) = Search(capacity: 700);

        Assert.Equal([50, 100, 200, 400, 800, 600, 700, 750, 700], steps);
        Assert.Equal(700, search.Knee);
        Assert.False(search.AtLeastMax);
    }

    [Fact]
    public void SustainedMaxIsALowerBoundAfterItsRepeat()
    {
        var (steps, search) = Search(capacity: 9000);

        Assert.Equal([50, 100, 200, 400, 800, 1600, 3200, 4000, 4000], steps);
        Assert.Equal(4000, search.Knee);
        Assert.True(search.AtLeastMax);
    }

    [Fact]
    public void BisectsBelowTheStartRate()
    {
        var (steps, search) = Search(capacity: 30);

        Assert.Equal([50, 20, 30, 40, 30], steps);
        Assert.Equal(30, search.Knee);
    }

    [Fact]
    public void NothingSustainedGivesKneeZero()
    {
        var (steps, search) = Search(capacity: 5);

        Assert.Equal([50, 20, 10], steps);
        Assert.Equal(0, search.Knee);
    }

    [Fact]
    public void AFailedRepeatMovesTheKneeDownAndRepeatsTheNewOne()
    {
        var search = new KneeSearch(100, 4000, 0.1);
        var steps = new List<int>();
        var firstTry700 = true;
        while (search.Next is { } rate)
        {
            steps.Add(rate);
            var sustained = rate <= 700 && !(rate == 700 && !firstTry700);
            firstTry700 = firstTry700 && rate != 700;
            search.Record(rate, sustained);
        }

        Assert.Equal(650, search.Knee);
        Assert.Equal(650, steps[^1]);
        Assert.Equal(2, steps.Count(s => s == 650));
    }

    [Fact]
    public void GeneratorLimitEndsTheSearchWithoutAFailedRate()
    {
        var search = new KneeSearch(50, 4000, 0.1);
        search.Record(50, true);

        search.StopAtGeneratorLimit();

        Assert.Null(search.Next);
        Assert.True(search.GeneratorLimited);
        Assert.Equal(50, search.Knee);
    }

    [Theory]
    [InlineData(80)]
    [InlineData(420)]
    [InlineData(999)]
    [InlineData(3333)]
    public void KneeIsWithinPrecisionOfTheCapacity(int capacity)
    {
        var (steps, search) = Search(capacity);

        Assert.InRange(capacity - search.Knee, 0, Math.Max(KneeSearch.RateStep, capacity * 0.11));
        Assert.All(steps, rate => Assert.Equal(0, rate % KneeSearch.RateStep));
        Assert.Equal(search.Knee, steps[^1]);
        Assert.True(steps.Count <= 12);
    }

    [Fact]
    public void KneeAtTheMaxIsALowerBound()
    {
        var knee = Assert.Single(KneeReport.Knees([Step(Transport.Rest, Scenario.S4, 4000, RunStatus.Valid)], max: 4000));

        Assert.Equal(4000, knee.KneeRate);
        Assert.True(knee.AtLeast);
    }

    [Fact]
    public void GeneratorLimitIsALowerBoundAndASaturatedStepIsNot()
    {
        var limited = Assert.Single(KneeReport.Knees([
            Step(Transport.Rest, Scenario.S4, 800, RunStatus.Valid),
            Step(Transport.Rest, Scenario.S4, 1600, RunStatus.InvalidLoadGenerator)]));
        var saturated = Assert.Single(KneeReport.Knees([
            Step(Transport.Rest, Scenario.S4, 800, RunStatus.Valid),
            Step(Transport.Rest, Scenario.S4, 1600, RunStatus.Saturated)]));

        Assert.True(limited.AtLeast);
        Assert.False(saturated.AtLeast);
    }

    [Fact]
    public void SuggestionIsALowerBoundOnlyWhenTheLowestKneeIs()
    {
        var bound = Assert.Single(KneeReport.Suggest([new KneeResult(Transport.Rest, Scenario.S4, 4000, AtLeast: true), new KneeResult(Transport.Grpc, Scenario.S4, 4000, AtLeast: true)]));
        var exact = Assert.Single(KneeReport.Suggest([new KneeResult(Transport.Rest, Scenario.S4, 900), new KneeResult(Transport.Grpc, Scenario.S4, 4000, AtLeast: true)]));

        Assert.True(bound.AtLeast);
        Assert.False(exact.AtLeast);
    }

    [Fact]
    public void PairsFollowTheShuffledTransportOrderAndRunIdsMatchExpand()
    {
        var campaign = new Campaign(
            new CampaignTarget(new Uri("https://localhost:8443"), new Uri("https://localhost:9443"), true),
            2, [Transport.Rest, Transport.Grpc, Transport.GrpcWeb], [Scenario.S1, Scenario.S6], [100], [true], 30, 60,
            new CampaignUsers("user{0}@gmail.com", 1, 100, "x"), new CampaignExport(false, false));

        var pairs = CampaignPlanner.Pairs(campaign, 7);
        var runs = CampaignPlanner.Expand(campaign, 7);

        Assert.Equal(runs.Select(r => (r.Round, r.Transport, r.Scenario)), pairs.Select(p => (p.Round, p.Transport, p.Scenario)));
        Assert.Equal(runs[0].RunId, pairs[0].At(100).RunId);
    }
}
