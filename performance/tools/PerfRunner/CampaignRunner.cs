using Perf.Core;
using Perf.Core.Agent;
using Perf.Core.Campaigns;
using Perf.Core.Knee;
using Perf.Core.Runs;
using PerfRunner.Tools;

namespace PerfRunner;

public sealed class CampaignRunner(RunnerSettings settings, AgentClient agent, IRunTool k6, IRunTool stream)
{
    public async Task<string> Run(string campaignPath, CancellationToken ct)
    {
        var campaign = CampaignLoader.Parse(await File.ReadAllTextAsync(campaignPath, ct));
        var seed = Random.Shared.Next();
        var dir = Path.Combine(settings.ResultsDir, $"{Path.GetFileNameWithoutExtension(campaignPath)}-{DateTime.UtcNow:yyyyMMdd-HHmm}");

        if (campaign.Knee is { } knee)
        {
            var pairs = CampaignPlanner.Pairs(campaign, seed);
            await ResultsWriter.Json(Path.Combine(dir, "campaign.json"), new { campaign, seed, pairs }, ct);
            Console.WriteLine($"knee search for {pairs.Count} transport/scenario pairs (max {knee.Max}/s, precision {knee.Precision}/s), shuffle seed {seed}, results in {dir}");
            await RecordNetwork(campaign, Path.Combine(dir, "network-before.json"), ct);

            foreach (var pair in pairs)
                await SearchKnee(campaign, knee, pair, seed, dir, ct);

            await agent.Stop(ct);
            await RecordNetwork(campaign, Path.Combine(dir, "network-after.json"), ct);
            return dir;
        }

        var runs = CampaignPlanner.Expand(campaign, seed);
        await ResultsWriter.Json(Path.Combine(dir, "campaign.json"), new { campaign, seed, runs = runs.Select(r => r.RunId) }, ct);

        Console.WriteLine($"{runs.Count} runs, shuffle seed {seed}, results in {dir}");
        await RecordNetwork(campaign, Path.Combine(dir, "network-before.json"), ct);

        var climb = new KneeClimb();
        foreach (var run in runs)
        {
            if (campaign.StopAfterSaturated && climb.ShouldSkip(run))
            {
                Console.WriteLine($"{run.RunId}: skipped (a lower rate of this ladder was not valid)");
                continue;
            }

            var status = RunStatus.Failed;
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var (meta, _) = await Execute(campaign, run, attempt, seed, dir, null, ct);
                status = meta.Status;
                Console.WriteLine($"{run.RunId} a{attempt}: {meta.Status} {string.Join("; ", meta.Failures)}");
                // A saturated run is a result (the server did not sustain the rate), so it is not retried.
                if (RunStatus.IsFinal(meta.Status))
                    break;
            }

            climb.Record(run, status);
        }

        await agent.Stop(ct);
        await RecordNetwork(campaign, Path.Combine(dir, "network-after.json"), ct);
        return dir;
    }

    /// <summary>
    /// Bisection for one pair (KneeSearch). After a sustained (valid) step the next, higher step runs on the same API
    /// instance and test data, without reset and discovery. After any other step the API and database are reset, because
    /// an overloaded API can keep backlogs that would distort the next step. A step that fails for a reason other than
    /// saturation (verification, crash, overloaded load generator) still counts as not sustained, so the search ends.
    /// </summary>
    private async Task SearchKnee(Campaign campaign, CampaignKnee knee, RunPair pair, int seed, string dir, CancellationToken ct)
    {
        var search = new KneeSearch(knee.Max, knee.Precision);
        Session? session = null;

        while (search.Next is { } rate)
        {
            var run = pair.At(rate);
            RunMeta meta = null!;
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                (meta, var next) = await Execute(campaign, run, attempt, seed, dir, session, ct);
                session = meta.Status == RunStatus.Valid ? next : null;
                Console.WriteLine($"{run.RunId} a{attempt}: {meta.Status} {string.Join("; ", meta.Failures)}");
                if (RunStatus.IsFinal(meta.Status))
                    break;
            }

            search.Record(rate, meta.Status == RunStatus.Valid);
        }

        var result = search.AtLeastMax ? $">= {search.Knee}/s (max sustained)" : $"{search.Knee}/s (precision {knee.Precision}/s)";
        Console.WriteLine($"knee {pair.Transport} {pair.Scenario}: {result}");
    }

    /// <summary>A started API with discovered test data that the next knee step can reuse.</summary>
    private sealed record Session(AgentStatus Status, RunData Data);

    /// <summary>RTT and bandwidth of the load-side path at the start and end of a session; a failure is logged, not fatal.</summary>
    private async Task RecordNetwork(Campaign campaign, string file, CancellationToken ct)
    {
        try
        {
            var check = await NetworkChecker.Measure(campaign.Target, settings.AgentToken, ct);
            await ResultsWriter.Json(file, check, ct);
            Console.WriteLine($"network: RTT median {check.TcpConnectRtt.MedianMs:F1} ms (p95 {check.TcpConnectRtt.P95Ms:F1}), down {check.DownloadMbps:F0} Mbit/s, up {check.UploadMbps:F0} Mbit/s");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"warning: network check failed: {ex.Message}");
        }
    }

    /// <param name="reuse">A running API and its test data from the previous (valid) knee step: no reset, no discovery.</param>
    private async Task<(RunMeta Meta, Session? Session)> Execute(Campaign campaign, RunSpec run, int attempt, int seed, string campaignDir,
        Session? reuse, CancellationToken ct)
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
            RunData runData;
            if (reuse is not null)
            {
                status = reuse.Status;
                runData = reuse.Data;
            }
            else
            {
                status = await agent.Activate(new ActivateRequest(run.Transport, run.Otel, run.RunId), ct);
                if (status.Active != run.Transport || !status.Healthy)
                    return (await Finish(runDir, Meta("failed-verification", [$"agent reports {status.Active} healthy={status.Healthy}"]), run, started, ct), null);

                runData = await Discovery.Run(run.Transport, run.Scenario, campaign, settings.BrokerClientUser, settings.BrokerClientPassword, ct);
            }

            var session = new Session(status, runData);
            var dataFile = Path.Combine(runDir, "run-data.json");
            await ResultsWriter.Json(dataFile, runData, ct);
            await RecordSample(campaign, run, runData, campaignDir, ct);
            var context = new RunContext(run, campaign, runDir, dataFile);

            var verify = await tool.Verify(context, ct);
            // Stream tools signal failure through the exit code alone; k6 additionally fails on any failed check.
            var verified = isStream ? verify.Success : verify.Success && verify.ErrorRate <= 0;
            if (!verified)
                return (await Finish(runDir, Meta("failed-verification", ["verification failed", Trim(verify.Output)]), run, started, ct), null);

            warmupStart = DateTimeOffset.UtcNow;
            await tool.Run(context, campaign.WarmupSeconds, measured: false, ct);

            await using var cpu = new CpuSampler();
            measureStart = isStream ? warmupStart.Value.AddSeconds(campaign.WarmupSeconds) : DateTimeOffset.UtcNow;
            var result = await tool.Run(context, campaign.DurationSeconds, measured: true, ct);
            measureStart = result.MeasureStartUtc ?? measureStart;
            measureEnd = result.MeasureEndUtc ?? DateTimeOffset.UtcNow;

            if (!result.Success)
                return (await Finish(runDir, Meta("failed", ["measured run failed", Trim(result.Output)]), run, started, ct), null);

            var gates = Gates.Evaluate(new RunMetrics(result.Dropped, result.ErrorRate, cpu.AverageBetween(measureStart.Value, measureEnd.Value),
                (measureEnd.Value - measureStart.Value).TotalSeconds, campaign.DurationSeconds, result.DeliveryP99Ms, result.Lost));
            return (await Finish(runDir, Meta(gates.Status, gates.Failures), run, started, ct), session);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await Finish(runDir, Meta("cancelled", ["cancelled by user"]), run, started, CancellationToken.None, fetchLogs: false);
            throw;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            return (await Finish(runDir, Meta("failed", [ex.Message]), run, started, ct), null);
        }
    }

    /// <summary>
    /// Writes one example response per transport and scenario (samples/&lt;scenario&gt;-&lt;transport&gt;.json) the first time
    /// the pair runs in the campaign, before its measurement. A failure is logged, not fatal.
    /// </summary>
    private static async Task RecordSample(Campaign campaign, RunSpec run, RunData data, string campaignDir, CancellationToken ct)
    {
        var file = Path.Combine(campaignDir, "samples", $"{run.Scenario}-{run.Transport}.json".ToLowerInvariant());
        if (File.Exists(file) || data.Users.Count == 0)
            return;

        try
        {
            await using var client = Perf.Client.ApiClientFactory.Create(run.Transport, new Perf.Client.ApiTarget(campaign.Target.Api, campaign.Target.InsecureTls));
            var sample = await client.Sample(run.Scenario, data.Users[0], ct);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await ResultsWriter.Json(file, sample, ct);
            Console.WriteLine($"sample {run.Scenario} {run.Transport}: {sample.PayloadBytes} B payload");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"warning: could not record the {run.Scenario} {run.Transport} sample: {ex.Message}");
        }
    }

    private async Task<RunMeta> Finish(string runDir, RunMeta meta, RunSpec run, DateTimeOffset started, CancellationToken ct, bool fetchLogs = true)
    {
        await ResultsWriter.Json(Path.Combine(runDir, "meta.json"), meta, ct);
        await BlankTokens(Path.Combine(runDir, "run-data.json"), ct);
        if (fetchLogs && meta.Status != "valid")
        {
            try { await File.WriteAllTextAsync(Path.Combine(runDir, "agent-logs.txt"), await agent.Logs(run.Transport, started, ct), ct); }
            catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(runDir, "agent-logs.txt"), $"could not fetch logs: {ex.Message}", ct); }
        }
        return meta;
    }

    /// <summary>Results and archives must not carry access tokens, so they are blanked once the attempt is over.</summary>
    private static async Task BlankTokens(string dataFile, CancellationToken ct)
    {
        if (!File.Exists(dataFile))
            return;
        try
        {
            var data = System.Text.Json.JsonSerializer.Deserialize<RunData>(await File.ReadAllTextAsync(dataFile, ct), PerfJson.Options)!;
            await ResultsWriter.Json(dataFile, new RunData(data.Users.Select(u => u with { AccessToken = "" }).ToList()), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"warning: could not blank tokens in {dataFile}: {ex.Message}");
        }
    }

    private static string Trim(string output) => output.Length > 4000 ? output[^4000..] : output;
}
