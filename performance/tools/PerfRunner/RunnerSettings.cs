namespace PerfRunner;

public sealed record RunnerSettings(
    string AgentToken, string ResultsDir, string K6Dir, string K6Binary, string StreamBinary,
    string MqttHost, int MqttPort, string? MqttUser, string? MqttPassword, bool MqttTls,
    string? PromUrl, string? PromUser, string? GrafanaReadToken, string? TempoUrl, string? TempoUser, string? ArchiveSasUrl,
    string BrokerClientUser, string BrokerClientPassword)
{
    public static RunnerSettings FromEnvironment() => new(
        Required("AGENT_TOKEN"),
        Env("RESULTS_DIR") ?? "/work/performance/results",
        Env("K6_DIR") ?? "/work/performance/k6",
        Env("K6_BINARY") ?? "k6",
        Env("STREAM_BINARY") ?? "/app/stream/PerfStream",
        Env("MQTT_HOST") ?? "host.docker.internal",
        int.Parse(Env("MQTT_PORT") ?? "1883"),
        Env("MQTT_USERNAME"), Env("MQTT_PASSWORD"), Env("MQTT_TLS") == "true",
        Env("GRAFANA_PROM_URL"), Env("GRAFANA_PROM_USER"), Env("GRAFANA_READ_TOKEN"),
        Env("GRAFANA_TEMPO_URL"), Env("GRAFANA_TEMPO_USER"), Env("ARCHIVE_SAS_URL"),
        Env("BROKER_CLIENT_USERNAME") ?? "perf", Env("BROKER_CLIENT_PASSWORD") ?? "perf");

    private static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;
    private static string Required(string name) => Env(name) ?? throw new InvalidOperationException($"Environment variable {name} is required.");
}
