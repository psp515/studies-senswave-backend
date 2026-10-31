using Perf.Core;

namespace PerfAgent;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public string Token { get; set; } = "";
    /// <summary>Admin connection string to the server's <c>postgres</c> database.</summary>
    public string PostgresAdmin { get; set; } = "";
    public string RunDatabase { get; set; } = "senswave_run";
    public string TemplateDatabase { get; set; } = "senswave_template";
    public string SeedDatabase { get; set; } = "senswave_seed";
    public string SeedContainer { get; set; } = "Senswave.Perf.Seed";
    public string DockerSocket { get; set; } = "/var/run/docker.sock";
    public int HealthTimeoutSeconds { get; set; } = 180;
    public int SeedTimeoutMinutes { get; set; } = 30;
    public Dictionary<Transport, string> ApiContainers { get; set; } = new()
    {
        [Transport.Rest] = "Senswave.Perf.Rest",
        [Transport.Grpc] = "Senswave.Perf.Grpc",
        [Transport.GrpcWeb] = "Senswave.Perf.GrpcWeb",
    };
}
