namespace Perf.Core.Runs;

/// <param name="MeasuredWindowSeconds">Wall-clock length of the measurement; null skips the window gate.</param>
/// <param name="ExpectedWindowSeconds">The campaign's measurement duration.</param>
/// <param name="DeliveryP99Ms">Delivery latency p99 of stream scenarios; null for k6 scenarios.</param>
/// <param name="Lost">Stream events that were never delivered (stream scenarios only).</param>
public sealed record RunMetrics(long DroppedIterations, double ErrorRate, double LoadGeneratorCpu,
    double? MeasuredWindowSeconds = null, int ExpectedWindowSeconds = 0, double? DeliveryP99Ms = null, long Lost = 0);

/// <param name="Status">A <see cref="RunStatus"/> value: valid, saturated or invalid-loadgen.</param>
public sealed record GateResult(string Status, IReadOnlyList<string> Failures)
{
    public bool Passed => Status == RunStatus.Valid;
}

public static class Gates
{
    /// <summary>A stream run whose delivery p99 exceeds this has an unbounded backlog, not a latency.</summary>
    public const double MaxDeliveryP99Ms = 5000;

    /// <summary>Allowed difference between the measured window and the campaign duration: max(2 s, 5%).</summary>
    public static double WindowToleranceSeconds(int expectedSeconds) => Math.Max(2.0, expectedSeconds * 0.05);

    /// <summary>
    /// An overloaded load generator invalidates the run (<see cref="RunStatus.InvalidLoadGenerator"/>). Otherwise any failed
    /// gate means the system under test did not sustain the rate (<see cref="RunStatus.Saturated"/>), which is a result.
    /// </summary>
    public static GateResult Evaluate(RunMetrics metrics, double maxErrorRate = 0.01, double maxLoadCpu = 0.80)
    {
        var server = new List<string>();

        if (metrics.DroppedIterations > 0)
            server.Add($"dropped_iterations = {metrics.DroppedIterations} (the rate was not sustained)");
        if (metrics.ErrorRate >= maxErrorRate)
            server.Add($"error rate {metrics.ErrorRate:P2} >= {maxErrorRate:P0}");
        if (metrics.MeasuredWindowSeconds is { } window
            && Math.Abs(window - metrics.ExpectedWindowSeconds) > WindowToleranceSeconds(metrics.ExpectedWindowSeconds))
            server.Add($"measured window {window:F1} s differs from the {metrics.ExpectedWindowSeconds} s duration by more than {WindowToleranceSeconds(metrics.ExpectedWindowSeconds):F1} s (backlog or stall)");
        if (metrics.DeliveryP99Ms is { } p99 && p99 > MaxDeliveryP99Ms)
            server.Add($"delivery latency p99 {p99:F0} ms > {MaxDeliveryP99Ms:F0} ms (backlog)");
        if (metrics.Lost > 0)
            server.Add($"lost = {metrics.Lost} events (lost events bias FIFO latency matching)");

        // Spec: the load generator must stay below 80%, so 80% itself fails.
        if (metrics.LoadGeneratorCpu >= maxLoadCpu)
            return new GateResult(RunStatus.InvalidLoadGenerator,
                [$"load generator CPU {metrics.LoadGeneratorCpu:P0} >= {maxLoadCpu:P0}", .. server]);

        return new GateResult(server.Count == 0 ? RunStatus.Valid : RunStatus.Saturated, server);
    }
}
