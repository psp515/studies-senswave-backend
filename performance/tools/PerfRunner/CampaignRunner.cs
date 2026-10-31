using Perf.Core;
using Perf.Core.Agent;
using Perf.Core.Campaigns;
using Perf.Core.Runs;
using PerfRunner.Tools;

namespace PerfRunner;

public sealed class CampaignRunner(RunnerSettings settings, AgentClient agent, IRunTool k6, IRunTool stream)
{
    public async Task<string> Run(string campaignPath, CancellationToken ct)
    {
        var campaign = CampaignLoader.Parse(await File.ReadAllTextAsync(campaignPath, ct));
        var seed = Random.Shared.Next();
        var runs = CampaignPlanner.Expand(campaign, seed);
        var dir = Path.Combine(settings.ResultsDir, $"{Path.GetFileNameWithoutExtension(campaignPath)}-{DateTime.UtcNow:yyyyMMdd-HHmm}");
        await ResultsWriter.Json(Path.Combine(dir, "campaign.json"), new { campaign, seed, runs = runs.Select(r => r.RunId) }, ct);

        Console.WriteLine($"{runs.Count} runs, shuffle seed {seed}, results in {dir}");

        foreach (var run in runs)
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var meta = await Execute(campaign, run, attempt, seed, dir, ct);
                Console.WriteLine($"{run.RunId} a{attempt}: {meta.Status} {string.Join("; ", meta.Failures)}");
                if (meta.Status == "valid")
                    break;
            }
        }

        await agent.Stop(ct);
        return dir;
    }

    private async Task<RunMeta> Execute(Campaign campaign, RunSpec run, int attempt, int seed, string campaignDir, CancellationToken ct)
    {
        var runDir = Path.Combine(campaignDir, $"{run.RunId}-a{attempt}");
        Directory.CreateDirectory(runDir);
        var isStream = run.Scenario.IsStream();
        var tool = isStream ? stream : k6;
        var started = DateTimeOffset.UtcNow;
        AgentStatus? status = null;
        DateTimeOffset? warmupStart = null, measureStart = null, measureEnd = null;

        RunMeta Meta(string state, IReadOnlyList<string> failures) => new(run.RunId, attempt, run.Round, run.Transport, run.Scenario, run.Rate, run.Otel, seed,
            status?.Containers.FirstOrDefault(c => c.State == "running")?.GitSha, status?.Containers.FirstOrDefault(c => c.State == "running")?.ImageId,
            status?.Containers ?? [], warmupStart, measureStart, measureEnd, state, failures);

        try
        {
            status = await agent.Activate(new ActivateRequest(run.Transport, run.Otel, run.RunId), ct);
            if (status.Active != run.Transport || !status.Healthy)
                return await Finish(runDir, Meta("failed-verification", [$"agent reports {status.Active} healthy={status.Healthy}"]), run, started, ct);

            var dataFile = Path.Combine(runDir, "run-data.json");
            await ResultsWriter.Json(dataFile, await Discovery.Run(run.Transport, campaign, settings.BrokerClientUser, settings.BrokerClientPassword, ct), ct);
            var context = new RunContext(run, campaign, runDir, dataFile);

            var verify = await tool.Verify(context, ct);
            // Stream tools signal failure through the exit code alone; k6 additionally fails on any failed check.
            var verified = isStream ? verify.Success : verify.Success && verify.ErrorRate <= 0;
            if (!verified)
                return await Finish(runDir, Meta("failed-verification", ["verification failed", Trim(verify.Output)]), run, started, ct);

            warmupStart = DateTimeOffset.UtcNow;
            await tool.Run(context, campaign.WarmupSeconds, measured: false, ct);

            await using var cpu = new CpuSampler();
            measureStart = isStream ? warmupStart.Value.AddSeconds(campaign.WarmupSeconds) : DateTimeOffset.UtcNow;
            var result = await tool.Run(context, campaign.DurationSeconds, measured: true, ct);
            measureStart = result.MeasureStartUtc ?? measureStart;
            measureEnd = result.MeasureEndUtc ?? DateTimeOffset.UtcNow;

            var gates = Gates.Evaluate(new RunMetrics(result.Dropped, result.ErrorRate, cpu.Average));
            return await Finish(runDir, Meta(gates.Passed ? "valid" : "invalid", gates.Failures), run, started, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await Finish(runDir, Meta("failed", [ex.Message]), run, started, ct);
        }
    }

    private async Task<RunMeta> Finish(string runDir, RunMeta meta, RunSpec run, DateTimeOffset started, CancellationToken ct)
    {
        await ResultsWriter.Json(Path.Combine(runDir, "meta.json"), meta, ct);
        if (meta.Status != "valid")
        {
            try { await File.WriteAllTextAsync(Path.Combine(runDir, "agent-logs.txt"), await agent.Logs(run.Transport, started, ct), ct); }
            catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(runDir, "agent-logs.txt"), $"could not fetch logs: {ex.Message}", ct); }
        }
        return meta;
    }

    private static string Trim(string output) => output.Length > 4000 ? output[^4000..] : output;
}
