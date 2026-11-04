using Perf.Core.Runs;

namespace Perf.UnitTests;

public class GatesTests
{
    [Fact]
    public void PassesCleanRun() => Assert.True(Gates.Evaluate(new RunMetrics(0, 0.001, 0.5)).Passed);

    [Theory]
    [InlineData(3, 0.0, 0.1, "dropped_iterations")]
    [InlineData(0, 0.02, 0.1, "error rate")]
    [InlineData(0, 0.0, 0.9, "load generator CPU")]
    public void FailsAndNamesTheGate(long dropped, double errors, double cpu, string expected)
    {
        var result = Gates.Evaluate(new RunMetrics(dropped, errors, cpu));

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, f => f.Contains(expected));
    }

    [Theory]
    [InlineData(0.79, true)]
    [InlineData(0.80, false)]   // spec: "below 80%"
    [InlineData(0.81, false)]
    public void CpuGateBoundary(double cpu, bool passes) => Assert.Equal(passes, Gates.Evaluate(new RunMetrics(0, 0, cpu)).Passed);

    [Theory]
    [InlineData(0.0099, true)]
    [InlineData(0.01, false)]
    [InlineData(0.011, false)]
    public void ErrorRateGateBoundary(double errors, bool passes) => Assert.Equal(passes, Gates.Evaluate(new RunMetrics(0, errors, 0.1)).Passed);

    [Theory]
    [InlineData(30, 30, true)]
    [InlineData(30, 31.9, true)]     // within max(2 s, 5%)
    [InlineData(30, 32.1, false)]
    [InlineData(30, 27.9, false)]
    [InlineData(30, 160, false)]     // the evidence run: 160 s window for a 30 s measurement
    [InlineData(300, 314.9, true)]   // 5% = 15 s
    [InlineData(300, 315.1, false)]
    public void WindowMustMatchDuration(int expected, double window, bool passes)
    {
        var result = Gates.Evaluate(new RunMetrics(0, 0, 0.1, window, expected));

        Assert.Equal(passes, result.Passed);
        if (!passes)
            Assert.Contains(result.Failures, f => f.Contains("measured window"));
    }

    [Fact]
    public void WindowGateIsSkippedWithoutAWindow() => Assert.True(Gates.Evaluate(new RunMetrics(0, 0, 0.1, null, 30)).Passed);

    [Theory]
    [InlineData(5000.0, true)]
    [InlineData(5000.1, false)]
    [InlineData(13000.0, false)]
    public void DeliveryP99Bound(double p99, bool passes)
    {
        var result = Gates.Evaluate(new RunMetrics(0, 0, 0.1, DeliveryP99Ms: p99));

        Assert.Equal(passes, result.Passed);
        if (!passes)
            Assert.Contains(result.Failures, f => f.Contains("p99"));
    }

    [Fact]
    public void LostEventsInvalidateStreamRuns()
    {
        var result = Gates.Evaluate(new RunMetrics(0, 0, 0.1, Lost: 1));

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, f => f.Contains("FIFO"));
    }

    [Fact]
    public void CleanRunIsValid() => Assert.Equal(RunStatus.Valid, Gates.Evaluate(new RunMetrics(0, 0, 0.5)).Status);

    [Theory]
    [InlineData(3, 0.0, null, null, 0)]        // dropped iterations
    [InlineData(0, 0.02, null, null, 0)]       // errors
    [InlineData(0, 0.0, 160.0, null, 0)]       // stretched window
    [InlineData(0, 0.0, null, 13000.0, 0)]     // delivery backlog
    [InlineData(0, 0.0, null, null, 1)]        // lost events
    public void ServerSideFailuresWithAHealthyLoadGeneratorAreSaturated(long dropped, double errors, double? window, double? p99, long lost)
    {
        var result = Gates.Evaluate(new RunMetrics(dropped, errors, 0.5, window, 30, p99, lost));

        Assert.Equal(RunStatus.Saturated, result.Status);
        Assert.NotEmpty(result.Failures);
    }

    [Fact]
    public void OverloadedLoadGeneratorWinsOverServerFailures()
    {
        var result = Gates.Evaluate(new RunMetrics(330, 0.02, 0.9));

        Assert.Equal(RunStatus.InvalidLoadGenerator, result.Status);
        Assert.Contains(result.Failures, f => f.Contains("load generator CPU"));
        Assert.Contains(result.Failures, f => f.Contains("dropped_iterations"));
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("saturated", true)]
    [InlineData("invalid-loadgen", false)]
    [InlineData("failed-verification", false)]
    [InlineData("failed", false)]
    public void ValidAndSaturatedRunsAreFinalAndExported(string status, bool kept)
    {
        Assert.Equal(kept, RunStatus.IsFinal(status));
        Assert.Equal(kept, RunStatus.HasMeasurement(status));
    }
}
