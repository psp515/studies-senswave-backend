using System.Text.Json.Nodes;
using PerfAgent.Docker;

namespace Perf.UnitTests;

public class ContainerSpecTests
{
    private static JsonObject Inspect() => JsonNode.Parse("""
    {
      "Name": "/Senswave.Perf.Grpc",
      "Config": { "Image": "senswave-perf-api", "Env": ["A=1", "PostgreSql__Database=senswave_run", "OTEL_RESOURCE_ATTRIBUTES=run_id=none"],
                  "Labels": { "com.docker.compose.service": "api-grpc" }, "Hostname": "abc123" },
      "HostConfig": { "CpusetCpus": "2-3", "NanoCpus": 2000000000, "Memory": 4294967296, "Binds": ["/host/https:/https:ro"] },
      "NetworkSettings": { "Networks": { "senswave-perf": { "Aliases": ["api", "api-grpc"], "NetworkID": "n1", "IPAddress": "172.18.0.5" } } }
    }
    """)!.AsObject();

    [Fact]
    public void OverridesAndAddsEnvKeepingOthers()
    {
        var body = ContainerSpec.BuildCreateBody(Inspect(), new Dictionary<string, string>
        {
            ["PostgreSql__Database"] = "senswave_seed",
            ["Diagnostics__OpenTelemetry__Enabled"] = "false",
        });

        var env = body["Env"]!.AsArray().Select(e => e!.GetValue<string>()).ToList();
        Assert.Contains("A=1", env);
        Assert.Contains("PostgreSql__Database=senswave_seed", env);
        Assert.Contains("Diagnostics__OpenTelemetry__Enabled=false", env);
        Assert.DoesNotContain("PostgreSql__Database=senswave_run", env);
    }

    [Fact]
    public void KeepsHostConfigLabelsAndAliases_DropsRuntimeFields()
    {
        var body = ContainerSpec.BuildCreateBody(Inspect(), new Dictionary<string, string>());

        Assert.Equal("2-3", body["HostConfig"]!["CpusetCpus"]!.GetValue<string>());
        Assert.Equal("api-grpc", body["Labels"]!["com.docker.compose.service"]!.GetValue<string>());
        Assert.Null(body["Hostname"]);
        var endpoint = body["NetworkingConfig"]!["EndpointsConfig"]!["senswave-perf"]!;
        Assert.Equal(new[] { "api", "api-grpc" }, endpoint["Aliases"]!.AsArray().Select(a => a!.GetValue<string>()));
        Assert.Null(endpoint["IPAddress"]);
    }
}
