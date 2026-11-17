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

        var numbers = K6Summary.Read(json);

        Assert.Equal(12, numbers.Dropped);
        Assert.Equal(0.01, numbers.ErrorRate, 6);
    }

    [Fact]
    public void PrefersTheMeasurementPhaseOverTheWholeTest()
    {
        const string json = """
        { "metrics": {
            "dropped_iterations": { "count": 500 },
            "dropped_iterations{scenario:measure}": { "count": 3 },
            "checks": { "passes": 50, "fails": 50 },
            "checks{scenario:measure}": { "passes": 100, "fails": 0 } } }
        """;

        var numbers = K6Summary.Read(json);

        Assert.Equal(3, numbers.Dropped);
        Assert.Equal(0, numbers.ErrorRate);
    }

    [Theory]
    [InlineData("""{ "metrics": { "http_req_duration{scenario:measure}": { "max": 90, "p(99)": 40 }, "grpc_req_duration{scenario:measure}": { "max": 0 }, "iterations{scenario:measure}": { "count": 7 } } }""", 40)]
    [InlineData("""{ "metrics": { "http_req_duration{scenario:measure}": { "max": 0, "p(99)": 0 }, "grpc_req_duration{scenario:measure}": { "max": 90, "p(99)": 60 }, "iterations{scenario:measure}": { "count": 7 } } }""", 60)]
    public void ReadsIterationsAndTheP99OfTheTransportUnderTest(string json, double p99)
    {
        var numbers = K6Summary.Read(json);

        Assert.Equal(7, numbers.Iterations);
        Assert.Equal(p99, numbers.P99Ms);
    }

    [Fact]
    public void ReadsTheMeasureStartMarker()
    {
        const string output = "time=\"2026-11-06T12:00:00Z\" level=info msg=\"PERF_MEASURE_START 1793966400123\" source=console";

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1793966400123), K6Summary.MeasureStart(output));
        Assert.Null(K6Summary.MeasureStart("no marker"));
    }

    [Fact]
    public void MissingDroppedMeansZero()
    {
        var numbers = K6Summary.Read("""{ "metrics": { "checks": { "passes": 5, "fails": 0, "value": 1 } } }""");

        Assert.Equal(0, numbers.Dropped);
        Assert.Equal(0, numbers.ErrorRate);
    }
}
