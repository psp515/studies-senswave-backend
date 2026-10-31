using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Perf.Core;
using Perf.Core.Agent;
using PerfAgent;
using PerfAgent.Activation;
using PerfAgent.Database;
using PerfAgent.Docker;

namespace Perf.UnitTests;

public class ActivationServiceRobustnessTests
{
    private readonly List<string> _calls = new();
    private readonly Mock<IDockerEngine> _docker = new();
    private readonly Mock<IDatabaseAdmin> _database = new();
    private string _state = "running";
    private string? _health = "healthy";
    private string _seedLog = "Initial seed finished.";
    private int _createCount;
    private bool _failFirstCreate;

    private ActivationService Service(int healthTimeoutSeconds = 5)
    {
        _docker.Setup(d => d.Inspect(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string n, CancellationToken _) => JsonNode.Parse(
                $$$$"""
                {"Name":"/{{{{n}}}}","Config":{"Env":["ORIG=1"],"Labels":{}},"Image":"sha256:1","HostConfig":{},
                 "State":{"Status":"{{{{_state}}}}","Health":{"Status":"{{{{_health}}}}"}},"NetworkSettings":{"Networks":{}}}
                """)!.AsObject());
        _docker.Setup(d => d.Stop(It.IsAny<string>(), It.IsAny<CancellationToken>())).Callback((string n, CancellationToken _) => _calls.Add($"stop {n}"));
        _docker.Setup(d => d.Remove(It.IsAny<string>(), It.IsAny<CancellationToken>())).Callback((string n, CancellationToken _) => _calls.Add($"remove {n}"));
        _docker.Setup(d => d.Create(It.IsAny<string>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .Returns((string n, JsonObject b, CancellationToken _) =>
            {
                _calls.Add($"create {n} {string.Join(';', b["Env"]!.AsArray())}");
                if (_failFirstCreate && ++_createCount == 1)
                    throw new InvalidOperationException("create failed");
                return Task.CompletedTask;
            });
        _docker.Setup(d => d.Start(It.IsAny<string>(), It.IsAny<CancellationToken>())).Callback((string n, CancellationToken _) => _calls.Add($"start {n}"));
        _docker.Setup(d => d.Logs(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _seedLog);
        _database.Setup(d => d.Recreate(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback((string db, string? t, CancellationToken _) => _calls.Add($"db {db} from {t ?? "empty"}"));

        return new ActivationService(_docker.Object, _database.Object,
            Options.Create(new AgentOptions { HealthTimeoutSeconds = healthTimeoutSeconds }), NullLogger<ActivationService>.Instance);
    }

    [Fact]
    public async Task Prepare_RunsInOrderAndCreatesTemplateFromSeedDatabase()
    {
        var log = await Service().Prepare(CancellationToken.None);

        Assert.Contains("Initial seed finished", log);
        var dbSeed = _calls.IndexOf("db senswave_seed from empty");
        var createRest = _calls.FindIndex(c => c.StartsWith("create Senswave.Perf.Rest") && c.Contains("PostgreSql__Database=senswave_seed"));
        var startSeed = _calls.IndexOf("start Senswave.Perf.Seed");
        var stopSeedAfter = _calls.LastIndexOf("stop Senswave.Perf.Seed");
        var stopRestAfter = _calls.LastIndexOf("stop Senswave.Perf.Rest");
        var template = _calls.IndexOf("db senswave_template from senswave_seed");

        Assert.True(_calls.IndexOf("stop Senswave.Perf.Seed") < dbSeed);
        Assert.InRange(createRest, dbSeed + 1, startSeed - 1);
        Assert.InRange(stopSeedAfter, startSeed + 1, template - 1);
        Assert.InRange(stopRestAfter, startSeed + 1, template - 1);
        Assert.Equal(_calls.Count - 1, template);
    }

    [Fact]
    public async Task Prepare_SeedErrors_ThrowsWithoutCreatingTemplateAndStillStopsContainers()
    {
        _seedLog = "Initial seed finished with 2 errors.";

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().Prepare(CancellationToken.None));

        Assert.DoesNotContain(_calls, c => c.StartsWith("db senswave_template"));
        Assert.True(_calls.LastIndexOf("stop Senswave.Perf.Seed") > _calls.IndexOf("start Senswave.Perf.Seed"));
        Assert.True(_calls.LastIndexOf("stop Senswave.Perf.Rest") > _calls.IndexOf("start Senswave.Perf.Rest"));
    }

    [Fact]
    public async Task Prepare_StopsContainersWhenItThrowsMidway()
    {
        var service = Service();
        _docker.Setup(d => d.Start("Senswave.Perf.Seed", It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Prepare(CancellationToken.None));

        Assert.True(_calls.LastIndexOf("stop Senswave.Perf.Rest") > _calls.IndexOf("start Senswave.Perf.Rest"));
        Assert.DoesNotContain(_calls, c => c.StartsWith("db senswave_template"));
    }

    [Theory]
    [InlineData("exited", "none")]
    [InlineData("dead", "none")]
    [InlineData("running", "unhealthy")]
    public async Task Activate_FailsFastWhenContainerIsNotGoingToBecomeHealthy(string state, string health)
    {
        _state = state;
        _health = health;

        var started = DateTime.UtcNow;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service(healthTimeoutSeconds: 60).Activate(new ActivateRequest(Transport.Rest, true, "r"), CancellationToken.None));

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10));
        Assert.Contains("Senswave.Perf.Rest", ex.Message);
    }

    [Fact]
    public async Task Activate_WhenCreateFails_RecreatesOriginalContainerAndRethrowsOriginal()
    {
        _failFirstCreate = true;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service().Activate(new ActivateRequest(Transport.Grpc, false, "r1"), CancellationToken.None));

        Assert.Equal("create failed", ex.Message);
        var creates = _calls.Where(c => c.StartsWith("create Senswave.Perf.Grpc")).ToList();
        Assert.Equal(2, creates.Count);
        Assert.Contains("Diagnostics__OpenTelemetry__Enabled=false", creates[0]);
        Assert.Equal("create Senswave.Perf.Grpc ORIG=1", creates[1]);
    }
}
