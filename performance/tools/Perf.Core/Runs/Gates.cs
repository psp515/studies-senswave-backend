namespace Perf.Core.Runs;

public sealed record RunMetrics(long DroppedIterations, double ErrorRate, double LoadGeneratorCpu);

public sealed record GateResult(bool Passed, IReadOnlyList<string> Failures);

public static class Gates
{
    public static GateResult Evaluate(RunMetrics metrics, double maxErrorRate = 0.01, double maxLoadCpu = 0.80)
    {
        var failures = new List<string>();

        if (metrics.DroppedIterations > 0)
            failures.Add($"dropped_iterations = {metrics.DroppedIterations} (load generator could not keep the rate)");
        if (metrics.ErrorRate >= maxErrorRate)
            failures.Add($"error rate {metrics.ErrorRate:P2} >= {maxErrorRate:P0}");
        if (metrics.LoadGeneratorCpu > maxLoadCpu)
            failures.Add($"load generator CPU {metrics.LoadGeneratorCpu:P0} > {maxLoadCpu:P0}");

        return new GateResult(failures.Count == 0, failures);
    }
}
