namespace PerfRunner;

public sealed record RunnerSettings(
    string AgentToken, string ResultsDir, string K6Dir, string K6Binary, string StreamBinary,
    string MqttHost, int MqttPort, string? MqttUser, string? MqttPassword, bool MqttTls,
    string? PromPushUrl, string? PromUser, string? GrafanaToken, string? TempoUrl, string? TempoUser, string? ArchiveSasUrl,
    string BrokerClientUser, string BrokerClientPassword, bool K6RemoteWrite)
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
        Env("GRAFANA_CLOUD_PROM_URL"), Env("GRAFANA_CLOUD_PROM_USER"), Env("GRAFANA_CLOUD_TOKEN"),
        Env("GRAFANA_CLOUD_TEMPO_URL"), Env("GRAFANA_CLOUD_TEMPO_USER"), Env("ARCHIVE_SAS_URL"),
        Env("BROKER_CLIENT_USERNAME") ?? "perf", Env("BROKER_CLIENT_PASSWORD") ?? "perf",
        Env("K6_REMOTE_WRITE") == "true");

    /// <summary>Prometheus query base: the remote write URL (.../api/prom/push) without /push.</summary>
    public string? PromQueryUrl => PromPushUrl is { } push && push.TrimEnd('/').EndsWith("/push", StringComparison.Ordinal)
        ? push.TrimEnd('/')[..^"/push".Length]
        : PromPushUrl;

    private static string? Env(string name) => Environment.GetEnvironmentVariable(name)?.Trim() is { Length: > 0 } v ? v : null;
    private static string Required(string name) => Env(name) ?? throw new InvalidOperationException($"Environment variable {name} is required.");
}
