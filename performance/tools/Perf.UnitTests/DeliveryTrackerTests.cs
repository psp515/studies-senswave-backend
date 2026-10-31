using Perf.Core.Streams;

namespace Perf.UnitTests;

public class DeliveryTrackerTests
{
    private static long Ms(double ms) => TimeSpan.FromMilliseconds(ms).Ticks;

    [Fact]
    public void MatchesDeliveriesToOldestPendingPublishPerDevice()
    {
        var tracker = new DeliveryTracker();
        tracker.Published("d1", Ms(0), measured: true);
        tracker.Published("d1", Ms(10), measured: true);
        tracker.Published("d2", Ms(5), measured: true);

        Assert.True(tracker.Delivered("d1", Ms(20)));   // matches the publish at 0 → 20 ms
        Assert.True(tracker.Delivered("d2", Ms(8)));    // 3 ms
        Assert.True(tracker.Delivered("d1", Ms(25)));   // matches the publish at 10 → 15 ms

        var result = tracker.Finish(Ms(10_000), TimeSpan.FromSeconds(5));
        Assert.Equal(3, result.Published);
        Assert.Equal(3, result.Delivered);
        Assert.Equal(0, result.Lost);
        Assert.Equal(new[] { 3.0, 15.0, 20.0 }, result.LatenciesMs.Order().ToArray());
    }

    [Fact]
    public void WarmupPublishesAreConsumedButNotRecorded_UnmatchedAndLostAreCounted()
    {
        var tracker = new DeliveryTracker();
        tracker.Published("d1", Ms(0), measured: false);
        tracker.Published("d1", Ms(1), measured: true);
        tracker.Published("d1", Ms(2), measured: true);

        tracker.Delivered("d1", Ms(5));                 // warm-up publish, not recorded
        tracker.Delivered("d1", Ms(6));                 // 5 ms, recorded
        Assert.False(tracker.Delivered("d9", Ms(7)));   // nothing pending → unmatched

        var result = tracker.Finish(Ms(10_000), TimeSpan.FromSeconds(5));
        Assert.Equal(2, result.Published);
        Assert.Equal(1, result.Delivered);
        Assert.Equal(1, result.Lost);                   // publish at 2 ms never delivered
        Assert.Equal(1, result.Unmatched);
        Assert.Equal(new[] { 5.0 }, result.LatenciesMs);
    }

    [Fact]
    public void PercentilesUseNearestRank()
    {
        var values = Enumerable.Range(1, 100).Select(i => (double)i).ToArray();

        Assert.Equal(new[] { 50.0, 95.0, 99.0, 100.0 }, Percentiles.Of(values, 50, 95, 99, 100));
    }
}
