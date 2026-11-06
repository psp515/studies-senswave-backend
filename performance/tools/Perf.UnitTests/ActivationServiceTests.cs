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

public class ActivationServiceTests
{
    private readonly List<string> _calls = new();
    private readonly Mock<IDockerEngine> _docker = new();
    private readonly Mock<IDatabaseAdmin> _database = new();

    private ActivationService Service()
    {
        _docker.Setup(d => d.Inspect(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string n, CancellationToken _) => JsonNode.Parse(
                $$$$"""
                {"Name":"/{{{{n}}}}","Config":{"Env":[],"Labels":{"senswave.git-sha":"abc"}},"Image":"sha256:1","HostConfig":{},
                 "State":{"Status":"running","Health":{"Status":"healthy"}},"NetworkSettings":{"Networks":{}}}
                """)!.AsObject());
        _docker.Setup(d => d.Stop(It.IsAny<string>(), It.IsAny<CancellationToken>())).Callback((string n, CancellationToken _) => _calls.Add($"stop {n}"));
        _docker.Setup(d => d.Remove(It.IsAny<string>(), It.IsAny<CancellationToken>())).Callback((string n, CancellationToken _) => _calls.Add($"remove {n}"));
        _docker.Setup(d => d.Create(It.IsAny<string>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .Callback((string n, JsonObject b, CancellationToken _) => _calls.Add($"create {n} {string.Join(';', b["Env"]!.AsArray())}"));
        _docker.Setup(d => d.Start(It.IsAny<string>(), It.IsAny<CancellationToken>())).Callback((string n, CancellationToken _) => _calls.Add($"start {n}"));
        _database.Setup(d => d.Recreate(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback((string db, string? t, CancellationToken _) => _calls.Add($"db {db} from {t}"));

        return new ActivationService(_docker.Object, _database.Object, Options.Create(new AgentOptions()), NullLogger<ActivationService>.Instance);
    }

    [Fact]
    public async Task StopsAllThenResetsDatabaseThenRecreatesOnlyTheTarget()
    {
        await Service().Activate(new ActivateRequest(Transport.Grpc, Otel: false, RunId: "r1"), CancellationToken.None);

        var firstDb = _calls.FindIndex(c => c.StartsWith("db "));
        Assert.All(new[] { "stop Senswave.Perf.Rest", "stop Senswave.Perf.Grpc", "stop Senswave.Perf.GrpcWeb" },
            s => Assert.InRange(_calls.IndexOf(s), 0, firstDb - 1));
        Assert.Equal("db senswave_run from senswave_template", _calls[firstDb]);
        var create = Assert.Single(_calls, c => c.StartsWith("create "));
        Assert.StartsWith("create Senswave.Perf.Grpc", create);
        Assert.Contains("Diagnostics__OpenTelemetry__Enabled=false", create);
        Assert.Contains("OTEL_RESOURCE_ATTRIBUTES=run_id=r1", create);
        Assert.Contains("PostgreSql__Database=senswave_run", create);
        Assert.Equal("start Senswave.Perf.Grpc", _calls[^1]);
    }

    [Fact]
    public async Task SecondConcurrentOperationIsRejected()
    {
        var service = Service();
        var gate = new TaskCompletionSource();
        _database.Setup(d => d.Recreate(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(gate.Task);

        var first = service.Activate(new ActivateRequest(Transport.Rest, true, "a"), CancellationToken.None);

        await Assert.ThrowsAsync<AgentBusyException>(() => service.Activate(new ActivateRequest(Transport.Rest, true, "b"), CancellationToken.None));
        gate.SetResult();
        await first;
    }
}
