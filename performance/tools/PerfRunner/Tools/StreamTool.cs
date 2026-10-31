using System.Diagnostics;
using System.Text.Json.Nodes;

namespace PerfRunner.Tools;

public sealed class StreamTool(RunnerSettings settings) : IRunTool
{
    public Task<RunToolResult> Verify(RunContext c, CancellationToken ct) => Execute(c, "verify", 5, 0, Path.Combine(c.RunDir, "verify-stream.json"), ct);

    public Task<RunToolResult> Run(RunContext c, int seconds, bool measured, CancellationToken ct) => measured
        ? Execute(c, "run", seconds, c.Campaign.WarmupSeconds, Path.Combine(c.RunDir, "stream-summary.json"), ct)
        : Task.FromResult(new RunToolResult(true, 0, 0, "warm-up runs inside the measured PerfStream process"));

    private async Task<RunToolResult> Execute(RunContext c, string mode, int seconds, int warmup, string summary, CancellationToken ct)
    {
        var start = new ProcessStartInfo(settings.StreamBinary) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var (key, value) in K6Tool.ToolEnvironment(c, mode, seconds))
            start.Environment[key] = value;
        start.Environment["SCENARIO"] = c.Run.Scenario.ToString();
        start.Environment["WARMUP"] = warmup.ToString();
        start.Environment["OUT"] = summary;
        start.Environment["MQTT_HOST"] = settings.MqttHost;
        start.Environment["MQTT_PORT"] = settings.MqttPort.ToString();
        start.Environment["MQTT_USERNAME"] = settings.MqttUser ?? "";
        start.Environment["MQTT_PASSWORD"] = settings.MqttPassword ?? "";
        start.Environment["MQTT_TLS"] = settings.MqttTls ? "true" : "false";

        Process process;
        try
        {
            process = Process.Start(start)!;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException or DirectoryNotFoundException)
        {
            return new RunToolResult(false, 0, 1, $"could not start '{settings.StreamBinary}': {ex.Message}");
        }

        using (process)
        {
            var stdout = process.StandardOutput.ReadToEndAsync(ct);
            var stderr = process.StandardError.ReadToEndAsync(ct);
            await K6Tool.WaitOrKill(process, ct);
            var output = await stdout + await stderr;

            if (!File.Exists(summary))
                return new RunToolResult(false, 0, 1, output);

            var s = JsonNode.Parse(await File.ReadAllTextAsync(summary, ct))!;
            var published = Math.Max(s["published"]!.GetValue<long>(), 1);
            var errors = s["lost"]!.GetValue<long>() + s["publishFailures"]!.GetValue<long>() + (s["subscriptionsEnded"]?.GetValue<long>() ?? 0);
            return new RunToolResult(process.ExitCode == 0, s["publishBehind"]!.GetValue<long>(), (double)errors / published, output,
                ReadTime(s, "measureStartUtc"), ReadTime(s, "measureEndUtc"));
        }
    }

    private static DateTimeOffset? ReadTime(JsonNode summary, string name) =>
        summary[name] is JsonValue v && v.TryGetValue<DateTimeOffset>(out var t) ? t : null;
}
