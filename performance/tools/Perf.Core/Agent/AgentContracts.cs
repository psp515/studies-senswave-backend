namespace Perf.Core.Agent;

public sealed record ActivateRequest(Transport Transport, bool Otel, string RunId);

public sealed record ContainerInfo(
    string Name, string State, string? Health, string? CpusetCpus, double Cpus, long MemoryBytes, string? ImageId, string? GitSha);

public sealed record AgentStatus(Transport? Active, bool Healthy, IReadOnlyList<ContainerInfo> Containers);
