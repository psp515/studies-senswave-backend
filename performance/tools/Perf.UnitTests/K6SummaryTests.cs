using Perf.Core.Runs;

namespace Perf.UnitTests;

public class K6SummaryTests
{
    [Fact]
    public void ReadsDroppedAndCheckFailures()
    {
        const string json = """
        { "metrics": {
            "dropped_iterations": { "count": 12, "rate": 0.04 },
            "checks": { "passes": 990, "fails": 10, "value": 0.99 } } }
        """;

        var (dropped, errorRate) = K6Summary.Read(json);

        Assert.Equal(12, dropped);
        Assert.Equal(0.01, errorRate, 6);
    }

    [Fact]
    public void MissingDroppedMeansZero()
    {
        var (dropped, errorRate) = K6Summary.Read("""{ "metrics": { "checks": { "passes": 5, "fails": 0, "value": 1 } } }""");

        Assert.Equal(0, dropped);
        Assert.Equal(0, errorRate);
    }
}
