using Perf.Core.Campaigns;
using PerfRunner;
using PerfRunner.Tools;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: perfrunner run|prepare <campaign.json> | export|archive <resultsDir>");
    return 2;
}

var settings = RunnerSettings.FromEnvironment();
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

switch (args[0])
{
    case "run":
    {
        var campaign = CampaignLoader.Parse(await File.ReadAllTextAsync(args[1]));
        using var agent = new AgentClient(campaign.Target, settings.AgentToken);
        try
        {
            var dir = await new CampaignRunner(settings, agent, new K6Tool(settings), new StreamTool(settings)).Run(args[1], cts.Token);
            await Export(settings, dir, campaign.Export.Traces, cts.Token);
            Console.WriteLine($"Done: {dir}");
            return 0;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            Console.Error.WriteLine("Cancelled: stopping the API under test.");
            try { await agent.Stop(CancellationToken.None); }
            catch (Exception ex) { Console.Error.WriteLine($"Could not stop the agent: {ex.Message}"); }
            return 130;
        }
    }
    case "prepare":
    {
        var campaign = CampaignLoader.Parse(await File.ReadAllTextAsync(args[1]));
        using var agent = new AgentClient(campaign.Target, settings.AgentToken);
        Console.WriteLine(await agent.Prepare(cts.Token));
        return 0;
    }
    case "export":
    {
        await Export(settings, args[1], traces: args.Contains("--traces"), cts.Token);
        return 0;
    }
    case "archive":
    {
        var url = await PerfRunner.Export.Archiver.Upload(args[1], settings.ArchiveSasUrl ?? throw new InvalidOperationException("ARCHIVE_SAS_URL is required."), cts.Token);
        Console.WriteLine($"Uploaded {url}");
        return 0;
    }
    default:
        Console.Error.WriteLine($"Unknown command {args[0]}");
        return 2;
}

static async Task Export(RunnerSettings settings, string dir, bool traces, CancellationToken ct)
{
    if (settings.PromUrl is null || settings.GrafanaReadToken is null)
    {
        Console.WriteLine("GRAFANA_PROM_URL / GRAFANA_READ_TOKEN not set; skipping server export (run 'export' later).");
        return;
    }

    Console.WriteLine("Waiting 60 s for the last metric exports to reach Grafana Cloud...");
    await Task.Delay(TimeSpan.FromSeconds(60), ct);

    using var prometheus = new PerfRunner.Export.PrometheusClient(settings.PromUrl, settings.PromUser!, settings.GrafanaReadToken);
    await new PerfRunner.Export.ServerExporter(prometheus).ExportCampaign(dir, ct);

    if (traces && settings.TempoUrl is not null)
        await new PerfRunner.Export.TraceExporter(settings.TempoUrl, settings.TempoUser!, settings.GrafanaReadToken).ExportCampaign(dir, ct);
}
