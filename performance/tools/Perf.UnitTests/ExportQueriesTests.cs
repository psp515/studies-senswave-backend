using Perf.Core;
using Perf.Core.Export;
using Perf.Core.Runs;

namespace Perf.UnitTests;

public class ExportQueriesTests
{
    private static RunMeta Meta() => new(
        "r1-grpc-s1-100-otel", 1, 1, Transport.Grpc, Scenario.S1, 100, true, 1, "sha", "img", [],
        DateTimeOffset.Parse("2026-11-01T10:00:00Z"), DateTimeOffset.Parse("2026-11-01T10:00:30Z"),
        DateTimeOffset.Parse("2026-11-01T10:05:30Z"), "valid", []);

    [Fact]
    public void LatencyIsRawBucketCountsOverTheWholeWindow()
    {
        var latency = ExportQueries.For(Meta()).Where(q => q.File == "latency-buckets.csv").ToList();

        Assert.NotEmpty(latency);
        Assert.All(latency, q =>
        {
            Assert.False(q.Range);
            Assert.Contains("transport=\"Grpc\"", q.PromQl);
            Assert.Contains("[300s]", q.PromQl);
            Assert.Contains("sum by (le", q.PromQl);
            Assert.Contains("http_server_request_duration_seconds_bucket", q.PromQl);
        });
    }

    [Fact]
    public void NoPrecomputedQuantilesAreExported() =>
        Assert.DoesNotContain(ExportQueries.For(Meta()), q => q.PromQl.Contains("histogram_quantile"));

    [Fact]
    public void ContainerQueriesFilterByContainerName()
    {
        var cpu = ExportQueries.For(Meta()).First(q => q.File == "cpu.csv" && q.PromQl.Contains("container_cpu"));

        Assert.True(cpu.Range);
        Assert.Contains("name=\"Senswave.Perf.Grpc\"", cpu.PromQl);
    }

    [Fact]
    public void ThroughputIncludesStatusCodeBreakdown()
    {
        var throughput = ExportQueries.For(Meta()).Where(q => q.File == "throughput.csv").ToList();

        Assert.Contains(throughput, q => q.PromQl.Contains("sum by (http_response_status_code)") && q.PromQl.Contains("transport=\"Grpc\""));
    }

    [Fact]
    public void HttpServerSelectorsExcludeThePlainHttpHealthCheck()
    {
        var http = ExportQueries.For(Meta()).Where(q => q.PromQl.Contains("http_server_request_duration_seconds")).ToList();

        Assert.NotEmpty(http);
        Assert.All(http, q =>
        {
            Assert.Contains("url_scheme=\"https\"", q.PromQl);
            Assert.Contains("transport=\"Grpc\"", q.PromQl);
        });
    }

    [Fact]
    public void DatabaseQueriesSelectTheDatabaseUnderTest()
    {
        var db = ExportQueries.For(Meta()).Where(q => q.File == "db.csv").ToList();

        Assert.Contains(db, q => q.PromQl.Contains("pg_stat_database_xact_commit"));
        Assert.All(db, q =>
        {
            Assert.True(q.Range);
            Assert.Contains("datname=\"senswave_run\"", q.PromQl);
        });
    }

    [Fact]
    public void MetaWithoutMeasurementWindowDoesNotThrow()
    {
        var meta = Meta() with { MeasureStartUtc = null, MeasureEndUtc = null };

        Assert.NotEmpty(ExportQueries.For(meta));
    }
}
