namespace Perf.Core.Runs;

public sealed record RunMeta(
    string RunId,
    int Attempt,
    int Round,
    Transport Transport,
    Scenario Scenario,
    int Rate,
    bool Otel,
    int ShuffleSeed,
    string? GitSha,
    string? ImageId,
    IReadOnlyList<Agent.ContainerInfo> Containers,
    DateTimeOffset? WarmupStartUtc,
    DateTimeOffset? MeasureStartUtc,
    DateTimeOffset? MeasureEndUtc,
    string Status,                 // "valid" | "invalid" | "failed-verification" | "failed"
    IReadOnlyList<string> Failures);
