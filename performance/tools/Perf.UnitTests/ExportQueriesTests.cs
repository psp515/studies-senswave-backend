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
    public void LatencyIsOneWholeWindowQuantilePerPercentile()
    {
        var latency = ExportQueries.For(Meta()).Where(q => q.File == "latency.csv").ToList();

        Assert.Equal(3, latency.Count);
        Assert.All(latency, q =>
        {
            Assert.False(q.Range);
            Assert.Contains("run_id=\"r1-grpc-s1-100-otel\"", q.PromQl);
            Assert.Contains("[300s]", q.PromQl);
            Assert.Contains("http_server_request_duration_seconds_bucket", q.PromQl);
        });
    }

    [Fact]
    public void ContainerQueriesFilterByContainerName()
    {
        var cpu = ExportQueries.For(Meta()).First(q => q.File == "cpu.csv" && q.PromQl.Contains("container_cpu"));

        Assert.True(cpu.Range);
        Assert.Contains("name=\"Senswave.Perf.Grpc\"", cpu.PromQl);
    }
}
