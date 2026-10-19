namespace Senswave.Infrastructure.Diagnostics.OpenTelemetry;

public class OpenTelemetryOptions
{
    public const string SectionName = "Diagnostics:OpenTelemetry";

    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Fraction of root traces to record, 0.0 to 1.0. Child spans follow the parent's decision.
    /// Metrics are not sampled and stay complete regardless of this value.
    /// </summary>
    public double TraceSamplingRatio { get; set; } = 1.0;

    /// <summary>
    /// How often aggregated metrics are exported, in seconds. Every request is counted regardless; this only sets the time resolution.
    /// </summary>
    public int MetricExportIntervalSeconds { get; set; } = 60;
}
