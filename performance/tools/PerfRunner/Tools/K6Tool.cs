using System.Diagnostics;
using Perf.Core;
using Perf.Core.Campaigns;
using Perf.Core.Runs;

namespace PerfRunner.Tools;

public sealed record RunContext(RunSpec Run, Campaign Campaign, string RunDir, string DataFile);

/// <param name="MeasureStartUtc">Set by tools that run warm-up and measurement in one process (PerfStream).</param>
public sealed record RunToolResult(bool Success, long Dropped, double ErrorRate, string Output,
    DateTimeOffset? MeasureStartUtc = null, DateTimeOffset? MeasureEndUtc = null,
    double? DeliveryP99Ms = null, long Lost = 0);

public interface IRunTool
{
    Task<RunToolResult> Verify(RunContext context, CancellationToken ct);
    Task<RunToolResult> Run(RunContext context, int seconds, bool measured, CancellationToken ct);
}

public sealed class K6Tool(RunnerSettings settings) : IRunTool
{
    public Task<RunToolResult> Verify(RunContext c, CancellationToken ct) => Execute(c, "verify", 1, Path.Combine(c.RunDir, "verify-summary.json"), ct);

    public Task<RunToolResult> Run(RunContext c, int seconds, bool measured, CancellationToken ct) =>
        Execute(c, "run", seconds, Path.Combine(c.RunDir, measured ? "k6-summary.json" : "warmup-summary.json"), ct, measured);

    private async Task<RunToolResult> Execute(RunContext c, string mode, int seconds, string summary, CancellationToken ct, bool measured = false)
    {
        var script = Path.Combine(settings.K6Dir, $"{c.Run.Scenario.ToString().ToLowerInvariant()}.js");
        var args = new List<string> { "run", "--quiet", "--summary-export", summary, "--tag", $"run_id={c.Run.RunId}", "--tag", $"transport={c.Run.Transport}",
            "--tag", $"scenario={c.Run.Scenario}", "--tag", $"rate={c.Run.Rate}" };
        if (measured && Environment.GetEnvironmentVariable("K6_PROMETHEUS_RW_SERVER_URL") is { Length: > 0 })
            args.AddRange(["-o", "experimental-prometheus-rw"]);
        if (measured && c.Campaign.Export.RawK6)
            args.AddRange(["--out", $"csv={Path.Combine(c.RunDir, "k6-raw.csv.gz")}"]);
        args.Add(script);

        var start = new ProcessStartInfo(settings.K6Binary) { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = settings.K6Dir };
        args.ForEach(start.ArgumentList.Add);
        foreach (var (key, value) in ToolEnvironment(c, mode, seconds))
            start.Environment[key] = value;

        Process process;
        try
        {
            process = Process.Start(start)!;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException or DirectoryNotFoundException)
        {
            return new RunToolResult(false, 0, 1, $"could not start '{settings.K6Binary}': {ex.Message}");
        }

        using (process)
        {
            var stdout = process.StandardOutput.ReadToEndAsync(ct);
            var stderr = process.StandardError.ReadToEndAsync(ct);
            await K6Tool.WaitOrKill(process, ct);
            var output = await stdout + await stderr;

            if (!File.Exists(summary))
                return new RunToolResult(false, 0, 1, output);

            var (dropped, errorRate) = K6Summary.Read(await File.ReadAllTextAsync(summary, ct));
            return new RunToolResult(process.ExitCode == 0 || mode == "run", dropped, errorRate, output);
        }
    }

    internal static async Task WaitOrKill(Process process, CancellationToken ct)
    {
        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw;
        }
    }

    internal static IEnumerable<(string, string)> ToolEnvironment(RunContext c, string mode, int seconds) =>
    [
        ("TRANSPORT", c.Run.Transport.ToString().ToLowerInvariant()),
        ("BASE_URL", c.Campaign.Target.Api.ToString().TrimEnd('/')),
        ("MODE", mode),
        ("RATE", c.Run.Rate.ToString()),
        ("DURATION", $"{seconds}s"),
        ("DATA_FILE", c.DataFile),
        ("INSECURE", c.Campaign.Target.InsecureTls ? "true" : "false"),
        ("RUN_ID", c.Run.RunId),
    ];
}
