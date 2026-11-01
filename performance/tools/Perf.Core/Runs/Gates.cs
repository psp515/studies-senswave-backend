namespace Perf.Core.Runs;

/// <param name="MeasuredWindowSeconds">Wall-clock length of the measurement; null skips the window gate.</param>
/// <param name="ExpectedWindowSeconds">The campaign's measurement duration.</param>
/// <param name="DeliveryP99Ms">Delivery latency p99 of stream scenarios; null for k6 scenarios.</param>
/// <param name="Lost">Stream events that were never delivered (stream scenarios only).</param>
public sealed record RunMetrics(long DroppedIterations, double ErrorRate, double LoadGeneratorCpu,
    double? MeasuredWindowSeconds = null, int ExpectedWindowSeconds = 0, double? DeliveryP99Ms = null, long Lost = 0);

public sealed record GateResult(bool Passed, IReadOnlyList<string> Failures);

public static class Gates
{
    /// <summary>A stream run whose delivery p99 exceeds this has an unbounded backlog, not a latency.</summary>
    public const double MaxDeliveryP99Ms = 5000;

    /// <summary>Allowed difference between the measured window and the campaign duration: max(2 s, 5%).</summary>
    public static double WindowToleranceSeconds(int expectedSeconds) => Math.Max(2.0, expectedSeconds * 0.05);

    public static GateResult Evaluate(RunMetrics metrics, double maxErrorRate = 0.01, double maxLoadCpu = 0.80)
    {
        var failures = new List<string>();

        if (metrics.DroppedIterations > 0)
            failures.Add($"dropped_iterations = {metrics.DroppedIterations} (load generator could not keep the rate)");
        if (metrics.ErrorRate >= maxErrorRate)
            failures.Add($"error rate {metrics.ErrorRate:P2} >= {maxErrorRate:P0}");
        // Spec: the load generator must stay below 80%, so 80% itself fails.
        if (metrics.LoadGeneratorCpu >= maxLoadCpu)
            failures.Add($"load generator CPU {metrics.LoadGeneratorCpu:P0} >= {maxLoadCpu:P0}");
        if (metrics.MeasuredWindowSeconds is { } window
            && Math.Abs(window - metrics.ExpectedWindowSeconds) > WindowToleranceSeconds(metrics.ExpectedWindowSeconds))
            failures.Add($"measured window {window:F1} s differs from the {metrics.ExpectedWindowSeconds} s duration by more than {WindowToleranceSeconds(metrics.ExpectedWindowSeconds):F1} s (backlog or stall)");
        if (metrics.DeliveryP99Ms is { } p99 && p99 > MaxDeliveryP99Ms)
            failures.Add($"delivery latency p99 {p99:F0} ms > {MaxDeliveryP99Ms:F0} ms (backlog)");
        if (metrics.Lost > 0)
            failures.Add($"lost = {metrics.Lost} events (lost events bias FIFO latency matching)");

        return new GateResult(failures.Count == 0, failures);
    }
}
