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
}
