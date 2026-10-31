using Perf.Core;

namespace PerfStream;

public sealed record StreamSettings(
    Scenario Scenario, Transport Transport, Uri BaseUrl, string Mode, int Rate, int DurationSeconds, int WarmupSeconds,
    string DataFile, bool Insecure, string Out, string MqttHost, int MqttPort, string? MqttUser, string? MqttPassword, bool MqttTls)
{
    public static StreamSettings FromEnvironment() => new(
        Enum.Parse<Scenario>(Env("SCENARIO")), Enum.Parse<Transport>(Env("TRANSPORT"), ignoreCase: true), new Uri(Env("BASE_URL").TrimEnd('/') + "/"),
        Env("MODE"), int.Parse(Env("RATE")), int.Parse(Env("DURATION").TrimEnd('s')), int.Parse(Environment.GetEnvironmentVariable("WARMUP") ?? "0"),
        Env("DATA_FILE"), Environment.GetEnvironmentVariable("INSECURE") == "true", Env("OUT"),
        Environment.GetEnvironmentVariable("MQTT_HOST") ?? "localhost", int.Parse(Environment.GetEnvironmentVariable("MQTT_PORT") ?? "1883"),
        Environment.GetEnvironmentVariable("MQTT_USERNAME"), Environment.GetEnvironmentVariable("MQTT_PASSWORD"),
        Environment.GetEnvironmentVariable("MQTT_TLS") == "true");

    private static string Env(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"{name} is required.");
}
