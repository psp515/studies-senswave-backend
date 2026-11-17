namespace PerfStream;

public sealed record Percentile3(double P50, double P95, double Max);
public sealed record Percentile5(double P50, double P90, double P95, double P99, double Max);

public sealed record StreamSummary(
    string Scenario, string Transport, int Rate, int Subscribers, Percentile3 ConnectSetupMs,
    long Published, long Delivered, long Lost, long Unmatched, long PublishFailures, long PublishBehind,
    int SubscriptionsEnded, DateTimeOffset MeasureStartUtc, DateTimeOffset MeasureEndUtc, Percentile5 LatencyMs);
