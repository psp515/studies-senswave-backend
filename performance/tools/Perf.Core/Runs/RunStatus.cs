namespace Perf.Core.Runs;

/// <summary>The <c>status</c> values written to <c>meta.json</c>.</summary>
public static class RunStatus
{
    /// <summary>Every gate passed.</summary>
    public const string Valid = "valid";

    /// <summary>
    /// The load generator was healthy, but the system under test did not sustain the rate (dropped iterations, errors,
    /// a stretched window, a delivery backlog or lost events). A result, not a failure: it is kept, exported and not retried.
    /// </summary>
    public const string Saturated = "saturated";

    /// <summary>The load generator itself was overloaded, so the measurement says nothing about the server. Retried once.</summary>
    public const string InvalidLoadGenerator = "invalid-loadgen";

    public const string FailedVerification = "failed-verification";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";

    /// <summary>Statuses whose measurement window holds usable data (server export, analysis).</summary>
    public static bool HasMeasurement(string status) => status is Valid or Saturated;

    /// <summary>Statuses that are final for a run id: no second attempt.</summary>
    public static bool IsFinal(string status) => status is Valid or Saturated;
}
