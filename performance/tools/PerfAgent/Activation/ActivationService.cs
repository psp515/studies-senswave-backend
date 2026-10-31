using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Perf.Core;
using Perf.Core.Agent;
using PerfAgent.Database;
using PerfAgent.Docker;

namespace PerfAgent.Activation;

public sealed class AgentBusyException() : Exception("Another agent operation is running.");

public sealed class ActivationService(IDockerEngine docker, IDatabaseAdmin database, IOptions<AgentOptions> options, ILogger<ActivationService> logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly AgentOptions _options = options.Value;

    public Task<AgentStatus> Activate(ActivateRequest request, CancellationToken ct) => Exclusive(async () =>
    {
        await StopApis(ct);
        await database.Recreate(_options.RunDatabase, _options.TemplateDatabase, ct);
        await Recreate(_options.ApiContainers[request.Transport], new Dictionary<string, string>
        {
            ["PostgreSql__Database"] = _options.RunDatabase,
            ["Diagnostics__OpenTelemetry__Enabled"] = request.Otel ? "true" : "false",
            ["OTEL_RESOURCE_ATTRIBUTES"] = $"run_id={request.RunId},deployment.environment={Environment.GetEnvironmentVariable("PERF_ENV") ?? "local"}",
        }, ct);
        await WaitHealthy(_options.ApiContainers[request.Transport], ct);
        return await StatusCore(ct);
    });

    public Task<AgentStatus> Stop(CancellationToken ct) => Exclusive(async () =>
    {
        await StopApis(ct);
        return await StatusCore(ct);
    });

    public Task<AgentStatus> Status(CancellationToken ct) => StatusCore(ct);

    /// <summary>Seeds <c>senswave_seed</c> through api-rest and the seed container, then makes it the template.</summary>
    public Task<string> Prepare(CancellationToken ct) => Exclusive(async () =>
    {
        var rest = _options.ApiContainers[Transport.Rest];
        await StopApis(ct);
        await docker.Stop(_options.SeedContainer, ct);
        await database.Recreate(_options.SeedDatabase, null, ct);
        await Recreate(rest, new Dictionary<string, string>
        {
            ["PostgreSql__Database"] = _options.SeedDatabase,
            ["Diagnostics__OpenTelemetry__Enabled"] = "false",
            ["OTEL_RESOURCE_ATTRIBUTES"] = "run_id=prepare",
        }, ct);
        string log;
        try
        {
            await WaitHealthy(rest, ct);
            var since = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await docker.Start(_options.SeedContainer, ct);
            log = await WaitForSeed(since, ct);
        }
        finally
        {
            await TryStop(_options.SeedContainer);
            await TryStop(rest);
        }

        if (log.Contains("Initial seed finished with"))
            throw new InvalidOperationException("Seed finished with errors; template not created. See seed log.\n" + Tail(log));

        await database.Recreate(_options.TemplateDatabase, _options.SeedDatabase, ct);
        return Tail(log);
    });

    public async Task<string> Logs(Transport transport, long sinceUnix, CancellationToken ct) =>
        await docker.Logs(_options.ApiContainers[transport], sinceUnix, ct);

    private async Task<T> Exclusive<T>(Func<Task<T>> operation)
    {
        if (!await _lock.WaitAsync(0))
            throw new AgentBusyException();
        try { return await operation(); }
        finally { _lock.Release(); }
    }

    private async Task StopApis(CancellationToken ct)
    {
        foreach (var name in _options.ApiContainers.Values)
            await docker.Stop(name, ct);
    }

    private async Task TryStop(string name)
    {
        try { await docker.Stop(name, CancellationToken.None); }
        catch (Exception ex) { logger.LogWarning(ex, "Best-effort stop of {container} failed", name); }
    }

    private async Task Recreate(string name, IReadOnlyDictionary<string, string> env, CancellationToken ct)
    {
        var inspect = await docker.Inspect(name, ct);
        var body = ContainerSpec.BuildCreateBody(inspect, env);
        logger.LogInformation("Recreating {container} with {overrides}", name, string.Join(", ", env.Keys));
        await docker.Remove(name, ct);
        try
        {
            await docker.Create(name, body, ct);
            await docker.Start(name, ct);
        }
        catch
        {
            await Restore(name, inspect);
            throw;
        }
    }

    /// <summary>Best-effort: puts the container back as it was so later activations still find it.</summary>
    private async Task Restore(string name, JsonObject inspect)
    {
        try
        {
            try { await docker.Remove(name, CancellationToken.None); } catch { /* may not exist */ }
            await docker.Create(name, ContainerSpec.BuildCreateBody(inspect, new Dictionary<string, string>()), CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not restore container {container}", name);
        }
    }

    private async Task WaitHealthy(string name, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(_options.HealthTimeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var state = (await docker.Inspect(name, ct))["State"];
            var status = state?["Status"]?.GetValue<string>();
            var health = state?["Health"]?["Status"]?.GetValue<string>();
            if (status is "exited" or "dead")
                throw new InvalidOperationException($"{name} exited before becoming healthy.");
            if (health == "healthy")
                return;
            if (health == "unhealthy")
                break;
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
        throw new InvalidOperationException($"{name} did not become healthy.");
    }

    private async Task<string> WaitForSeed(long sinceUnix, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMinutes(_options.SeedTimeoutMinutes);
        while (DateTime.UtcNow < deadline)
        {
            var log = await docker.Logs(_options.SeedContainer, sinceUnix, ct);
            if (log.Contains("Initial seed finished"))
                return log;
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
        throw new InvalidOperationException("Seed did not finish in time.");
    }

    private async Task<AgentStatus> StatusCore(CancellationToken ct)
    {
        var containers = new List<ContainerInfo>();
        Transport? active = null;
        var healthy = false;

        foreach (var (transport, name) in _options.ApiContainers)
        {
            var i = await docker.Inspect(name, ct);
            var state = i["State"]?["Status"]?.GetValue<string>() ?? "unknown";
            var health = i["State"]?["Health"]?["Status"]?.GetValue<string>();
            containers.Add(new ContainerInfo(name, state, health,
                i["HostConfig"]?["CpusetCpus"]?.GetValue<string>(),
                (i["HostConfig"]?["NanoCpus"]?.GetValue<long>() ?? 0) / 1e9,
                i["HostConfig"]?["Memory"]?.GetValue<long>() ?? 0,
                i["Image"]?.GetValue<string>(),
                i["Config"]?["Labels"]?["senswave.git-sha"]?.GetValue<string>()));

            if (state == "running")
            {
                active = transport;
                healthy = health == "healthy";
            }
        }

        return new AgentStatus(active, healthy, containers);
    }

    private static string Tail(string log) => string.Join('\n', log.Split('\n').TakeLast(200));
}
