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

/// <summary>Rules for the agent bearer token: it controls Docker and the databases, so a template or short value is refused.</summary>
public static class AgentTokenPolicy
{
    public const string TemplateValue = "change-me-to-a-long-random-string";
    public const int MinLength = 32;

    /// <summary>Returns why the token is unusable, or null when it is acceptable.</summary>
    public static string? Problem(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return "Agent__Token is not set";
        if (token == TemplateValue)
            return "Agent__Token is still the template value";
        if (token.Length < MinLength)
            return $"Agent__Token is shorter than {MinLength} characters";
        return null;
    }
}
