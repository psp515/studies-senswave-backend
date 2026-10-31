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
        var dir = await new CampaignRunner(settings, agent, new K6Tool(settings), new StreamTool(settings)).Run(args[1], cts.Token);
        Console.WriteLine($"Done: {dir}");
        return 0;
    }
    case "prepare":
    {
        var campaign = CampaignLoader.Parse(await File.ReadAllTextAsync(args[1]));
        using var agent = new AgentClient(campaign.Target, settings.AgentToken);
        Console.WriteLine(await agent.Prepare(cts.Token));
        return 0;
    }
    default:
        Console.Error.WriteLine($"Unknown command {args[0]}");
        return 2;
}
