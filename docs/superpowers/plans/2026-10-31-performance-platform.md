# Performance Test Platform (Phase 1, local) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the `performance/` platform from Spec E so that a smoke campaign (every scenario × transport at 100/s) runs end-to-end on a local Docker stack, with one API container active at a time, server telemetry going through Grafana Alloy, and results written to files.

**Architecture:** A VM-side stack: three API containers (only one running), **PerfAgent** (switches containers through the Docker Engine API, resets Postgres from a template, seeds), and Grafana Alloy. A load-side **PerfRunner** container (with k6 and **PerfStream**) expands a JSON campaign into runs and drives the agent over HTTPS. Shared .NET code lives in `Perf.Core` (pure, unit-tested) and `Perf.Client` (REST / gRPC / gRPC-Web clients built from `Senswave.Contracts`).

**Tech Stack:** .NET 10 (ASP.NET Core minimal API, console apps), xUnit + Moq, k6 1.3.0 (JavaScript), Docker Compose, Grafana Alloy, Postgres 17.6, Eclipse Mosquitto (local stand-in only), Grafana Cloud (Prometheus + Tempo HTTP APIs).

**Spec:** `docs/specs/2026-10-31-performance-platform-design.md`. Read it before starting any task.

## Global Constraints

- No new NuGet packages. Use only the packages already in `Directory.Packages.props`; `PackageReference` items have no `Version` (central package management). Npgsql is used via `Npgsql.EntityFrameworkCore.PostgreSQL`, YAML is not used (campaign files are JSON), Azure Blob upload is a plain `HttpClient` `PUT`, and Docker is driven over its Engine API with plain `HttpClient`.
- New container images allowed: `postgres:17.6`, `eclipse-mosquitto:2.0.21`, `grafana/alloy:v1.10.0`, `grafana/k6:1.3.0`, `mcr.microsoft.com/dotnet/sdk:10.0`, `mcr.microsoft.com/dotnet/aspnet:10.0`, `mcr.microsoft.com/dotnet/runtime:10.0`. If a tag does not exist on Docker Hub when you `docker pull` it, use the newest patch release of the same minor that is at least two weeks old and note it in the README "Problems and solutions" section.
- Perf tools live under `performance/tools/`, in their own `performance/tools/Perf.sln`, and are **not** added to `Senswave.sln`. Project and assembly names must **not** start with `Senswave` (`ModuleLoader` loads every `Senswave*.dll`).
- Target framework `net10.0`, `<ImplicitUsings>enable</ImplicitUsings>`, `<Nullable>enable</Nullable>`. Follow `.editorconfig` (4-space indent, CRLF).
- All three transports run over **HTTPS with HTTP/2**. Rates are **100, 500, 1000** per second; one run = reset → verification → 30 s warm-up → 5 min measurement (smoke: 10 s warm-up, 30 s measurement).
- Container names: `Senswave.Perf.Rest`, `Senswave.Perf.Grpc`, `Senswave.Perf.GrpcWeb`, `Senswave.Perf.Seed`, `Senswave.Perf.Agent`, `Senswave.Perf.Alloy`; local only: `Senswave.Perf.Database`, `Senswave.Perf.Mqtt`.
- OTel service names: `senswave-api-rest`, `senswave-api-grpc`, `senswave-api-grpcweb`.
- Databases: `senswave_seed`, `senswave_template`, `senswave_run`.
- Commit messages: `feat: …` / `chore: …` / `docs: …`, **no `Co-Authored-By` trailer**. One branch: `perf/platform` (already created). Never push or merge.
- Flag test failures in your report; never skip or delete a failing test to get green.

## File Structure

```
performance/
  README.md                                   (rewritten, Task 13)
  .gitignore                                  results/, **/.env
  stack/compose.yml                           VM stack (Task 2)
  stack/.env.template
  stack/api/appsettings.Production.json       perf overrides mounted into the API
  stack/alloy/config.alloy                    (Task 11)
  local/compose.yml                           includes stack/, adds postgres + mosquitto
  local/mosquitto/mosquitto.conf
  runner/compose.yml                          load side (Task 12)
  runner/.env.template
  runner/campaigns/smoke.json
  runner/campaigns/full.json
  k6/lib/{config,data,rest,grpc,grpcweb,protobuf}.js   (Task 9)
  k6/s1.js … s5.js
  grafana/dashboard.json                      (Task 11)
  tools/Perf.sln
  tools/Perf.Core/        pure logic: transports, campaign, planner, gates, summaries, tracker, export queries, blob url
  tools/Perf.Client/      REST / gRPC / gRPC-Web client: login, discovery, tile action, live-update subscription
  tools/PerfAgent/        ASP.NET minimal API on the VM + Dockerfile
  tools/PerfRunner/       console app on the load side + Dockerfile (also packages PerfStream and k6)
  tools/PerfStream/       console app for S6a/S6b
  tools/Perf.UnitTests/   xUnit tests for Perf.Core and PerfAgent logic
src/Shared/Senswave.Infrastructure/Diagnostics/OpenTelemetry/OpenTelemetryOptions.cs   (+LatencyHistogramBoundaries, Task 1)
src/Presentation/Senswave.Presentation.Api/Diagnostics/DiagnosticsExtensions.cs        (+AddView, Task 1)
```

Removed: `performance/docker-compose.yml`, `performance/.env.template`, `performance/k6/legal-terms.js` (replaced by `k6/s4.js`).

---

### Task 1: API latency histogram boundaries

**Files:**
- Modify: `src/Shared/Senswave.Infrastructure/Diagnostics/OpenTelemetry/OpenTelemetryOptions.cs`
- Modify: `src/Presentation/Senswave.Presentation.Api/Diagnostics/DiagnosticsExtensions.cs`

**Interfaces:**
- Produces: configuration key `Diagnostics:OpenTelemetry:LatencyHistogramBoundaries` (array of seconds, `double[]`, empty = framework default). Task 2 sets it in `stack/api/appsettings.Production.json`.

- [ ] **Step 1: Add the option**

Append to `OpenTelemetryOptions` (keep the existing XML-comment style):

```csharp
    /// <summary>
    /// Explicit bucket boundaries, in seconds, for <c>http.server.request.duration</c>. Empty keeps the framework default.
    /// Finer buckets give p95/p99 real resolution in the low-millisecond range.
    /// </summary>
    public double[] LatencyHistogramBoundaries { get; set; } = [];
```

- [ ] **Step 2: Apply the view**

In `DiagnosticsExtensions.AddDiagnostics`, inside `.WithMetrics(x => x ...)`, insert before `.AddOtlpExporter(...)` by splitting the chain:

```csharp
            ).WithMetrics(x =>
            {
                x.AddNpgsqlInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddMeter(Telemetry.SmtpClient.MeterName);

                if (options.LatencyHistogramBoundaries.Length > 0)
                    x.AddView("http.server.request.duration",
                        new ExplicitBucketHistogramConfiguration { Boundaries = options.LatencyHistogramBoundaries });

                x.AddOtlpExporter((_, reader) =>
                    reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = options.MetricExportIntervalSeconds * 1000);
            });
```

- [ ] **Step 3: Build and run the affected tests**

Run: `dotnet build Senswave.sln` → Expected: Build succeeded, 0 errors.
Run: `dotnet test test/Modules/Users/Senswave.Users.EndTests --filter "Category!=FailurePath&Category!=RateLimiting"` → Expected: all pass (exercises API startup with diagnostics).
Run: `dotnet test test/Shared/Senswave.ArchitectureTests` → Expected: all pass.

- [ ] **Step 4: Commit**

```bash
git add src/Shared/Senswave.Infrastructure/Diagnostics/OpenTelemetry/OpenTelemetryOptions.cs src/Presentation/Senswave.Presentation.Api/Diagnostics/DiagnosticsExtensions.cs
git commit -m "feat: configurable latency histogram boundaries"
```

---

### Task 2: VM stack and local compose

**Files:**
- Create: `performance/stack/compose.yml`, `performance/stack/.env.template`, `performance/stack/api/appsettings.Production.json`
- Create: `performance/local/compose.yml`, `performance/local/mosquitto/mosquitto.conf`, `performance/.gitignore`
- Delete: `performance/docker-compose.yml`, `performance/.env.template`

**Interfaces:**
- Produces: containers named per Global Constraints; all API containers share network alias `${PERF_API_HOST}` on network `senswave-perf`; health check on `http://localhost:5001/api/health`; env keys the agent overrides: `PostgreSql__Database`, `Diagnostics__OpenTelemetry__Enabled`, `OTEL_RESOURCE_ATTRIBUTES`.
- Agent (Task 5) container and Alloy (Task 11) services are added to this file by those tasks.

- [ ] **Step 1: Check there is no existing `appsettings.Production.json`**

Run: `ls src/Presentation/Senswave.Presentation.Api/appsettings.Production.json`
Expected: "No such file". If it exists, read it and merge its keys into the file from Step 2 instead of shadowing them.

- [ ] **Step 2: Write `performance/stack/api/appsettings.Production.json`**

```json
{
  "OTEL_EXPORTER_OTLP_PROTOCOL": "grpc",
  "RateLimiter": { "Enabled": false },
  "MessageBus": { "Type": "InMemory" },
  "Smtp": { "Enabled": false },
  "Diagnostics": {
    "OpenTelemetry": {
      "TraceSamplingRatio": 0.01,
      "MetricExportIntervalSeconds": 10,
      "LatencyHistogramBoundaries": [ 0.0005, 0.001, 0.0015, 0.002, 0.003, 0.004, 0.005, 0.0075, 0.01, 0.015, 0.02, 0.03, 0.05, 0.075, 0.1, 0.15, 0.25, 0.5, 1, 2.5, 5, 10 ]
    }
  },
  "Modules": {
    "DataSource": {
      "Brokers": {
        "AllowSharedBrokers": true,
        "Client": {
          "MilisecondSpanBetweenMessagesOnSameTopic": 0,
          "RateLimiters": { "MessageRateLimiter": { "TokenLimit": 100000, "KeepTokenSeconds": 60 } }
        }
      }
    }
  }
}
```

- [ ] **Step 3: Write `performance/stack/.env.template`**

```dotenv
# Host name clients use for the API; also a network alias of every API container,
# so the seed container resolves it internally. Local: api (matches docker/generate-cert.sh).
PERF_API_HOST=api
API_PORT=8443
AGENT_PORT=9443
# local | azure (Alloy label)
PERF_ENV=local
GIT_SHA=unknown

# Certificate directory (host path) with senswave.api.pfx and senswave.api.crt
CERT_DIR=../../docker/https
API_CERTIFICATE_PASSWORD=password
# Seed trust store: self-signed locally; /etc/ssl/certs/ca-certificates.crt with Let's Encrypt
SEED_SSL_CERT_FILE=/https/senswave.api.crt

# vCPU pinning (D4as_v5: 0-1 = OS/agent/alloy, 2-3 = API). Check sibling pairs with lscpu -e.
API_CPUSET=2-3
API_CPUS=2
API_MEMORY=4g
SIDE_CPUSET=0-1

# Postgres (local: database container; Azure: Flexible Server host)
POSTGRES_HOST=database
POSTGRES_PORT=5432
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres

# Seed: 100 generated users sharing one broker (local: mosquitto)
SEED_USER_COUNT=100
SEED_BROKER_URL=mqtt
SEED_BROKER_PORT=1883
SEED_BROKER_USERNAME=perf
SEED_BROKER_PASSWORD=perf
```

- [ ] **Step 4: Write `performance/stack/compose.yml`**

```yaml
name: senswave-perf

# VM stack (Spec E). Create everything once, start nothing:
#   docker compose --profile seed up --no-start
# PerfAgent then starts exactly one API container per run.

x-api: &api
  image: senswave-perf-api
  build:
    context: ../..
    dockerfile: src/Presentation/Senswave.Presentation.Api/Dockerfile
    args:
      BUILD_CONFIGURATION: Release
      BUILD_IMAGE_CONFIGURATION: release
    labels:
      senswave.git-sha: ${GIT_SHA:-unknown}
  restart: "no"
  cpuset: ${API_CPUSET:-2-3}
  cpus: ${API_CPUS:-2}
  mem_limit: ${API_MEMORY:-4g}
  memswap_limit: ${API_MEMORY:-4g}
  pids_limit: 512
  ports:
    - ${API_PORT:-8443}:8443
  volumes:
    - ${CERT_DIR:-../../docker/https}:/https:ro
    - ./api/appsettings.Production.json:/app/appsettings.Production.json:ro
  labels:
    senswave.perf.role: api
  healthcheck:
    test: ["CMD-SHELL", "curl -s http://localhost:5001/api/health | grep -q '\"status\":\"Healthy\"'"]
    interval: 2s
    timeout: 2s
    retries: 60
    start_period: 5s

x-api-environment: &api-environment
  ASPNETCORE_ENVIRONMENT: Production
  ASPNETCORE_URLS: https://+:8443;http://+:5001
  ASPNETCORE_Kestrel__Certificates__Default__Path: /https/senswave.api.pfx
  ASPNETCORE_Kestrel__Certificates__Default__Password: ${API_CERTIFICATE_PASSWORD}
  PostgreSql__Host: ${POSTGRES_HOST}
  PostgreSql__Port: ${POSTGRES_PORT:-5432}
  PostgreSql__Username: ${POSTGRES_USER}
  PostgreSql__Password: ${POSTGRES_PASSWORD}
  PostgreSql__Database: senswave_run
  Diagnostics__OpenTelemetry__Enabled: "true"
  OTEL_EXPORTER_OTLP_ENDPOINT: http://alloy:4317
  OTEL_RESOURCE_ATTRIBUTES: run_id=none,deployment.environment=${PERF_ENV:-local}

services:
  api-rest:
    <<: *api
    container_name: Senswave.Perf.Rest
    networks:
      perf:
        aliases: [ "${PERF_API_HOST:-api}" ]
    environment:
      <<: *api-environment
      Transport__Mode: Rest
      OTEL_SERVICE_NAME: senswave-api-rest

  api-grpc:
    <<: *api
    container_name: Senswave.Perf.Grpc
    networks:
      perf:
        aliases: [ "${PERF_API_HOST:-api}" ]
    environment:
      <<: *api-environment
      Transport__Mode: Grpc
      OTEL_SERVICE_NAME: senswave-api-grpc

  api-grpcweb:
    <<: *api
    container_name: Senswave.Perf.GrpcWeb
    networks:
      perf:
        aliases: [ "${PERF_API_HOST:-api}" ]
    environment:
      <<: *api-environment
      Transport__Mode: GrpcWeb
      OTEL_SERVICE_NAME: senswave-api-grpcweb

  seed:
    image: senswave-perf-seed
    build:
      context: ../..
      dockerfile: src/Presentation/Senswave.Presentation.Seed/Dockerfile
      args:
        BUILD_CONFIGURATION: Release
    container_name: Senswave.Perf.Seed
    profiles: [ seed ]
    restart: "no"
    cpuset: ${SIDE_CPUSET:-0-1}
    networks: [ perf ]
    volumes:
      - ${CERT_DIR:-../../docker/https}:/https:ro
    environment:
      SSL_CERT_FILE: ${SEED_SSL_CERT_FILE:-/https/senswave.api.crt}
      PostgreSql__Host: ${POSTGRES_HOST}
      PostgreSql__Port: ${POSTGRES_PORT:-5432}
      PostgreSql__Username: ${POSTGRES_USER}
      PostgreSql__Password: ${POSTGRES_PASSWORD}
      PostgreSql__Database: senswave_seed
      Seed__Instance: https://${PERF_API_HOST:-api}:8443/api
      Seed__GeneratedUsers__Count: ${SEED_USER_COUNT:-100}
      Seed__GeneratedUsers__EmailPattern: user{0}@gmail.com
      Seed__GeneratedUsers__Password: User123456!
      Seed__GeneratedUsers__Broker__Create: "true"
      Seed__GeneratedUsers__Broker__Name: Shared Broker
      Seed__GeneratedUsers__Broker__Url: ${SEED_BROKER_URL}
      Seed__GeneratedUsers__Broker__Port: ${SEED_BROKER_PORT}
      Seed__GeneratedUsers__Broker__Username: ${SEED_BROKER_USERNAME}
      Seed__GeneratedUsers__Broker__Password: ${SEED_BROKER_PASSWORD}
      Seed__GeneratedUsers__Devices__PicoController__GlobalTopic: perf/controller/{0}
      Seed__DefaultUser__Broker__Url: ${SEED_BROKER_URL}
      Seed__DefaultUser__Broker__Port: ${SEED_BROKER_PORT}
      Seed__DefaultUser__Broker__Username: ${SEED_BROKER_USERNAME}
      Seed__DefaultUser__Broker__Password: ${SEED_BROKER_PASSWORD}
      Seed__DefaultUser__Devices__PicoController__GlobalTopic: perf/controller
      # The static seed user list contains user1@gmail.com, which collides with generated user 1.
      Seed__Users__1__Email: static.user1@gmail.com

networks:
  perf:
    name: senswave-perf
```

- [ ] **Step 5: Write `performance/local/mosquitto/mosquitto.conf` and `performance/local/compose.yml`**

`mosquitto.conf`:
```
listener 1883
allow_anonymous true
```

`compose.yml`:
```yaml
# Local phase: the VM stack plus stand-ins for Azure Flexible Server and the external broker.
#   docker compose -f performance/local/compose.yml --env-file performance/stack/.env --profile seed up --no-start
#   docker compose -f performance/local/compose.yml --env-file performance/stack/.env up -d database mqtt
include:
  - path: ../stack/compose.yml
    env_file: ../stack/.env

services:
  database:
    image: postgres:17.6
    container_name: Senswave.Perf.Database
    cpuset: ${SIDE_CPUSET:-0-1}
    networks: [ perf ]
    ports: [ "5432:5432" ]
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - perf-database-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD", "pg_isready", "-q", "-U", "${POSTGRES_USER}"]
      interval: 5s
      timeout: 5s
      retries: 15

  mqtt:
    image: eclipse-mosquitto:2.0.21
    container_name: Senswave.Perf.Mqtt
    networks: [ perf ]
    ports: [ "1883:1883" ]
    volumes:
      - ./mosquitto/mosquitto.conf:/mosquitto/config/mosquitto.conf:ro

volumes:
  perf-database-data:
```

- [ ] **Step 6: Write `performance/.gitignore` and delete the old files**

```
results/
**/.env
```

Run: `git rm performance/docker-compose.yml performance/.env.template`

- [ ] **Step 7: Verify locally**

```bash
cp performance/stack/.env.template performance/stack/.env
sh docker/generate-cert.sh   # only if docker/https/senswave.api.pfx is missing
docker compose -f performance/local/compose.yml --env-file performance/stack/.env config > /dev/null
docker compose -f performance/local/compose.yml --env-file performance/stack/.env --profile seed up --no-start --build
docker compose -f performance/local/compose.yml --env-file performance/stack/.env up -d database mqtt
docker start Senswave.Perf.Rest
```
Expected: `config` prints nothing on stderr; after ~30 s `docker inspect -f '{{.State.Health.Status}}' Senswave.Perf.Rest` prints `healthy` (the API migrates the empty `senswave_run`, which Postgres does not have yet — if startup fails because the database is missing, create it once: `docker exec Senswave.Perf.Database createdb -U postgres senswave_run`, and note this in the README problems section; the agent creates it for real runs). Then `docker stop Senswave.Perf.Rest`.

If the local machine has fewer than 4 vCPUs, set `API_CPUSET=0-1` and `SIDE_CPUSET=0-1` in `.env` and note it.

- [ ] **Step 8: Commit**

```bash
git add performance/stack performance/local performance/.gitignore
git commit -m "feat: perf VM stack and local compose"
```

---

### Task 3: Perf solution and Perf.Core basics

**Files:**
- Create: `performance/tools/Perf.sln`, `performance/tools/Perf.Core/Perf.Core.csproj`, `performance/tools/Perf.UnitTests/Perf.UnitTests.csproj`
- Create: `performance/tools/Perf.Core/Transports.cs`, `Campaigns/Campaign.cs`, `Campaigns/CampaignLoader.cs`, `Campaigns/CampaignPlanner.cs`, `Runs/RunData.cs`, `Agent/AgentContracts.cs`, `PerfJson.cs`
- Test: `performance/tools/Perf.UnitTests/CampaignLoaderTests.cs`, `CampaignPlannerTests.cs`

**Interfaces:**
- Produces (namespace `Perf.Core`):
  - `enum Transport { Rest, Grpc, GrpcWeb }`, `enum Scenario { S1, S2, S3, S4, S5, S6a, S6b }`, `static bool ScenarioExtensions.IsStream(this Scenario)`.
  - `record Campaign(CampaignTarget Target, int Rounds, Transport[] Transports, Scenario[] Scenarios, int[] Rates, bool[] Otel, int WarmupSeconds, int DurationSeconds, CampaignUsers Users, CampaignExport Export)`; `record CampaignTarget(Uri Api, Uri Agent, bool InsecureTls)`; `record CampaignUsers(string EmailPattern, int FirstIndex, int Count, string Password)`; `record CampaignExport(bool Traces, bool RawK6)`.
  - `static Campaign CampaignLoader.Parse(string json)` throws `CampaignException` (message names the bad field).
  - `record RunSpec(string RunId, int Round, Transport Transport, Scenario Scenario, int Rate, bool Otel)`; `static IReadOnlyList<RunSpec> CampaignPlanner.Expand(Campaign, int seed)`.
  - `record UserData(string Email, string AccessToken, string HomeId, string DeviceId, string DashboardId)`; `record RunData(IReadOnlyList<UserData> Users)`.
  - `record ActivateRequest(Transport Transport, bool Otel, string RunId)`; `record ContainerInfo(string Name, string State, string? Health, string? CpusetCpus, double Cpus, long MemoryBytes, string? ImageId, string? GitSha)`; `record AgentStatus(Transport? Active, bool Healthy, IReadOnlyList<ContainerInfo> Containers)`.
  - `static JsonSerializerOptions PerfJson.Options` (camelCase, enums as strings, indented).

- [ ] **Step 1: Create the projects**

```bash
cd performance/tools
dotnet new sln -n Perf
dotnet new classlib -n Perf.Core -f net10.0 && rm Perf.Core/Class1.cs
dotnet new xunit -n Perf.UnitTests -f net10.0 && rm Perf.UnitTests/UnitTest1.cs
dotnet sln Perf.sln add Perf.Core Perf.UnitTests
dotnet add Perf.UnitTests reference Perf.Core
```

Replace `Perf.UnitTests/Perf.UnitTests.csproj` package references with central-managed ones (no versions):

```xml
  <ItemGroup>
    <PackageReference Include="coverlet.collector">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Moq" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
```

Run: `dotnet build performance/tools/Perf.sln` → Expected: Build succeeded (central package management resolves versions from the repo root).

- [ ] **Step 2: Write `Transports.cs`, `PerfJson.cs`, `Runs/RunData.cs`, `Agent/AgentContracts.cs`**

```csharp
namespace Perf.Core;

public enum Transport { Rest, Grpc, GrpcWeb }

public enum Scenario { S1, S2, S3, S4, S5, S6a, S6b }

public static class ScenarioExtensions
{
    public static bool IsStream(this Scenario scenario) => scenario is Scenario.S6a or Scenario.S6b;
}
```

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Perf.Core;

public static class PerfJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
```

```csharp
namespace Perf.Core.Runs;

public sealed record UserData(string Email, string AccessToken, string HomeId, string DeviceId, string DashboardId);

public sealed record RunData(IReadOnlyList<UserData> Users);
```

```csharp
namespace Perf.Core.Agent;

public sealed record ActivateRequest(Transport Transport, bool Otel, string RunId);

public sealed record ContainerInfo(
    string Name, string State, string? Health, string? CpusetCpus, double Cpus, long MemoryBytes, string? ImageId, string? GitSha);

public sealed record AgentStatus(Transport? Active, bool Healthy, IReadOnlyList<ContainerInfo> Containers);
```

- [ ] **Step 3: Write the failing loader tests** (`CampaignLoaderTests.cs`)

```csharp
using Perf.Core;
using Perf.Core.Campaigns;

namespace Perf.UnitTests;

public class CampaignLoaderTests
{
    private const string Valid = """
    {
      "target": { "api": "https://localhost:8443", "agent": "https://localhost:9443", "insecureTls": true },
      "rounds": 3,
      "transports": ["Rest", "Grpc", "GrpcWeb"],
      "scenarios": ["S1", "S6a"],
      "rates": [100, 500, 1000],
      "otel": [true, false],
      "warmupSeconds": 30,
      "durationSeconds": 300,
      "users": { "emailPattern": "user{0}@gmail.com", "firstIndex": 1, "count": 100, "password": "User123456!" },
      "export": { "traces": true, "rawK6": false }
    }
    """;

    [Fact]
    public void ParsesValidCampaign()
    {
        var campaign = CampaignLoader.Parse(Valid);

        Assert.Equal(3, campaign.Rounds);
        Assert.Equal([Transport.Rest, Transport.Grpc, Transport.GrpcWeb], campaign.Transports);
        Assert.Equal([Scenario.S1, Scenario.S6a], campaign.Scenarios);
        Assert.Equal([100, 500, 1000], campaign.Rates);
        Assert.True(campaign.Target.InsecureTls);
        Assert.Equal(100, campaign.Users.Count);
    }

    [Theory]
    [InlineData("\"rounds\": 3", "\"rounds\": 0", "rounds")]
    [InlineData("\"rates\": [100, 500, 1000]", "\"rates\": []", "rates")]
    [InlineData("\"durationSeconds\": 300", "\"durationSeconds\": 0", "durationSeconds")]
    [InlineData("\"transports\": [\"Rest\", \"Grpc\", \"GrpcWeb\"]", "\"transports\": [\"Rest\", \"Rest\"]", "transports")]
    public void RejectsInvalidField(string from, string to, string field)
    {
        var ex = Assert.Throws<CampaignException>(() => CampaignLoader.Parse(Valid.Replace(from, to)));

        Assert.Contains(field, ex.Message);
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test performance/tools/Perf.sln --filter CampaignLoaderTests`
Expected: FAIL (compile error: `CampaignLoader` not found).

- [ ] **Step 5: Implement `Campaign.cs` and `CampaignLoader.cs`**

```csharp
namespace Perf.Core.Campaigns;

public sealed record CampaignTarget(Uri Api, Uri Agent, bool InsecureTls);

public sealed record CampaignUsers(string EmailPattern, int FirstIndex, int Count, string Password)
{
    public string Email(int i) => string.Format(EmailPattern, FirstIndex + i);
}

public sealed record CampaignExport(bool Traces, bool RawK6);

public sealed record Campaign(
    CampaignTarget Target,
    int Rounds,
    Transport[] Transports,
    Scenario[] Scenarios,
    int[] Rates,
    bool[] Otel,
    int WarmupSeconds,
    int DurationSeconds,
    CampaignUsers Users,
    CampaignExport Export);

public sealed class CampaignException(string message) : Exception(message);
```

```csharp
using System.Text.Json;

namespace Perf.Core.Campaigns;

public static class CampaignLoader
{
    public static Campaign Parse(string json)
    {
        Campaign? campaign;
        try
        {
            campaign = JsonSerializer.Deserialize<Campaign>(json, PerfJson.Options);
        }
        catch (JsonException ex)
        {
            throw new CampaignException($"Invalid campaign JSON: {ex.Message}");
        }

        if (campaign is null)
            throw new CampaignException("Campaign is empty.");

        Require(campaign.Target is not null, "target");
        Require(campaign.Rounds > 0, "rounds");
        Require(NonEmptyDistinct(campaign.Transports), "transports");
        Require(NonEmptyDistinct(campaign.Scenarios), "scenarios");
        Require(NonEmptyDistinct(campaign.Rates) && campaign.Rates.All(r => r > 0), "rates");
        Require(NonEmptyDistinct(campaign.Otel), "otel");
        Require(campaign.WarmupSeconds >= 0, "warmupSeconds");
        Require(campaign.DurationSeconds > 0, "durationSeconds");
        Require(campaign.Users is { Count: > 0 } && campaign.Users.EmailPattern.Contains("{0}"), "users");

        return campaign with { Export = campaign.Export ?? new CampaignExport(false, false) };
    }

    private static bool NonEmptyDistinct<T>(T[]? values) => values is { Length: > 0 } && values.Distinct().Count() == values.Length;

    private static void Require(bool condition, string field)
    {
        if (!condition)
            throw new CampaignException($"Campaign field '{field}' is missing or invalid.");
    }
}
```

- [ ] **Step 6: Run loader tests** → `dotnet test performance/tools/Perf.sln --filter CampaignLoaderTests` → Expected: PASS.

- [ ] **Step 7: Write the failing planner tests** (`CampaignPlannerTests.cs`)

```csharp
using Perf.Core;
using Perf.Core.Campaigns;

namespace Perf.UnitTests;

public class CampaignPlannerTests
{
    private static Campaign Campaign(int rounds = 3) => new(
        new CampaignTarget(new Uri("https://localhost:8443"), new Uri("https://localhost:9443"), true),
        rounds,
        [Transport.Rest, Transport.Grpc, Transport.GrpcWeb],
        [Scenario.S1, Scenario.S4],
        [100, 500],
        [true],
        30, 300,
        new CampaignUsers("user{0}@gmail.com", 1, 100, "x"),
        new CampaignExport(false, false));

    [Fact]
    public void ProducesEveryCombinationOnce()
    {
        var runs = CampaignPlanner.Expand(Campaign(), seed: 42);

        Assert.Equal(3 * 3 * 2 * 2, runs.Count);
        Assert.Equal(runs.Count, runs.Select(r => r.RunId).Distinct().Count());
    }

    [Fact]
    public void KeepsTransportBlocksTogetherWithinARound()
    {
        var round1 = CampaignPlanner.Expand(Campaign(), 42).Where(r => r.Round == 1).ToList();

        var blocks = round1.Select(r => r.Transport).Distinct().ToList();
        Assert.Equal(3, blocks.Count);
        // Each transport's runs are contiguous.
        foreach (var transport in blocks)
        {
            var indexes = round1.Select((r, i) => (r, i)).Where(x => x.r.Transport == transport).Select(x => x.i).ToList();
            Assert.Equal(indexes.Count - 1, indexes[^1] - indexes[0]);
        }
    }

    [Fact]
    public void SameSeedSameOrder_DifferentSeedsEventuallyDiffer()
    {
        var a = CampaignPlanner.Expand(Campaign(), 7).Select(r => r.RunId);
        var b = CampaignPlanner.Expand(Campaign(), 7).Select(r => r.RunId);
        Assert.Equal(a, b);

        var orders = Enumerable.Range(0, 20)
            .Select(seed => string.Join(",", CampaignPlanner.Expand(Campaign(1), seed).Select(r => r.Transport).Distinct()))
            .Distinct();
        Assert.True(orders.Count() > 1);
    }

    [Fact]
    public void RunIdIsReadable()
    {
        var run = CampaignPlanner.Expand(Campaign(1), 1).First(r => r.Transport == Transport.GrpcWeb && r.Scenario == Scenario.S4 && r.Rate == 500);

        Assert.Equal("r1-grpcweb-s4-500-otel", run.RunId);
    }
}
```

- [ ] **Step 8: Run to verify failure** → `dotnet test performance/tools/Perf.sln --filter CampaignPlannerTests` → Expected: FAIL (compile error).

- [ ] **Step 9: Implement `CampaignPlanner.cs`**

```csharp
namespace Perf.Core.Campaigns;

public sealed record RunSpec(string RunId, int Round, Transport Transport, Scenario Scenario, int Rate, bool Otel);

public static class CampaignPlanner
{
    /// <summary>
    /// Rounds in order; within a round, the transport order is shuffled (seeded per round) and each transport's
    /// runs stay together, so transports interleave across rounds and noise spreads evenly.
    /// </summary>
    public static IReadOnlyList<RunSpec> Expand(Campaign campaign, int seed)
    {
        var runs = new List<RunSpec>();

        for (var round = 1; round <= campaign.Rounds; round++)
        {
            var random = new Random(HashCode.Combine(seed, round));
            var transports = campaign.Transports.OrderBy(_ => random.Next()).ToArray();

            foreach (var otel in campaign.Otel)
            foreach (var transport in transports)
            foreach (var scenario in campaign.Scenarios)
            foreach (var rate in campaign.Rates)
                runs.Add(new RunSpec(RunId(round, transport, scenario, rate, otel), round, transport, scenario, rate, otel));
        }

        return runs;
    }

    private static string RunId(int round, Transport transport, Scenario scenario, int rate, bool otel) =>
        $"r{round}-{transport.ToString().ToLowerInvariant()}-{scenario.ToString().ToLowerInvariant()}-{rate}-{(otel ? "otel" : "nootel")}";
}
```

- [ ] **Step 10: Run all tests** → `dotnet test performance/tools/Perf.sln` → Expected: PASS.

- [ ] **Step 11: Commit**

```bash
git add performance/tools
git commit -m "feat: perf solution with campaign loading and planning"
```

---

### Task 4: Perf.Core run logic (gates, summaries, tracker, CPU, queries, blob URL)

**Files:**
- Create in `performance/tools/Perf.Core/`: `Runs/Gates.cs`, `Runs/K6Summary.cs`, `Runs/ProcStat.cs`, `Runs/RunMeta.cs`, `Streams/DeliveryTracker.cs`, `Streams/Percentiles.cs`, `Export/ExportQueries.cs`, `Export/BlobUrl.cs`
- Test: `performance/tools/Perf.UnitTests/GatesTests.cs`, `K6SummaryTests.cs`, `ProcStatTests.cs`, `DeliveryTrackerTests.cs`, `ExportQueriesTests.cs`, `BlobUrlTests.cs`

**Interfaces:**
- Consumes: `Transport`, `Scenario`, `RunSpec` (Task 3).
- Produces:
  - `record RunMetrics(long DroppedIterations, double ErrorRate, double LoadGeneratorCpu)`; `record GateResult(bool Passed, IReadOnlyList<string> Failures)`; `static GateResult Gates.Evaluate(RunMetrics m, double maxErrorRate = 0.01, double maxLoadCpu = 0.80)`.
  - `static (long Dropped, double ErrorRate) K6Summary.Read(string summaryExportJson)`.
  - `record CpuTimes(long Busy, long Total)`; `static CpuTimes ProcStat.Parse(string firstLineOfProcStat)`; `static double ProcStat.Utilisation(CpuTimes a, CpuTimes b)`.
  - `record RunMeta(...)` (fields below), serialized with `PerfJson.Options` to `meta.json`.
  - `class DeliveryTracker` with `void Published(string deviceId, long timestampTicks, bool measured)`, `bool Delivered(string deviceId, long timestampTicks)`, `StreamResult Finish(long nowTicks, TimeSpan grace)`; `record StreamResult(long Published, long Delivered, long Lost, long Unmatched, double[] LatenciesMs)`.
  - `static double[] Percentiles.Of(double[] values, params double[] ps)` (nearest-rank, ps in 0..100).
  - `record ExportQuery(string File, bool Range, string PromQl)`; `static IReadOnlyList<ExportQuery> ExportQueries.For(RunMeta meta)`.
  - `static Uri BlobUrl.Build(Uri containerSasUrl, string blobName)`.

`RunMeta`:

```csharp
namespace Perf.Core.Runs;

public sealed record RunMeta(
    string RunId,
    int Attempt,
    int Round,
    Transport Transport,
    Scenario Scenario,
    int Rate,
    bool Otel,
    int ShuffleSeed,
    string? GitSha,
    string? ImageId,
    IReadOnlyList<Agent.ContainerInfo> Containers,
    DateTimeOffset? WarmupStartUtc,
    DateTimeOffset? MeasureStartUtc,
    DateTimeOffset? MeasureEndUtc,
    string Status,                 // "valid" | "invalid" | "failed-verification" | "failed"
    IReadOnlyList<string> Failures);
```

- [ ] **Step 1: Write the failing tests**

`GatesTests.cs`:
```csharp
using Perf.Core.Runs;

namespace Perf.UnitTests;

public class GatesTests
{
    [Fact]
    public void PassesCleanRun() => Assert.True(Gates.Evaluate(new RunMetrics(0, 0.001, 0.5)).Passed);

    [Theory]
    [InlineData(3, 0.0, 0.1, "dropped_iterations")]
    [InlineData(0, 0.02, 0.1, "error rate")]
    [InlineData(0, 0.0, 0.9, "load generator CPU")]
    public void FailsAndNamesTheGate(long dropped, double errors, double cpu, string expected)
    {
        var result = Gates.Evaluate(new RunMetrics(dropped, errors, cpu));

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, f => f.Contains(expected));
    }
}
```

`K6SummaryTests.cs`:
```csharp
using Perf.Core.Runs;

namespace Perf.UnitTests;

public class K6SummaryTests
{
    [Fact]
    public void ReadsDroppedAndCheckFailures()
    {
        const string json = """
        { "metrics": {
            "dropped_iterations": { "count": 12, "rate": 0.04 },
            "checks": { "passes": 990, "fails": 10, "value": 0.99 } } }
        """;

        var (dropped, errorRate) = K6Summary.Read(json);

        Assert.Equal(12, dropped);
        Assert.Equal(0.01, errorRate, 6);
    }

    [Fact]
    public void MissingDroppedMeansZero()
    {
        var (dropped, errorRate) = K6Summary.Read("""{ "metrics": { "checks": { "passes": 5, "fails": 0, "value": 1 } } }""");

        Assert.Equal(0, dropped);
        Assert.Equal(0, errorRate);
    }
}
```

`ProcStatTests.cs`:
```csharp
using Perf.Core.Runs;

namespace Perf.UnitTests;

public class ProcStatTests
{
    [Fact]
    public void ComputesUtilisationBetweenSamples()
    {
        // user nice system idle iowait irq softirq steal
        var a = ProcStat.Parse("cpu  100 0 100 800 0 0 0 0 0 0");
        var b = ProcStat.Parse("cpu  200 0 200 1400 0 0 0 0 0 0");

        Assert.Equal(200.0 / 800.0, ProcStat.Utilisation(a, b), 6);
    }
}
```

`DeliveryTrackerTests.cs`:
```csharp
using Perf.Core.Streams;

namespace Perf.UnitTests;

public class DeliveryTrackerTests
{
    private static long Ms(double ms) => TimeSpan.FromMilliseconds(ms).Ticks;

    [Fact]
    public void MatchesDeliveriesToOldestPendingPublishPerDevice()
    {
        var tracker = new DeliveryTracker();
        tracker.Published("d1", Ms(0), measured: true);
        tracker.Published("d1", Ms(10), measured: true);
        tracker.Published("d2", Ms(5), measured: true);

        Assert.True(tracker.Delivered("d1", Ms(20)));   // matches the publish at 0 → 20 ms
        Assert.True(tracker.Delivered("d2", Ms(8)));    // 3 ms
        Assert.True(tracker.Delivered("d1", Ms(25)));   // matches the publish at 10 → 15 ms

        var result = tracker.Finish(Ms(10_000), TimeSpan.FromSeconds(5));
        Assert.Equal(3, result.Published);
        Assert.Equal(3, result.Delivered);
        Assert.Equal(0, result.Lost);
        Assert.Equal(new[] { 3.0, 15.0, 20.0 }, result.LatenciesMs.Order().ToArray());
    }

    [Fact]
    public void WarmupPublishesAreConsumedButNotRecorded_UnmatchedAndLostAreCounted()
    {
        var tracker = new DeliveryTracker();
        tracker.Published("d1", Ms(0), measured: false);
        tracker.Published("d1", Ms(1), measured: true);
        tracker.Published("d1", Ms(2), measured: true);

        tracker.Delivered("d1", Ms(5));                 // warm-up publish, not recorded
        tracker.Delivered("d1", Ms(6));                 // 5 ms, recorded
        Assert.False(tracker.Delivered("d9", Ms(7)));   // nothing pending → unmatched

        var result = tracker.Finish(Ms(10_000), TimeSpan.FromSeconds(5));
        Assert.Equal(2, result.Published);
        Assert.Equal(1, result.Delivered);
        Assert.Equal(1, result.Lost);                   // publish at 2 ms never delivered
        Assert.Equal(1, result.Unmatched);
        Assert.Equal(new[] { 5.0 }, result.LatenciesMs);
    }

    [Fact]
    public void PercentilesUseNearestRank()
    {
        var values = Enumerable.Range(1, 100).Select(i => (double)i).ToArray();

        Assert.Equal(new[] { 50.0, 95.0, 99.0, 100.0 }, Percentiles.Of(values, 50, 95, 99, 100));
    }
}
```

`ExportQueriesTests.cs`:
```csharp
using Perf.Core;
using Perf.Core.Export;
using Perf.Core.Runs;

namespace Perf.UnitTests;

public class ExportQueriesTests
{
    private static RunMeta Meta() => new(
        "r1-grpc-s1-100-otel", 1, 1, Transport.Grpc, Scenario.S1, 100, true, 1, "sha", "img", [],
        DateTimeOffset.Parse("2026-11-01T10:00:00Z"), DateTimeOffset.Parse("2026-11-01T10:00:30Z"),
        DateTimeOffset.Parse("2026-11-01T10:05:30Z"), "valid", []);

    [Fact]
    public void LatencyIsOneWholeWindowQuantilePerPercentile()
    {
        var latency = ExportQueries.For(Meta()).Where(q => q.File == "latency.csv").ToList();

        Assert.Equal(3, latency.Count);
        Assert.All(latency, q =>
        {
            Assert.False(q.Range);
            Assert.Contains("run_id=\"r1-grpc-s1-100-otel\"", q.PromQl);
            Assert.Contains("[300s]", q.PromQl);
            Assert.Contains("http_server_request_duration_seconds_bucket", q.PromQl);
        });
    }

    [Fact]
    public void ContainerQueriesFilterByContainerName()
    {
        var cpu = ExportQueries.For(Meta()).First(q => q.File == "cpu.csv" && q.PromQl.Contains("container_cpu"));

        Assert.True(cpu.Range);
        Assert.Contains("name=\"Senswave.Perf.Grpc\"", cpu.PromQl);
    }
}
```

`BlobUrlTests.cs`:
```csharp
using Perf.Core.Export;

namespace Perf.UnitTests;

public class BlobUrlTests
{
    [Fact]
    public void InsertsBlobNameBeforeSasQuery()
    {
        var url = BlobUrl.Build(new Uri("https://acct.blob.core.windows.net/perf?sv=2022&sig=abc"), "smoke-2026.zip");

        Assert.Equal("https://acct.blob.core.windows.net/perf/smoke-2026.zip?sv=2022&sig=abc", url.ToString());
    }
}
```

- [ ] **Step 2: Run to verify failure** → `dotnet test performance/tools/Perf.sln` → Expected: FAIL (compile errors for the new types).

- [ ] **Step 3: Implement**

`Runs/Gates.cs`:
```csharp
namespace Perf.Core.Runs;

public sealed record RunMetrics(long DroppedIterations, double ErrorRate, double LoadGeneratorCpu);

public sealed record GateResult(bool Passed, IReadOnlyList<string> Failures);

public static class Gates
{
    public static GateResult Evaluate(RunMetrics metrics, double maxErrorRate = 0.01, double maxLoadCpu = 0.80)
    {
        var failures = new List<string>();

        if (metrics.DroppedIterations > 0)
            failures.Add($"dropped_iterations = {metrics.DroppedIterations} (load generator could not keep the rate)");
        if (metrics.ErrorRate >= maxErrorRate)
            failures.Add($"error rate {metrics.ErrorRate:P2} >= {maxErrorRate:P0}");
        if (metrics.LoadGeneratorCpu > maxLoadCpu)
            failures.Add($"load generator CPU {metrics.LoadGeneratorCpu:P0} > {maxLoadCpu:P0}");

        return new GateResult(failures.Count == 0, failures);
    }
}
```

`Runs/K6Summary.cs`:
```csharp
using System.Text.Json.Nodes;

namespace Perf.Core.Runs;

/// <summary>Reads k6 <c>--summary-export</c> output. Errors are failed checks, which works for every transport.</summary>
public static class K6Summary
{
    public static (long Dropped, double ErrorRate) Read(string json)
    {
        var metrics = JsonNode.Parse(json)?["metrics"] ?? throw new FormatException("k6 summary has no metrics.");

        var dropped = metrics["dropped_iterations"]?["count"]?.GetValue<double>() ?? 0;
        var passes = metrics["checks"]?["passes"]?.GetValue<double>() ?? 0;
        var fails = metrics["checks"]?["fails"]?.GetValue<double>() ?? 0;

        return ((long)dropped, passes + fails == 0 ? 1.0 : fails / (passes + fails));
    }
}
```

`Runs/ProcStat.cs`:
```csharp
namespace Perf.Core.Runs;

public sealed record CpuTimes(long Busy, long Total);

/// <summary>Parses the aggregate <c>cpu</c> line of <c>/proc/stat</c>. Idle = idle + iowait.</summary>
public static class ProcStat
{
    public static CpuTimes Parse(string line)
    {
        var values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(long.Parse).ToArray();
        var total = values.Take(8).Sum();
        var idle = values[3] + values[4];

        return new CpuTimes(total - idle, total);
    }

    public static double Utilisation(CpuTimes a, CpuTimes b) =>
        b.Total == a.Total ? 0 : (double)(b.Busy - a.Busy) / (b.Total - a.Total);
}
```

`Streams/Percentiles.cs`:
```csharp
namespace Perf.Core.Streams;

public static class Percentiles
{
    public static double[] Of(double[] values, params double[] ps)
    {
        if (values.Length == 0)
            return ps.Select(_ => double.NaN).ToArray();

        var sorted = values.Order().ToArray();

        return ps.Select(p => sorted[Math.Clamp((int)Math.Ceiling(p / 100.0 * sorted.Length) - 1, 0, sorted.Length - 1)]).ToArray();
    }
}
```

`Streams/DeliveryTracker.cs`:
```csharp
namespace Perf.Core.Streams;

public sealed record StreamResult(long Published, long Delivered, long Lost, long Unmatched, double[] LatenciesMs);

/// <summary>
/// Live updates carry only a device id, so each delivery is matched to the oldest pending publish for that device (FIFO).
/// Publishes made during warm-up are consumed but not recorded. Thread-safe.
/// </summary>
public sealed class DeliveryTracker
{
    private readonly Dictionary<string, Queue<(long Ticks, bool Measured)>> _pending = new();
    private readonly List<double> _latencies = new();
    private readonly object _lock = new();
    private long _published, _delivered, _unmatched;

    public void Published(string deviceId, long timestampTicks, bool measured)
    {
        lock (_lock)
        {
            if (!_pending.TryGetValue(deviceId, out var queue))
                _pending[deviceId] = queue = new Queue<(long, bool)>();

            queue.Enqueue((timestampTicks, measured));
            if (measured)
                _published++;
        }
    }

    public bool Delivered(string deviceId, long timestampTicks)
    {
        lock (_lock)
        {
            if (!_pending.TryGetValue(deviceId, out var queue) || queue.Count == 0)
            {
                _unmatched++;
                return false;
            }

            var (published, measured) = queue.Dequeue();
            if (measured)
            {
                _delivered++;
                _latencies.Add(TimeSpan.FromTicks(timestampTicks - published).TotalMilliseconds);
            }

            return true;
        }
    }

    public StreamResult Finish(long nowTicks, TimeSpan grace)
    {
        lock (_lock)
        {
            var cutoff = nowTicks - grace.Ticks;
            var lost = _pending.Values.SelectMany(q => q).LongCount(p => p.Measured && p.Ticks <= cutoff);

            return new StreamResult(_published, _delivered, lost, _unmatched, _latencies.ToArray());
        }
    }
}
```

`Export/ExportQueries.cs` (metric names follow the OTLP→Prometheus conversion used by Grafana Cloud; Task 11 Step 6 verifies them against the live label list and fixes this file if a name differs):
```csharp
using Perf.Core.Runs;

namespace Perf.Core.Export;

public sealed record ExportQuery(string File, bool Range, string PromQl);

public static class ExportQueries
{
    public static string ContainerName(Transport transport) => transport switch
    {
        Transport.Rest => "Senswave.Perf.Rest",
        Transport.Grpc => "Senswave.Perf.Grpc",
        _ => "Senswave.Perf.GrpcWeb",
    };

    public static IReadOnlyList<ExportQuery> For(RunMeta meta)
    {
        var run = $"run_id=\"{meta.RunId}\"";
        var container = $"name=\"{ContainerName(meta.Transport)}\"";
        var window = (int)(meta.MeasureEndUtc!.Value - meta.MeasureStartUtc!.Value).TotalSeconds;

        var list = new List<ExportQuery>();

        foreach (var q in new[] { "0.5", "0.95", "0.99" })
            list.Add(new("latency.csv", false,
                $"histogram_quantile({q}, sum by (le) (increase(http_server_request_duration_seconds_bucket{{{run}}}[{window}s])))"));

        list.Add(new("throughput.csv", true, $"sum(rate(http_server_request_duration_seconds_count{{{run}}}[30s]))"));
        list.Add(new("throughput.csv", true, $"sum by (error_type) (rate(http_server_request_duration_seconds_count{{{run},error_type!=\"\"}}[30s]))"));
        list.Add(new("cpu.csv", true, $"rate(container_cpu_usage_seconds_total{{{container}}}[30s])"));
        list.Add(new("cpu.csv", true, $"rate(process_cpu_time_seconds_total{{{run}}}[30s])"));
        list.Add(new("memory.csv", true, $"container_memory_working_set_bytes{{{container}}}"));
        list.Add(new("gc.csv", true, $"rate(dotnet_gc_heap_total_allocated_bytes_total{{{run}}}[30s])"));
        list.Add(new("gc.csv", true, $"sum by (dotnet_gc_heap_generation) (rate(dotnet_gc_collections_total{{{run}}}[30s]))"));
        list.Add(new("gc.csv", true, $"dotnet_thread_pool_queue_length{{{run}}}"));
        list.Add(new("network.csv", true, $"rate(container_network_receive_bytes_total{{{container}}}[30s])"));
        list.Add(new("network.csv", true, $"rate(container_network_transmit_bytes_total{{{container}}}[30s])"));
        list.Add(new("connections.csv", true, $"kestrel_active_connections{{{run}}}"));
        list.Add(new("connections.csv", false,
            $"histogram_quantile(0.95, sum by (le) (increase(kestrel_tls_handshake_duration_seconds_bucket{{{run}}}[{window}s])))"));

        return list;
    }
}
```

`Export/BlobUrl.cs`:
```csharp
namespace Perf.Core.Export;

public static class BlobUrl
{
    public static Uri Build(Uri containerSasUrl, string blobName)
    {
        var builder = new UriBuilder(containerSasUrl);
        builder.Path = builder.Path.TrimEnd('/') + "/" + Uri.EscapeDataString(blobName);
        return builder.Uri;
    }
}
```

- [ ] **Step 4: Run tests** → `dotnet test performance/tools/Perf.sln` → Expected: PASS (all).

- [ ] **Step 5: Commit**

```bash
git add performance/tools
git commit -m "feat: perf run gates, summaries, delivery tracker and export queries"
```

---

### Task 5: PerfAgent

**Files:**
- Create: `performance/tools/PerfAgent/PerfAgent.csproj`, `Program.cs`, `AgentOptions.cs`, `BearerTokenMiddleware.cs`, `Docker/IDockerEngine.cs`, `Docker/DockerEngine.cs`, `Docker/ContainerSpec.cs`, `Docker/DockerLogDemuxer.cs`, `Database/IDatabaseAdmin.cs`, `Database/DatabaseAdmin.cs`, `Activation/ActivationService.cs`, `Dockerfile`
- Modify: `performance/stack/compose.yml` (add `perf-agent` service), `performance/stack/.env.template` (add `AGENT_TOKEN`)
- Test: `performance/tools/Perf.UnitTests/ContainerSpecTests.cs`, `DockerLogDemuxerTests.cs`, `ActivationServiceTests.cs`

**Interfaces:**
- Consumes: `Transport`, `ActivateRequest`, `AgentStatus`, `ContainerInfo`, `PerfJson` (Task 3).
- Produces HTTP API (all require `Authorization: Bearer <AGENT_TOKEN>`; JSON uses `PerfJson.Options`):
  - `GET /status` → `AgentStatus`
  - `POST /activate` body `ActivateRequest` → `AgentStatus` (200), `409` if another operation is running, `500` with `{ "error": "..." }` on failure
  - `POST /stop` → `AgentStatus`
  - `POST /prepare` → `{ "seedLog": "<last 200 lines>" }`
  - `GET /logs/{transport}?sinceUnix=<seconds>` → `text/plain`
- Internal: `interface IDockerEngine { Task<JsonObject> Inspect(string name, CancellationToken ct); Task Stop(string name, CancellationToken ct); Task Remove(string name, CancellationToken ct); Task Create(string name, JsonObject body, CancellationToken ct); Task Start(string name, CancellationToken ct); Task<string> Logs(string name, long sinceUnix, CancellationToken ct); }`; `interface IDatabaseAdmin { Task Recreate(string database, string? template, CancellationToken ct); }`; `static JsonObject ContainerSpec.BuildCreateBody(JsonObject inspect, IReadOnlyDictionary<string,string> envOverrides)`; `static string DockerLogDemuxer.Demux(byte[] raw)`.

- [ ] **Step 1: Create the project**

```bash
cd performance/tools
dotnet new web -n PerfAgent -f net10.0
dotnet sln Perf.sln add PerfAgent
dotnet add PerfAgent reference Perf.Core
dotnet add Perf.UnitTests reference PerfAgent
```

In `PerfAgent.csproj` add `<ItemGroup><PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" /></ItemGroup>` (brings Npgsql; no new package) and `<InternalsVisibleTo Include="Perf.UnitTests" />` inside an `ItemGroup`.

- [ ] **Step 2: Write the failing tests**

`ContainerSpecTests.cs`:
```csharp
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
```

`DockerLogDemuxerTests.cs`:
```csharp
using System.Text;
using PerfAgent.Docker;

namespace Perf.UnitTests;

public class DockerLogDemuxerTests
{
    private static byte[] Frame(byte stream, string text)
    {
        var payload = Encoding.UTF8.GetBytes(text);
        var header = new byte[] { stream, 0, 0, 0, 0, 0, 0, (byte)payload.Length };
        return header.Concat(payload).ToArray();
    }

    [Fact]
    public void JoinsStdoutAndStderrFrames()
    {
        var raw = Frame(1, "hello\n").Concat(Frame(2, "oops\n")).ToArray();

        Assert.Equal("hello\noops\n", DockerLogDemuxer.Demux(raw));
    }
}
```

`ActivationServiceTests.cs`:
```csharp
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
                $$"""{"Name":"/{{n}}","Config":{"Env":[],"Labels":{"senswave.git-sha":"abc"}},"Image":"sha256:1","HostConfig":{},
                    "State":{"Status":"running","Health":{"Status":"healthy"}},"NetworkSettings":{"Networks":{}}}""")!.AsObject());
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
```

- [ ] **Step 3: Run to verify failure** → `dotnet test performance/tools/Perf.sln` → Expected: FAIL (compile errors).

- [ ] **Step 4: Implement `AgentOptions.cs`, `Docker/ContainerSpec.cs`, `Docker/DockerLogDemuxer.cs`**

```csharp
using Perf.Core;

namespace PerfAgent;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public string Token { get; set; } = "";
    /// <summary>Admin connection string to the server's <c>postgres</c> database.</summary>
    public string PostgresAdmin { get; set; } = "";
    public string RunDatabase { get; set; } = "senswave_run";
    public string TemplateDatabase { get; set; } = "senswave_template";
    public string SeedDatabase { get; set; } = "senswave_seed";
    public string SeedContainer { get; set; } = "Senswave.Perf.Seed";
    public string DockerSocket { get; set; } = "/var/run/docker.sock";
    public int HealthTimeoutSeconds { get; set; } = 180;
    public int SeedTimeoutMinutes { get; set; } = 30;
    public Dictionary<Transport, string> ApiContainers { get; set; } = new()
    {
        [Transport.Rest] = "Senswave.Perf.Rest",
        [Transport.Grpc] = "Senswave.Perf.Grpc",
        [Transport.GrpcWeb] = "Senswave.Perf.GrpcWeb",
    };
}
```

```csharp
using System.Text.Json.Nodes;

namespace PerfAgent.Docker;

/// <summary>
/// Builds a Docker Engine "create container" body from an inspect result, so a container can be recreated with
/// different env vars. Bind mounts keep the host paths compose resolved; runtime-only fields are dropped.
/// </summary>
public static class ContainerSpec
{
    private static readonly string[] RuntimeConfigFields = ["Hostname", "Domainname"];

    public static JsonObject BuildCreateBody(JsonObject inspect, IReadOnlyDictionary<string, string> envOverrides)
    {
        var body = inspect["Config"]!.DeepClone().AsObject();
        foreach (var field in RuntimeConfigFields)
            body.Remove(field);

        var env = (body["Env"]?.AsArray().Select(e => e!.GetValue<string>()) ?? [])
            .Where(e => !envOverrides.ContainsKey(e.Split('=', 2)[0]))
            .Concat(envOverrides.Select(kv => $"{kv.Key}={kv.Value}"));
        body["Env"] = new JsonArray(env.Select(e => (JsonNode)JsonValue.Create(e)!).ToArray());

        body["HostConfig"] = inspect["HostConfig"]?.DeepClone() ?? new JsonObject();

        var endpoints = new JsonObject();
        if (inspect["NetworkSettings"]?["Networks"] is JsonObject networks)
            foreach (var (name, network) in networks)
                endpoints[name] = new JsonObject { ["Aliases"] = network?["Aliases"]?.DeepClone() ?? new JsonArray() };
        body["NetworkingConfig"] = new JsonObject { ["EndpointsConfig"] = endpoints };

        return body;
    }
}
```

```csharp
using System.Buffers.Binary;
using System.Text;

namespace PerfAgent.Docker;

/// <summary>Docker multiplexes stdout/stderr of non-TTY containers in 8-byte-header frames.</summary>
public static class DockerLogDemuxer
{
    public static string Demux(byte[] raw)
    {
        var text = new StringBuilder();
        var offset = 0;

        while (offset + 8 <= raw.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(raw.AsSpan(offset + 4, 4));
            var end = Math.Min(offset + 8 + length, raw.Length);
            text.Append(Encoding.UTF8.GetString(raw, offset + 8, end - offset - 8));
            offset = end;
        }

        return text.ToString();
    }
}
```

- [ ] **Step 5: Implement `Docker/IDockerEngine.cs` and `Docker/DockerEngine.cs`**

```csharp
using System.Text.Json.Nodes;

namespace PerfAgent.Docker;

public interface IDockerEngine
{
    Task<JsonObject> Inspect(string name, CancellationToken ct);
    Task Stop(string name, CancellationToken ct);
    Task Remove(string name, CancellationToken ct);
    Task Create(string name, JsonObject body, CancellationToken ct);
    Task Start(string name, CancellationToken ct);
    Task<string> Logs(string name, long sinceUnix, CancellationToken ct);
}
```

```csharp
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace PerfAgent.Docker;

/// <summary>Docker Engine API over the unix socket with plain HttpClient (no SDK, no CLI).</summary>
public sealed class DockerEngine : IDockerEngine, IDisposable
{
    private const string Api = "http://docker/v1.44";
    private readonly HttpClient _http;

    public DockerEngine(IOptions<AgentOptions> options)
    {
        var socket = options.Value.DockerSocket;
        _http = new HttpClient(new SocketsHttpHandler
        {
            ConnectCallback = async (_, ct) =>
            {
                var s = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await s.ConnectAsync(new UnixDomainSocketEndPoint(socket), ct);
                return new NetworkStream(s, ownsSocket: true);
            },
        }) { Timeout = TimeSpan.FromMinutes(2) };
    }

    public async Task<JsonObject> Inspect(string name, CancellationToken ct) =>
        (await Send(HttpMethod.Get, $"/containers/{name}/json", null, ct).Content.ReadFromJsonAsync<JsonObject>(ct))!;

    public async Task Stop(string name, CancellationToken ct) => await Send(HttpMethod.Post, $"/containers/{name}/stop?t=10", null, ct, allow304: true);

    public async Task Remove(string name, CancellationToken ct) => await Send(HttpMethod.Delete, $"/containers/{name}", null, ct);

    public async Task Create(string name, JsonObject body, CancellationToken ct) =>
        await Send(HttpMethod.Post, $"/containers/create?name={Uri.EscapeDataString(name)}", body, ct);

    public async Task Start(string name, CancellationToken ct) => await Send(HttpMethod.Post, $"/containers/{name}/start", null, ct, allow304: true);

    public async Task<string> Logs(string name, long sinceUnix, CancellationToken ct)
    {
        var response = await Send(HttpMethod.Get, $"/containers/{name}/logs?stdout=1&stderr=1&timestamps=1&since={sinceUnix}", null, ct);
        return DockerLogDemuxer.Demux(await response.Content.ReadAsByteArrayAsync(ct));
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, JsonObject? body, CancellationToken ct, bool allow304 = false)
    {
        using var request = new HttpRequestMessage(method, Api + path) { Content = body is null ? null : JsonContent.Create(body) };
        var response = await _http.SendAsync(request, ct);

        if (response.IsSuccessStatusCode || (allow304 && (int)response.StatusCode == 304))
            return response;

        throw new InvalidOperationException($"Docker {method} {path} → {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
    }

    public void Dispose() => _http.Dispose();
}
```

Note: `Inspect` returns the response body after `Send`; write it as `var r = await Send(...); return (await r.Content.ReadFromJsonAsync<JsonObject>(ct))!;` (the one-liner above does not compile because `Send` is async — use the two-line form).

- [ ] **Step 6: Implement `Database/IDatabaseAdmin.cs` and `Database/DatabaseAdmin.cs`**

```csharp
namespace PerfAgent.Database;

public interface IDatabaseAdmin
{
    /// <summary>Drops <paramref name="database"/> (forcing out connections) and creates it empty or from <paramref name="template"/>.</summary>
    Task Recreate(string database, string? template, CancellationToken ct);
}
```

```csharp
using Microsoft.Extensions.Options;
using Npgsql;

namespace PerfAgent.Database;

public sealed class DatabaseAdmin(IOptions<AgentOptions> options) : IDatabaseAdmin
{
    public async Task Recreate(string database, string? template, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(options.Value.PostgresAdmin);
        await connection.OpenAsync(ct);

        await Execute(connection, $"DROP DATABASE IF EXISTS {Quote(database)} WITH (FORCE)", ct);
        await Execute(connection, template is null
            ? $"CREATE DATABASE {Quote(database)}"
            : $"CREATE DATABASE {Quote(database)} TEMPLATE {Quote(template)}", ct);
    }

    private static async Task Execute(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 300 };
        await command.ExecuteNonQueryAsync(ct);
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
```

- [ ] **Step 7: Implement `Activation/ActivationService.cs`**

```csharp
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
        await WaitHealthy(rest, ct);

        var since = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await docker.Start(_options.SeedContainer, ct);
        var log = await WaitForSeed(since, ct);
        await docker.Stop(_options.SeedContainer, ct);
        await docker.Stop(rest, ct);

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

    private async Task Recreate(string name, IReadOnlyDictionary<string, string> env, CancellationToken ct)
    {
        var inspect = await docker.Inspect(name, ct);
        var body = ContainerSpec.BuildCreateBody(inspect, env);
        logger.LogInformation("Recreating {container} with {overrides}", name, string.Join(", ", env.Keys));
        await docker.Remove(name, ct);
        await docker.Create(name, body, ct);
        await docker.Start(name, ct);
    }

    private async Task WaitHealthy(string name, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(_options.HealthTimeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var health = (await docker.Inspect(name, ct))["State"]?["Health"]?["Status"]?.GetValue<string>();
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
```

- [ ] **Step 8: Run tests** → `dotnet test performance/tools/Perf.sln` → Expected: PASS. (`ActivationServiceTests` uses `Options.Create`; add `<PackageReference Include="Microsoft.Extensions.Options" />` to `Perf.UnitTests.csproj` if it does not flow from the PerfAgent reference.)

- [ ] **Step 9: Implement `BearerTokenMiddleware.cs` and `Program.cs`**

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace PerfAgent;

public sealed class BearerTokenMiddleware(RequestDelegate next, IOptions<AgentOptions> options)
{
    private readonly byte[] _expected = Encoding.UTF8.GetBytes("Bearer " + options.Value.Token);

    public async Task Invoke(HttpContext context)
    {
        var header = Encoding.UTF8.GetBytes(context.Request.Headers.Authorization.ToString());

        if (options.Value.Token.Length < 16 || !CryptographicOperations.FixedTimeEquals(header, _expected))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }
}
```

```csharp
using Perf.Core;
using Perf.Core.Agent;
using PerfAgent;
using PerfAgent.Activation;
using PerfAgent.Database;
using PerfAgent.Docker;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = PerfJson.Options.PropertyNamingPolicy;
    foreach (var converter in PerfJson.Options.Converters)
        o.SerializerOptions.Converters.Add(converter);
});
builder.Services.AddSingleton<IDockerEngine, DockerEngine>();
builder.Services.AddSingleton<IDatabaseAdmin, DatabaseAdmin>();
builder.Services.AddSingleton<ActivationService>();

var app = builder.Build();

app.UseMiddleware<BearerTokenMiddleware>();

app.MapGet("/status", (ActivationService s, CancellationToken ct) => s.Status(ct));
app.MapPost("/activate", (ActivateRequest request, ActivationService s, CancellationToken ct) => Guard(() => s.Activate(request, ct)));
app.MapPost("/stop", (ActivationService s, CancellationToken ct) => Guard(() => s.Stop(ct)));
app.MapPost("/prepare", (ActivationService s, CancellationToken ct) => Guard(async () => new { seedLog = await s.Prepare(ct) }));
app.MapGet("/logs/{transport}", async (Transport transport, long? sinceUnix, ActivationService s, CancellationToken ct) =>
    Results.Text(await s.Logs(transport, sinceUnix ?? 0, ct)));

app.Run();

static async Task<IResult> Guard<T>(Func<Task<T>> operation)
{
    try
    {
        return Results.Ok(await operation());
    }
    catch (AgentBusyException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
}
```

- [ ] **Step 10: Write `performance/tools/PerfAgent/Dockerfile`** (build context: repo root, so central packages resolve)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY Directory.Packages.props global.json ./
COPY performance/tools/Perf.Core performance/tools/Perf.Core
COPY performance/tools/PerfAgent performance/tools/PerfAgent
RUN dotnet publish performance/tools/PerfAgent/PerfAgent.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "PerfAgent.dll"]
```

- [ ] **Step 11: Add the agent to `performance/stack/compose.yml`** (under `services:`) and `AGENT_TOKEN=change-me-to-a-long-random-string` to `.env.template`

```yaml
  perf-agent:
    image: senswave-perf-agent
    build:
      context: ../..
      dockerfile: performance/tools/PerfAgent/Dockerfile
    container_name: Senswave.Perf.Agent
    restart: unless-stopped
    cpuset: ${SIDE_CPUSET:-0-1}
    cpus: 0.25
    mem_limit: 128m
    memswap_limit: 128m
    networks: [ perf ]
    ports:
      - ${AGENT_PORT:-9443}:9443
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - ${CERT_DIR:-../../docker/https}:/https:ro
    environment:
      ASPNETCORE_URLS: https://+:9443
      ASPNETCORE_Kestrel__Certificates__Default__Path: /https/senswave.api.pfx
      ASPNETCORE_Kestrel__Certificates__Default__Password: ${API_CERTIFICATE_PASSWORD}
      PERF_ENV: ${PERF_ENV:-local}
      Agent__Token: ${AGENT_TOKEN}
      Agent__PostgresAdmin: Host=${POSTGRES_HOST};Port=${POSTGRES_PORT:-5432};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};Database=postgres;Include Error Detail=true
```

- [ ] **Step 12: Verify against the local stack**

```bash
docker compose -f performance/local/compose.yml --env-file performance/stack/.env up -d --build perf-agent database mqtt
TOKEN=$(grep AGENT_TOKEN performance/stack/.env | cut -d= -f2)
curl -sk -H "Authorization: Bearer $TOKEN" https://localhost:9443/status
curl -sk -o /dev/null -w "%{http_code}\n" https://localhost:9443/status
```
Expected: first call returns JSON with three containers in state `created` or `exited`; second prints `401`. Set `AGENT_TOKEN` in `.env` to ≥ 16 characters first.

- [ ] **Step 13: Commit**

```bash
git add performance/tools performance/stack
git commit -m "feat: perf agent switching containers via docker engine api"
```

---

### Task 6: Prepare and feasibility checkpoint (local)

No new code unless a check fails. This task verifies the spec's open points on the real local stack and records findings.

**Files:**
- Create: `performance/NOTES-phase1.md` (findings; Task 13 folds it into the README and deletes it)
- Modify only if a check fails: the file named in that check

- [ ] **Step 1: Run prepare**

```bash
curl -sk -X POST -H "Authorization: Bearer $TOKEN" https://localhost:9443/prepare | head -c 2000
docker exec Senswave.Perf.Database psql -U postgres -c "\l" | grep senswave
```
Expected: `seedLog` ends with `Initial seed finished successfully.`; `senswave_seed` and `senswave_template` exist. If the API cannot reach Mosquitto with the seeded broker settings (look for broker errors in `curl .../logs/Rest`), record the error and try `SEED_BROKER_PORT=1883` with the API's TLS option for brokers (read `src/Modules/DataSource/Senswave.DataSources.BrokerConnection` for how TLS is chosen); document the outcome.

- [ ] **Step 2: Activate and check OTel resource attributes and DB reset**

```bash
curl -sk -X POST -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"transport":"Grpc","otel":true,"runId":"feasibility-1"}' https://localhost:9443/activate
docker exec Senswave.Perf.Grpc printenv OTEL_RESOURCE_ATTRIBUTES PostgreSql__Database Diagnostics__OpenTelemetry__Enabled
```
Expected: status shows `Grpc` active and healthy; env shows `run_id=feasibility-1,...`, `senswave_run`, `true`. Whether the SDK picks the attribute up is confirmed in Task 11 Step 6 (the `run_id` label appears in Grafana Cloud).

- [ ] **Step 3: Check S5 works without the DataSource worker**

Find the seeded Pico device id and a token for `user1@gmail.com` (REST mode: activate `Rest`), then:
```bash
curl -sk https://localhost:8443/api/v1/auth/login -H "Content-Type: application/json" -d '{"email":"user1@gmail.com","password":"User123456!"}'
curl -sk -H "Authorization: Bearer <token>" "https://localhost:8443/api/v1/homes/current"
curl -sk -H "Authorization: Bearer <token>" "https://localhost:8443/api/v1/devices/display?homeId=<id>&page=1&size=30"
curl -sk -X POST -H "Authorization: Bearer <token>" -H "Content-Type: application/json" -d '{"value":true}' "https://localhost:8443/api/v1/devices/<picoId>/tile/action"
docker exec Senswave.Perf.Mqtt mosquitto_sub -t 'perf/#' -C 1 -W 10 -v
```
(Run `mosquitto_sub` in a second terminal before the POST.) Expected: POST returns 200 and a message appears on `perf/controller/...`. Record the exact login response field names and the shape of the devices and dashboards list responses (the field holding the items; the Pico device is named `Pico Controller`) — Task 7 needs them. Generated users are `user1@gmail.com` … `user100@gmail.com` (`GeneratedUsersOptions.Expand` starts at 1).

- [ ] **Step 4: Check WebSockets over HTTP/2 for SignalR**

Defer to Task 10 Step 6 (PerfStream verify mode); record the result there.

- [ ] **Step 5: Write `performance/NOTES-phase1.md` and commit**

Record for each check: command, outcome, and any fix applied. Commit:
```bash
git add performance/NOTES-phase1.md
git commit -m "docs: phase 1 feasibility findings"
```

---

### Task 7: Perf.Client (login, discovery, tile action, subscriptions)

**Files:**
- Create: `performance/tools/Perf.Client/Perf.Client.csproj`, `ApiTarget.cs`, `IApiClient.cs`, `ApiClientFactory.cs`, `Http/HttpHandlers.cs`, `Rest/RestApiClient.cs`, `Grpc/GrpcApiClient.cs`

**Interfaces:**
- Consumes: `Transport`, `UserData` (Task 3); REST shapes recorded in `performance/NOTES-phase1.md` (Task 6).
- Produces (namespace `Perf.Client`):

```csharp
public sealed record ApiTarget(Uri BaseUri, bool InsecureTls);

public interface ILiveUpdateSubscription : IAsyncDisposable { }

public interface IApiClient : IAsyncDisposable
{
    Task<string> Login(string email, string password, CancellationToken ct);
    /// <summary>Current home, the seeded Pico controller device and its first dashboard.</summary>
    Task<UserData> Discover(string email, string accessToken, CancellationToken ct);
    Task TileAction(string accessToken, string deviceId, bool value, CancellationToken ct);
    /// <summary>Completes once the server confirmed the subscription (SignalR Initialized / gRPC Initialized message).
    /// <paramref name="onDeviceUpdate"/> gets the device id of every tile or widget update.</summary>
    Task<ILiveUpdateSubscription> Subscribe(string accessToken, string homeId, Action<string> onDeviceUpdate, CancellationToken ct);
}

public static class ApiClientFactory
{
    public static IApiClient Create(Transport transport, ApiTarget target);
}
```

- [ ] **Step 1: Create the project**

```bash
cd performance/tools
dotnet new classlib -n Perf.Client -f net10.0 && rm Perf.Client/Class1.cs
dotnet sln Perf.sln add Perf.Client
dotnet add Perf.Client reference Perf.Core ../../src/Shared/Senswave.Contracts/Senswave.Contracts.csproj
```
Add package references (central versions): `Grpc.Net.Client`, `Grpc.Net.Client.Web`, `Google.Protobuf`, `Microsoft.AspNetCore.SignalR.Client`.

- [ ] **Step 2: Write `Http/HttpHandlers.cs`**

```csharp
using System.Net;
using System.Net.Security;

namespace Perf.Client.Http;

internal static class HttpHandlers
{
    public static SocketsHttpHandler Create(ApiTarget target) => new()
    {
        EnableMultipleHttp2Connections = true,
        PooledConnectionLifetime = Timeout.InfiniteTimeSpan,
        SslOptions = target.InsecureTls
            ? new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true }
            : new SslClientAuthenticationOptions(),
    };

    /// <summary>REST and SignalR negotiate are forced to HTTP/2 (Spec E: every transport over HTTP/2).</summary>
    public static HttpClient Http2Client(ApiTarget target) => new(Create(target))
    {
        BaseAddress = target.BaseUri,
        DefaultRequestVersion = HttpVersion.Version20,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
    };
}
```

- [ ] **Step 3: Write `Rest/RestApiClient.cs`**

Use the field names recorded in `NOTES-phase1.md` Step 3. The code below assumes Identity's `{ "accessToken": ... }`, an `IdResponse` `{ "id": ... }` for the current home, and list responses with an `items` array whose elements have `id` and `name`; adjust the JSON paths to what was recorded. The Pico device is the device named `Pico Controller` (`DeviceSeedingService.SeedPicoController`).

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Perf.Client.Http;
using Perf.Core.Runs;

namespace Perf.Client.Rest;

internal sealed class RestApiClient(ApiTarget target) : IApiClient
{
    internal const string PicoDeviceName = "Pico Controller";
    private readonly HttpClient _http = HttpHandlers.Http2Client(target);

    public async Task<string> Login(string email, string password, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("api/v1/auth/login", new { email, password }, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonObject>(ct))!["accessToken"]!.GetValue<string>();
    }

    public async Task<UserData> Discover(string email, string accessToken, CancellationToken ct)
    {
        var homeId = (await Get($"api/v1/homes/current", accessToken, ct))["id"]!.GetValue<string>();
        var devices = await Get($"api/v1/devices/display?homeId={homeId}&page=1&size=30", accessToken, ct);
        var deviceId = devices["items"]!.AsArray().First(d => d!["name"]!.GetValue<string>() == PicoDeviceName)!["id"]!.GetValue<string>();
        var dashboards = await Get($"api/v1/devices/dashboards/display?deviceId={deviceId}", accessToken, ct);
        var dashboardId = dashboards["items"]!.AsArray().First()!["id"]!.GetValue<string>();

        return new UserData(email, accessToken, homeId, deviceId, dashboardId);
    }

    public async Task TileAction(string accessToken, string deviceId, bool value, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v1/devices/{deviceId}/tile/action") { Content = JsonContent.Create(new { value }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        (await _http.SendAsync(request, ct)).EnsureSuccessStatusCode();
    }

    public async Task<ILiveUpdateSubscription> Subscribe(string accessToken, string homeId, Action<string> onDeviceUpdate, CancellationToken ct)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(target.BaseUri, "signalr/liveupdates/live"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.HttpMessageHandlerFactory = _ => HttpHandlers.Create(target);
                options.WebSocketConfiguration = ws =>
                {
                    ws.HttpVersion = System.Net.HttpVersion.Version20;
                    ws.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
                    if (target.InsecureTls)
                        ws.RemoteCertificateValidationCallback = (_, _, _, _) => true;
                };
            })
            .Build();

        var initialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("Initialized", () => initialized.TrySetResult());
        connection.On<string>("FailedToInitialize", reason => initialized.TrySetException(new InvalidOperationException(reason)));
        connection.On<string, JsonNode>("Update", (_, data) =>
        {
            if (data?["deviceId"]?.GetValue<string>() is { } deviceId)
                onDeviceUpdate(deviceId);
        });

        await connection.StartAsync(ct);
        await connection.InvokeAsync("Initialize", homeId, ct);
        await initialized.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);

        return new Subscription(connection);
    }

    private async Task<JsonObject> Get(string path, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonObject>(ct))!;
    }

    public ValueTask DisposeAsync()
    {
        _http.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class Subscription(HubConnection connection) : ILiveUpdateSubscription
    {
        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
```

Read `src/Modules/LiveUpdate/Senswave.LiveUpdates.Api/Hubs/ILiveUpdatesHub.cs` and the code that calls `Update(...)` to confirm the JSON key that carries the device id (`deviceId` assumed) and adjust.

- [ ] **Step 4: Write `Grpc/GrpcApiClient.cs`**

```csharp
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Perf.Client.Http;
using Perf.Core.Runs;
using Senswave.Contracts.Devices.V1;
using Senswave.Contracts.Homes.V1;
using Senswave.Contracts.LiveUpdates.V1;
using Senswave.Contracts.Users.V1;

namespace Perf.Client.Grpc;

/// <summary>Native gRPC or gRPC-Web (binary <c>application/grpc-web+proto</c>), both over HTTP/2.</summary>
internal sealed class GrpcApiClient : IApiClient
{
    private readonly GrpcChannel _channel;

    public GrpcApiClient(ApiTarget target, bool web)
    {
        HttpMessageHandler handler = HttpHandlers.Create(target);
        if (web)
            handler = new GrpcWebHandler(GrpcWebMode.GrpcWeb, handler) { HttpVersion = System.Net.HttpVersion.Version20 };

        _channel = GrpcChannel.ForAddress(target.BaseUri, new GrpcChannelOptions { HttpHandler = handler, MaxReceiveMessageSize = 16 * 1024 * 1024 });
    }

    private static Metadata Auth(string token) => new() { { "Authorization", $"Bearer {token}" } };

    public async Task<string> Login(string email, string password, CancellationToken ct) =>
        (await new AuthService.AuthServiceClient(_channel).LoginAsync(new LoginRequest { Email = email, Password = password }, cancellationToken: ct)).AccessToken;

    public async Task<UserData> Discover(string email, string accessToken, CancellationToken ct)
    {
        var homeId = (await new HomesService.HomesServiceClient(_channel).GetCurrentHomeAsync(new GetCurrentHomeRequest(), Auth(accessToken), cancellationToken: ct)).Id;
        var devices = await new DevicesService.DevicesServiceClient(_channel).DisplayDevicesAsync(
            new DisplayDevicesRequest { HomeId = homeId, Page = 1, Size = 30 }, Auth(accessToken), cancellationToken: ct);
        var deviceId = devices.Items.First(d => d.Name == Rest.RestApiClient.PicoDeviceName).Id;
        var dashboards = await new DashboardsService.DashboardsServiceClient(_channel).DisplayDashboardsAsync(
            new DisplayDashboardsRequest { DeviceId = deviceId }, Auth(accessToken), cancellationToken: ct);

        return new UserData(email, accessToken, homeId, deviceId, dashboards.Items.First().Id);
    }

    public async Task TileAction(string accessToken, string deviceId, bool value, CancellationToken ct) =>
        await new DevicesService.DevicesServiceClient(_channel).DeviceTileActionAsync(
            new DeviceTileActionRequest { DeviceId = deviceId, Value = Value.ForBool(value) }, Auth(accessToken), cancellationToken: ct);

    public async Task<ILiveUpdateSubscription> Subscribe(string accessToken, string homeId, Action<string> onDeviceUpdate, CancellationToken ct)
    {
        var lifetime = new CancellationTokenSource();
        var call = new LiveUpdatesService.LiveUpdatesServiceClient(_channel)
            .Subscribe(new SubscribeRequest { HomeReferenceId = homeId }, Auth(accessToken), cancellationToken: lifetime.Token);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        if (!await call.ResponseStream.MoveNext(timeout.Token) || call.ResponseStream.Current.UpdateCase != LiveUpdate.UpdateOneofCase.Initialized)
            throw new InvalidOperationException("Live-update stream did not start with Initialized.");

        var reader = Task.Run(async () =>
        {
            try
            {
                while (await call.ResponseStream.MoveNext(lifetime.Token))
                {
                    var update = call.ResponseStream.Current;
                    var deviceId = update.UpdateCase switch
                    {
                        LiveUpdate.UpdateOneofCase.DeviceTile => update.DeviceTile.DeviceId,
                        LiveUpdate.UpdateOneofCase.Widgets => update.Widgets.DeviceId,
                        _ => null,
                    };
                    if (deviceId is not null)
                        onDeviceUpdate(deviceId);
                }
            }
            catch (RpcException) when (lifetime.IsCancellationRequested) { }
            catch (OperationCanceledException) { }
        });

        return new Subscription(call, lifetime, reader);
    }

    public ValueTask DisposeAsync()
    {
        _channel.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class Subscription(AsyncServerStreamingCall<LiveUpdate> call, CancellationTokenSource lifetime, Task reader) : ILiveUpdateSubscription
    {
        public async ValueTask DisposeAsync()
        {
            await lifetime.CancelAsync();
            await reader;
            call.Dispose();
            lifetime.Dispose();
        }
    }
}
```

Message and field names come from the protos (`Senswave.Contracts`): check `homes.proto` (`GetCurrentHomeRequest`), `devices.proto` (`DisplayDevicesResponse.items`, `DisplayDeviceDto.name`), `dashboards.proto` (`DisplayDashboardsRequest`, response list field), `auth.proto` (`AccessTokenResponse.access_token`) and fix any name that differs.

- [ ] **Step 5: Write `ApiTarget.cs`, `IApiClient.cs`, `ApiClientFactory.cs`** exactly as in the Interfaces block, with:

```csharp
public static IApiClient Create(Transport transport, ApiTarget target) => transport switch
{
    Transport.Rest => new Rest.RestApiClient(target),
    Transport.Grpc => new Grpc.GrpcApiClient(target, web: false),
    Transport.GrpcWeb => new Grpc.GrpcApiClient(target, web: true),
    _ => throw new ArgumentOutOfRangeException(nameof(transport)),
};
```

- [ ] **Step 6: Build** → `dotnet build performance/tools/Perf.sln` → Expected: Build succeeded. (Behaviour is verified end-to-end in Tasks 8 and 10 against the local stack.)

- [ ] **Step 7: Commit**

```bash
git add performance/tools
git commit -m "feat: perf client for rest, grpc and grpc-web"
```

---

### Task 8: PerfRunner (run, prepare, verification, gates)

**Files:**
- Create: `performance/tools/PerfRunner/PerfRunner.csproj`, `Program.cs`, `RunnerSettings.cs`, `AgentClient.cs`, `Discovery.cs`, `CpuSampler.cs`, `Tools/K6Tool.cs`, `Tools/StreamTool.cs`, `CampaignRunner.cs`, `ResultsWriter.cs`
- Create: `performance/runner/campaigns/smoke.json`, `performance/runner/campaigns/full.json`

**Interfaces:**
- Consumes: `CampaignLoader`, `CampaignPlanner`, `RunSpec`, `RunMeta`, `Gates`, `K6Summary`, `ProcStat`, `RunData`, `AgentStatus`, `ActivateRequest`, `PerfJson` (Tasks 3–4); `ApiClientFactory`, `ApiTarget` (Task 7).
- Produces:
  - CLI: `perfrunner run <campaign.json>`, `perfrunner prepare <campaign.json>`, `perfrunner export <resultsDir>` (Task 11), `perfrunner archive <resultsDir>` (Task 11).
  - Results: `<RESULTS_DIR>/<campaignFileName>-<yyyyMMdd-HHmm>/campaign.json` (input + seed), `<runId>-a<attempt>/meta.json`, `run-data.json`, `verify.json`, `k6-summary.json` or `stream-summary.json`, `agent-logs.txt` (failures only).
  - Tool contract (Task 9 k6 scripts, Task 10 PerfStream): environment variables `TRANSPORT` (`rest|grpc|grpcweb`), `BASE_URL`, `MODE` (`verify|run`), `RATE`, `DURATION` (k6 duration string, e.g. `300s`), `DATA_FILE` (absolute path to `run-data.json`), `INSECURE` (`true|false`), `RUN_ID`; k6 also gets `--summary-export`.
  - `interface IRunTool { Task<RunToolResult> Verify(RunContext c, CancellationToken ct); Task<RunToolResult> Run(RunContext c, int seconds, bool measured, CancellationToken ct); }`, `record RunContext(RunSpec Run, Campaign Campaign, string RunDir, string DataFile)`, `record RunToolResult(bool Success, long Dropped, double ErrorRate, string Output)`.

- [ ] **Step 1: Create the project and campaign files**

```bash
cd performance/tools
dotnet new console -n PerfRunner -f net10.0
dotnet sln Perf.sln add PerfRunner
dotnet add PerfRunner reference Perf.Core Perf.Client
```

`performance/runner/campaigns/smoke.json`:
```json
{
  "target": { "api": "https://host.docker.internal:8443", "agent": "https://host.docker.internal:9443", "insecureTls": true },
  "rounds": 1,
  "transports": ["Rest", "Grpc", "GrpcWeb"],
  "scenarios": ["S1", "S2", "S3", "S4", "S5", "S6a", "S6b"],
  "rates": [100],
  "otel": [true],
  "warmupSeconds": 10,
  "durationSeconds": 30,
  "users": { "emailPattern": "user{0}@gmail.com", "firstIndex": 1, "count": 100, "password": "User123456!" },
  "export": { "traces": false, "rawK6": false }
}
```
`firstIndex` is 1 (`GeneratedUsersOptions.Expand` starts at 1). `full.json` is identical except `"rounds": 3`, `"rates": [100, 500, 1000]`, `"warmupSeconds": 30`, `"durationSeconds": 300`, `"export": { "traces": true, "rawK6": false }`, and target URLs `https://perf.example.com:8443` / `:9443` with `"insecureTls": false`.

- [ ] **Step 2: Write `RunnerSettings.cs`** (all from environment variables; documented in `runner/.env.template`, Task 12)

```csharp
namespace PerfRunner;

public sealed record RunnerSettings(
    string AgentToken, string ResultsDir, string K6Dir, string K6Binary, string StreamBinary,
    string MqttHost, int MqttPort, string? MqttUser, string? MqttPassword, bool MqttTls,
    string? PromUrl, string? PromUser, string? GrafanaReadToken, string? TempoUrl, string? TempoUser, string? ArchiveSasUrl)
{
    public static RunnerSettings FromEnvironment() => new(
        Required("AGENT_TOKEN"),
        Env("RESULTS_DIR") ?? "/work/performance/results",
        Env("K6_DIR") ?? "/work/performance/k6",
        Env("K6_BINARY") ?? "k6",
        Env("STREAM_BINARY") ?? "/app/stream/PerfStream",
        Env("MQTT_HOST") ?? "host.docker.internal",
        int.Parse(Env("MQTT_PORT") ?? "1883"),
        Env("MQTT_USERNAME"), Env("MQTT_PASSWORD"), Env("MQTT_TLS") == "true",
        Env("GRAFANA_PROM_URL"), Env("GRAFANA_PROM_USER"), Env("GRAFANA_READ_TOKEN"),
        Env("GRAFANA_TEMPO_URL"), Env("GRAFANA_TEMPO_USER"), Env("ARCHIVE_SAS_URL"));

    private static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;
    private static string Required(string name) => Env(name) ?? throw new InvalidOperationException($"Environment variable {name} is required.");
}
```

- [ ] **Step 3: Write `AgentClient.cs`**

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Perf.Core;
using Perf.Core.Agent;
using Perf.Core.Campaigns;

namespace PerfRunner;

public sealed class AgentClient : IDisposable
{
    private readonly HttpClient _http;

    public AgentClient(CampaignTarget target, string token)
    {
        var handler = new SocketsHttpHandler();
        if (target.InsecureTls)
            handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        _http = new HttpClient(handler) { BaseAddress = target.Agent, Timeout = TimeSpan.FromMinutes(40) };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public Task<AgentStatus> Status(CancellationToken ct) => Read<AgentStatus>(_http.GetAsync("status", ct), ct);
    public Task<AgentStatus> Activate(ActivateRequest request, CancellationToken ct) => Read<AgentStatus>(_http.PostAsJsonAsync("activate", request, PerfJson.Options, ct), ct);
    public Task<AgentStatus> Stop(CancellationToken ct) => Read<AgentStatus>(_http.PostAsync("stop", null, ct), ct);
    public async Task<string> Prepare(CancellationToken ct) => (await Read<PrepareResult>(_http.PostAsync("prepare", null, ct), ct)).SeedLog;
    public async Task<string> Logs(Transport transport, DateTimeOffset since, CancellationToken ct) =>
        await _http.GetStringAsync($"logs/{transport}?sinceUnix={since.ToUnixTimeSeconds()}", ct);

    private static async Task<T> Read<T>(Task<HttpResponseMessage> call, CancellationToken ct)
    {
        var response = await call;
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Agent {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(PerfJson.Options, ct))!;
    }

    private sealed record PrepareResult(string SeedLog);

    public void Dispose() => _http.Dispose();
}
```

- [ ] **Step 4: Write `Discovery.cs`, `CpuSampler.cs`, `ResultsWriter.cs`**

```csharp
using Perf.Client;
using Perf.Core;
using Perf.Core.Campaigns;
using Perf.Core.Runs;

namespace PerfRunner;

/// <summary>Logs in every seeded user over the transport under test and collects the ids the scenarios need.</summary>
public static class Discovery
{
    public static async Task<RunData> Run(Transport transport, Campaign campaign, CancellationToken ct)
    {
        await using var client = ApiClientFactory.Create(transport, new ApiTarget(campaign.Target.Api, campaign.Target.InsecureTls));
        var users = new UserData[campaign.Users.Count];

        await Parallel.ForEachAsync(Enumerable.Range(0, campaign.Users.Count), new ParallelOptions { MaxDegreeOfParallelism = 10, CancellationToken = ct },
            async (i, token) =>
            {
                var email = campaign.Users.Email(i);
                var accessToken = await client.Login(email, campaign.Users.Password, token);
                users[i] = await client.Discover(email, accessToken, token);
            });

        return new RunData(users);
    }
}
```

```csharp
using Perf.Core.Runs;

namespace PerfRunner;

/// <summary>Average busy fraction of the load machine's CPUs (/proc/stat), sampled every 5 s.</summary>
public sealed class CpuSampler : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly List<double> _samples = new();
    private readonly Task _loop;

    public CpuSampler() => _loop = Loop();

    public double Average => _samples.Count == 0 ? 0 : _samples.Average();

    private async Task Loop()
    {
        if (!File.Exists("/proc/stat"))
            return;
        var previous = Read();
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), _stop.Token);
                var current = Read();
                _samples.Add(ProcStat.Utilisation(previous, current));
                previous = current;
            }
        }
        catch (OperationCanceledException) { }
    }

    private static CpuTimes Read() => ProcStat.Parse(File.ReadLines("/proc/stat").First());

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _loop;
    }
}
```

```csharp
using System.Text.Json;
using Perf.Core;

namespace PerfRunner;

public static class ResultsWriter
{
    public static async Task Json<T>(string path, T value, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, PerfJson.Options), ct);
    }
}
```

- [ ] **Step 5: Write `Tools/K6Tool.cs` and `Tools/StreamTool.cs`**

```csharp
using System.Diagnostics;
using Perf.Core;
using Perf.Core.Campaigns;
using Perf.Core.Runs;

namespace PerfRunner.Tools;

public sealed record RunContext(RunSpec Run, Campaign Campaign, string RunDir, string DataFile);
public sealed record RunToolResult(bool Success, long Dropped, double ErrorRate, string Output);

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
        foreach (var (key, value) in Environment(c, mode, seconds))
            start.Environment[key] = value;

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var output = await stdout + await stderr;

        if (!File.Exists(summary))
            return new RunToolResult(false, 0, 1, output);

        var (dropped, errorRate) = K6Summary.Read(await File.ReadAllTextAsync(summary, ct));
        return new RunToolResult(process.ExitCode == 0 || mode == "run", dropped, errorRate, output);
    }

    internal static IEnumerable<(string, string)> Environment(RunContext c, string mode, int seconds) =>
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
```

`StreamTool` runs `settings.StreamBinary` with the same environment variables plus `SCENARIO` (`S6a|S6b`), `WARMUP` (seconds, only for `run`), `OUT` (summary path), `MQTT_HOST`, `MQTT_PORT`, `MQTT_USERNAME`, `MQTT_PASSWORD`, `MQTT_TLS`. Because PerfStream handles warm-up and measurement in one process (subscriptions must stay open), `Run(c, seconds, measured: false, ...)` returns a success result immediately without running anything, and `Run(..., measured: true)` passes `WARMUP={c.Campaign.WarmupSeconds}` and `DURATION={seconds}s`. It reads `stream-summary.json` (shape in Task 10) into `RunToolResult(Success: exit code 0, Dropped: summary.publishBehind, ErrorRate: (lost + publishFailures) / max(published, 1))`.

```csharp
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
        foreach (var (key, value) in K6Tool.Environment(c, mode, seconds))
            start.Environment[key] = value;
        start.Environment["SCENARIO"] = c.Run.Scenario.ToString();
        start.Environment["WARMUP"] = warmup.ToString();
        start.Environment["OUT"] = summary;
        start.Environment["MQTT_HOST"] = settings.MqttHost;
        start.Environment["MQTT_PORT"] = settings.MqttPort.ToString();
        start.Environment["MQTT_USERNAME"] = settings.MqttUser ?? "";
        start.Environment["MQTT_PASSWORD"] = settings.MqttPassword ?? "";
        start.Environment["MQTT_TLS"] = settings.MqttTls ? "true" : "false";

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var output = await stdout + await stderr;

        if (!File.Exists(summary))
            return new RunToolResult(false, 0, 1, output);

        var s = JsonNode.Parse(await File.ReadAllTextAsync(summary, ct))!;
        var published = Math.Max(s["published"]!.GetValue<long>(), 1);
        var errors = s["lost"]!.GetValue<long>() + s["publishFailures"]!.GetValue<long>();
        return new RunToolResult(process.ExitCode == 0, s["publishBehind"]!.GetValue<long>(), (double)errors / published, output);
    }
}
```

- [ ] **Step 6: Write `CampaignRunner.cs`**

```csharp
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
        var tool = run.Scenario.IsStream() ? stream : k6;
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
            await ResultsWriter.Json(dataFile, await Discovery.Run(run.Transport, campaign, ct), ct);
            var context = new RunContext(run, campaign, runDir, dataFile);

            var verify = await tool.Verify(context, ct);
            if (!verify.Success || verify.ErrorRate > 0)
                return await Finish(runDir, Meta("failed-verification", ["verification failed", Trim(verify.Output)]), run, started, ct);

            warmupStart = DateTimeOffset.UtcNow;
            await tool.Run(context, campaign.WarmupSeconds, measured: false, ct);

            await using var cpu = new CpuSampler();
            measureStart = run.Scenario.IsStream() ? warmupStart.Value.AddSeconds(campaign.WarmupSeconds) : DateTimeOffset.UtcNow;
            var result = await tool.Run(context, campaign.DurationSeconds, measured: true, ct);
            measureEnd = DateTimeOffset.UtcNow;

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
```

- [ ] **Step 7: Write `Program.cs`**

```csharp
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
```

(Task 11 adds the `export` and `archive` cases.)

- [ ] **Step 8: Build and run unit tests** → `dotnet build performance/tools/Perf.sln && dotnet test performance/tools/Perf.sln` → Expected: Build succeeded, tests PASS. End-to-end runs are verified in Task 14.

- [ ] **Step 9: Commit**

```bash
git add performance/tools performance/runner/campaigns
git commit -m "feat: perf runner with verification and validity gates"
```

---

### Task 9: k6 scenarios S1–S5

**Files:**
- Create: `performance/k6/lib/config.js`, `lib/data.js`, `lib/rest.js`, `lib/grpc.js`, `lib/grpcweb.js`, `lib/protobuf.js`, `lib/scenario.js`
- Create: `performance/k6/s1.js`, `s2.js`, `s3.js`, `s4.js`, `s5.js`
- Delete: `performance/k6/legal-terms.js`

**Interfaces:**
- Consumes: the tool contract env vars from Task 8; `run-data.json` shape `{ "users": [ { "email", "accessToken", "homeId", "deviceId", "dashboardId" } ] }`.
- Produces: scripts runnable as `k6 run --summary-export x.json sN.js`; checks named consistently; protos loaded from `../../src/Shared/Senswave.Contracts/Protos` relative to `performance/k6` (the runner image keeps that layout).

- [ ] **Step 1: Write `lib/config.js`**

```javascript
// Shared options for every scenario. The runner passes everything as env vars (see performance/README.md).
export const TRANSPORT = (__ENV.TRANSPORT || 'rest').toLowerCase();
export const BASE_URL = (__ENV.BASE_URL || 'https://localhost:8443').replace(/\/+$/, '');
export const MODE = (__ENV.MODE || 'run').toLowerCase();
export const RATE = parseInt(__ENV.RATE || '100', 10);
export const DURATION = __ENV.DURATION || '30s';
export const DATA_FILE = __ENV.DATA_FILE || '../results/run-data.json';
export const INSECURE = (__ENV.INSECURE || 'false') === 'true';

if (!['rest', 'grpc', 'grpcweb'].includes(TRANSPORT)) {
    throw new Error(`Unknown TRANSPORT '${TRANSPORT}', expected rest | grpc | grpcweb`);
}

// Open model: a constant arrival rate, so every transport gets exactly the same load.
export function options() {
    const base = {
        insecureSkipTLSVerify: INSECURE,
        summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
        discardResponseBodies: false,
    };

    if (MODE === 'verify') {
        return { ...base, scenarios: { verify: { executor: 'per-vu-iterations', vus: 1, iterations: 1 } }, thresholds: { checks: ['rate==1'] } };
    }

    return {
        ...base,
        scenarios: {
            load: {
                executor: 'constant-arrival-rate',
                rate: RATE,
                timeUnit: '1s',
                duration: DURATION,
                preAllocatedVUs: Math.max(20, Math.ceil(RATE * 0.2)),
                maxVUs: Math.max(100, RATE * 2),
            },
        },
    };
}
```

- [ ] **Step 2: Write `lib/data.js`**

```javascript
import { SharedArray } from 'k6/data';
import exec from 'k6/execution';
import { DATA_FILE } from './config.js';

const users = new SharedArray('users', () => JSON.parse(open(DATA_FILE)).users);

// Round-robin over the seeded users, one user per iteration.
export function nextUser() {
    return users[exec.scenario.iterationInTest % users.length];
}
```

- [ ] **Step 3: Write `lib/protobuf.js` and `lib/grpcweb.js`** (k6 has no gRPC-Web client; requests are encoded and framed by hand)

```javascript
// Minimal protobuf encoder for request messages: string fields, int32 fields and google.protobuf.Value(bool).
function varint(value) {
    const out = [];
    let v = value >>> 0;
    while (v > 0x7f) { out.push((v & 0x7f) | 0x80); v >>>= 7; }
    out.push(v);
    return out;
}

function utf8(text) {
    const out = [];
    for (const ch of unescape(encodeURIComponent(text))) out.push(ch.charCodeAt(0));
    return out;
}

export function stringField(number, text) {
    const bytes = utf8(text);
    return [...varint((number << 3) | 2), ...varint(bytes.length), ...bytes];
}

export function int32Field(number, value) {
    return [...varint((number << 3) | 0), ...varint(value)];
}

export function messageField(number, bytes) {
    return [...varint((number << 3) | 2), ...varint(bytes.length), ...bytes];
}

// google.protobuf.Value { bool bool_value = 4; }
export function boolValue(value) {
    return [...varint((4 << 3) | 0), value ? 1 : 0];
}
```

```javascript
import http from 'k6/http';
import { fail } from 'k6';

// One data frame: flag 0x00 + 4-byte big-endian length + message.
export function frame(messageBytes) {
    const n = messageBytes.length;
    return new Uint8Array([0x00, (n >>> 24) & 0xff, (n >>> 16) & 0xff, (n >>> 8) & 0xff, n & 0xff, ...messageBytes]).buffer;
}

export function call(baseUrl, path, messageBytes, token) {
    const headers = { 'content-type': 'application/grpc-web+proto', 'x-grpc-web': '1' };
    if (token) headers.authorization = `Bearer ${token}`;
    const res = http.post(`${baseUrl}/${path}`, frame(messageBytes), { headers, responseType: 'binary' });
    const frames = res.status === 200 ? parseFrames(res.body) : { data: 0, status: null };
    return { res, data: frames.data, grpcStatus: frames.status ?? res.headers['Grpc-Status'] ?? null };
}

// Returns the total data-frame payload size and grpc-status from the trailer frame (flag 0x80).
export function parseFrames(body) {
    if (!body) return { data: 0, status: null };
    const bytes = new Uint8Array(body);
    let offset = 0, data = 0, status = null;
    while (offset + 5 <= bytes.length) {
        const flag = bytes[offset];
        const length = ((bytes[offset + 1] << 24) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 8) | bytes[offset + 4]) >>> 0;
        const start = offset + 5, end = start + length;
        if (end > bytes.length) fail(`Truncated gRPC-Web frame: needs ${end} bytes, got ${bytes.length}`);
        if ((flag & 0x80) === 0x80) {
            const match = /grpc-status:\s*(\d+)/i.exec(String.fromCharCode.apply(null, bytes.subarray(start, end)));
            status = match ? match[1] : null;
        } else {
            data += length;
        }
        offset = end;
    }
    return { data, status };
}
```

- [ ] **Step 4: Write `lib/grpc.js`, `lib/rest.js`, `lib/scenario.js`**

```javascript
import grpc from 'k6/net/grpc';
import { BASE_URL, TRANSPORT } from './config.js';

const client = new grpc.Client();
if (TRANSPORT === 'grpc') {
    client.load(['../../src/Shared/Senswave.Contracts/Protos'],
        'users/v1/users.proto', 'users/v1/legal.proto', 'devices/v1/operations.proto', 'devices/v1/dashboards.proto', 'devices/v1/devices.proto');
}

let connected = false;

// One HTTP/2 connection per VU, reused across iterations (same as REST and gRPC-Web connection reuse in k6).
export function invoke(method, message, token) {
    if (!connected) {
        client.connect(BASE_URL.replace(/^https?:\/\//, ''), { plaintext: false });
        connected = true;
    }
    const metadata = token ? { authorization: `Bearer ${token}` } : {};
    return client.invoke(method, message, { metadata });
}

export const StatusOK = grpc.StatusOK;
```

```javascript
import http from 'k6/http';
import { BASE_URL } from './config.js';

export function get(path, token) {
    return http.get(`${BASE_URL}/${path}`, { headers: token ? { authorization: `Bearer ${token}` } : {} });
}

export function post(path, body, token) {
    return http.post(`${BASE_URL}/${path}`, JSON.stringify(body), {
        headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) },
    });
}
```

```javascript
import { check } from 'k6';
import { BASE_URL, TRANSPORT } from './config.js';
import * as rest from './rest.js';
import * as grpcClient from './grpc.js';
import * as grpcWeb from './grpcweb.js';

// Runs one operation over the selected transport with the same two checks everywhere:
// "ok" (success status) and "has body" (non-empty response).
export function run(op) {
    switch (TRANSPORT) {
        case 'rest': {
            const res = op.rest();
            return check(res, { ok: (r) => r.status === 200, 'has body': (r) => !!r.body && r.body.length > 0 });
        }
        case 'grpc': {
            const res = grpcClient.invoke(op.grpc.method, op.grpc.message, op.token);
            return check(res, { ok: (r) => r && r.status === grpcClient.StatusOK, 'has body': (r) => r && r.message !== null });
        }
        case 'grpcweb': {
            const r = grpcWeb.call(BASE_URL, op.grpcWeb.path, op.grpcWeb.bytes, op.token);
            return check(r, { ok: (x) => x.res.status === 200 && x.grpcStatus === '0', 'has body': (x) => x.data > 0 });
        }
    }
}
```

- [ ] **Step 5: Write the scenario scripts**

`s1.js`:
```javascript
// S1 Flat data: the signed-in user's profile.
import { options as build } from './lib/config.js';
import { nextUser } from './lib/data.js';
import { run } from './lib/scenario.js';
import * as rest from './lib/rest.js';

export const options = build();

export default function () {
    const user = nextUser();
    run({
        token: user.accessToken,
        rest: () => rest.get('api/v1/users', user.accessToken),
        grpc: { method: 'senswave.users.v1.UsersService/GetUser', message: {} },
        grpcWeb: { path: 'senswave.users.v1.UsersService/GetUser', bytes: [] },
    });
}
```

`s2.js`:
```javascript
// S2 List of objects: a device's operations, page size 30.
import { options as build } from './lib/config.js';
import { nextUser } from './lib/data.js';
import { run } from './lib/scenario.js';
import * as rest from './lib/rest.js';
import { stringField, int32Field } from './lib/protobuf.js';

export const options = build();

export default function () {
    const user = nextUser();
    run({
        token: user.accessToken,
        rest: () => rest.get(`api/v1/devices/operations/display?deviceId=${user.deviceId}&page=1&size=30`, user.accessToken),
        grpc: { method: 'senswave.devices.v1.OperationsService/DisplayOperations', message: { device_id: user.deviceId, page: 1, size: 30 } },
        grpcWeb: { path: 'senswave.devices.v1.OperationsService/DisplayOperations', bytes: [...stringField(1, user.deviceId), ...int32Field(2, 1), ...int32Field(3, 30)] },
    });
}
```

`s3.js`:
```javascript
// S3 Nested list: a dashboard's display layout with dynamic values.
import { options as build } from './lib/config.js';
import { nextUser } from './lib/data.js';
import { run } from './lib/scenario.js';
import * as rest from './lib/rest.js';
import { stringField } from './lib/protobuf.js';

export const options = build();

export default function () {
    const user = nextUser();
    run({
        token: user.accessToken,
        rest: () => rest.get(`api/v1/devices/dashboards/${user.dashboardId}/display`, user.accessToken),
        grpc: { method: 'senswave.devices.v1.DashboardsService/DisplayDashboard', message: { dashboard_id: user.dashboardId } },
        grpcWeb: { path: 'senswave.devices.v1.DashboardsService/DisplayDashboard', bytes: stringField(1, user.dashboardId) },
    });
}
```

`s4.js`:
```javascript
// S4 String: the current terms and conditions (Markdown, anonymous).
import { options as build } from './lib/config.js';
import { run } from './lib/scenario.js';
import * as rest from './lib/rest.js';

export const options = build();

export default function () {
    run({
        token: null,
        rest: () => rest.get('api/v1/legal/terms'),
        grpc: { method: 'senswave.users.v1.LegalService/GetTermsAndConditions', message: {} },
        grpcWeb: { path: 'senswave.users.v1.LegalService/GetTermsAndConditions', bytes: [] },
    });
}
```

`s5.js`:
```javascript
// S5 Write command: toggle the Pico controller tile (publishes to the device over MQTT).
import exec from 'k6/execution';
import { options as build } from './lib/config.js';
import { nextUser } from './lib/data.js';
import { run } from './lib/scenario.js';
import * as rest from './lib/rest.js';
import { stringField, messageField, boolValue } from './lib/protobuf.js';

export const options = build();

export default function () {
    const user = nextUser();
    const value = exec.scenario.iterationInTest % 2 === 0;
    run({
        token: user.accessToken,
        rest: () => rest.post(`api/v1/devices/${user.deviceId}/tile/action`, { value }, user.accessToken),
        grpc: { method: 'senswave.devices.v1.DevicesService/DeviceTileAction', message: { device_id: user.deviceId, value: value } },
        grpcWeb: { path: 'senswave.devices.v1.DevicesService/DeviceTileAction', bytes: [...stringField(1, user.deviceId), ...messageField(2, boolValue(value))] },
    });
}
```

(For native gRPC, k6 maps a JSON boolean to `google.protobuf.Value` through protojson; if it rejects it, use `value: { bool_value: value }` and note it.)

- [ ] **Step 6: Remove the old script and verify each scenario against the local stack**

```bash
git rm performance/k6/legal-terms.js
```
With the stack prepared (Task 6) and `Rest` active, write a `run-data.json` by running the runner discovery once (simplest: `dotnet run --project performance/tools/PerfRunner -- run performance/runner/campaigns/smoke.json` with `smoke.json` temporarily pointing at `https://localhost:8443` / `https://localhost:9443` and `AGENT_TOKEN`, `K6_DIR=performance/k6` set; stop it after the first `run-data.json` appears under `performance/results/`). Then for each transport (activate it via the agent first):
```bash
cd performance/k6
for s in s1 s2 s3 s4 s5; do k6 run -e TRANSPORT=rest -e BASE_URL=https://localhost:8443 -e MODE=verify -e INSECURE=true -e DATA_FILE=<abs path to run-data.json> $s.js; done
```
Expected: every run ends with `checks ... 100.00%` and exit code 0, for `rest`, `grpc` and `grpcweb`. Use the k6 Docker image if k6 is not installed (see the old README command, `MSYS_NO_PATHCONV=1` on Git Bash).

- [ ] **Step 7: Commit**

```bash
git add performance/k6
git commit -m "feat: k6 scenarios s1-s5 for rest, grpc and grpc-web"
```

---

### Task 10: PerfStream (S6a, S6b)

**Files:**
- Create: `performance/tools/PerfStream/PerfStream.csproj`, `Program.cs`, `StreamSettings.cs`, `Publishers.cs`, `StreamSummary.cs`

**Interfaces:**
- Consumes: env contract from Task 8 (`TRANSPORT`, `BASE_URL`, `MODE`, `RATE`, `DURATION`, `WARMUP`, `DATA_FILE`, `INSECURE`, `SCENARIO`, `OUT`, `MQTT_*`); `RunData`, `DeliveryTracker`, `Percentiles` (Tasks 3–4); `ApiClientFactory`, `IApiClient.Subscribe`, `IApiClient.TileAction` (Task 7).
- Produces `OUT` JSON:

```json
{ "scenario": "S6a", "transport": "Grpc", "rate": 100, "subscribers": 100,
  "connectSetupMs": { "p50": 0, "p95": 0, "max": 0 },
  "published": 0, "delivered": 0, "lost": 0, "unmatched": 0, "publishFailures": 0, "publishBehind": 0,
  "latencyMs": { "p50": 0, "p90": 0, "p95": 0, "p99": 0, "max": 0 } }
```
Exit code 0 when the run completed (gates are applied by the runner), 1 on setup failure; in `verify` mode exit 0 only if every subscriber connected and at least one event per 10 subscribers was delivered within 5 s.

- [ ] **Step 1: Create the project**

```bash
cd performance/tools
dotnet new console -n PerfStream -f net10.0
dotnet sln Perf.sln add PerfStream
dotnet add PerfStream reference Perf.Core Perf.Client
```
Add `<PackageReference Include="MQTTnet" />`.

- [ ] **Step 2: Confirm the S6a device message format**

From `DeviceSeedingService.AddStateToController`: the Pico tile is a Switch on the boolean operation "Controller State", whose topic is the user's `GlobalTopic` (`perf/controller/{n}` in the perf stack) with `isJson: true` and JSON name `working`. So a device message is `{"working":true}` on `perf/controller/{n}`. Confirm on the local stack before writing code: with `Rest` active and one SignalR or gRPC subscription open (or simply by reading the tile with `GET api/v1/devices/{picoId}/display` before and after), run `docker exec Senswave.Perf.Mqtt mosquitto_pub -t perf/controller/1 -m '{"working":false}'` and check the tile value changes. Check in `src/Modules/DataSource/Senswave.DataSources.BrokerConnection` whether the API subscribes to the topic exactly or with a suffix. Record the result in `performance/NOTES-phase1.md`.

- [ ] **Step 3: Write `StreamSettings.cs`, `Publishers.cs`, `StreamSummary.cs`, `Program.cs`**

```csharp
using Perf.Core;

namespace PerfStream;

public sealed record StreamSettings(
    Scenario Scenario, Transport Transport, Uri BaseUrl, string Mode, int Rate, int DurationSeconds, int WarmupSeconds,
    string DataFile, bool Insecure, string Out, string MqttHost, int MqttPort, string? MqttUser, string? MqttPassword, bool MqttTls)
{
    public static StreamSettings FromEnvironment() => new(
        Enum.Parse<Scenario>(Env("SCENARIO")), Enum.Parse<Transport>(Env("TRANSPORT"), ignoreCase: true), new Uri(Env("BASE_URL").TrimEnd('/') + "/"),
        Env("MODE"), int.Parse(Env("RATE")), int.Parse(Env("DURATION").TrimEnd('s')), int.Parse(Environment.GetEnvironmentVariable("WARMUP") ?? "0"),
        Env("DATA_FILE"), Environment.GetEnvironmentVariable("INSECURE") == "true", Env("OUT"),
        Environment.GetEnvironmentVariable("MQTT_HOST") ?? "localhost", int.Parse(Environment.GetEnvironmentVariable("MQTT_PORT") ?? "1883"),
        Environment.GetEnvironmentVariable("MQTT_USERNAME"), Environment.GetEnvironmentVariable("MQTT_PASSWORD"),
        Environment.GetEnvironmentVariable("MQTT_TLS") == "true");

    private static string Env(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"{name} is required.");
}
```

```csharp
using MQTTnet;
using MQTTnet.Client;
using Perf.Client;
using Perf.Core.Runs;

namespace PerfStream;

public interface IPublisher : IAsyncDisposable
{
    /// <summary>Triggers one live update for <paramref name="user"/>'s device.</summary>
    Task Publish(UserData user, long sequence, CancellationToken ct);
}

/// <summary>S6a: acts as the device and publishes to the external MQTT broker.</summary>
public sealed class MqttPublisher(IMqttClient client) : IPublisher
{
    public static async Task<MqttPublisher> Connect(StreamSettings s, CancellationToken ct)
    {
        var client = new MqttFactory().CreateMqttClient();
        var options = new MqttClientOptionsBuilder().WithTcpServer(s.MqttHost, s.MqttPort).WithClientId($"perfstream-{Guid.NewGuid():N}");
        if (s.MqttUser is { Length: > 0 })
            options = options.WithCredentials(s.MqttUser, s.MqttPassword);
        if (s.MqttTls)
            options = options.WithTlsOptions(o => o.UseTls());
        await client.ConnectAsync(options.Build(), ct);
        return new MqttPublisher(client);
    }

    public async Task Publish(UserData user, long sequence, CancellationToken ct)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(DeviceTopic(user))
            .WithPayload(DevicePayload(sequence))
            .Build();
        await client.PublishAsync(message, ct);
    }

    // The seeded Pico controller's tile is a Switch bound to the "Controller State" operation:
    // topic = Seed GlobalTopic "perf/controller/{n}" (n from user{n}@gmail.com), JSON key "working".
    internal static string DeviceTopic(UserData user) =>
        $"perf/controller/{System.Text.RegularExpressions.Regex.Match(user.Email, @"\d+").Value}";

    // Alternate so every message is a value change.
    internal static string DevicePayload(long sequence) => sequence % 2 == 0 ? "{\"working\":true}" : "{\"working\":false}";

    public async ValueTask DisposeAsync()
    {
        await client.DisconnectAsync();
        client.Dispose();
    }
}

/// <summary>S6b: a user action through the API over the transport under test (SendEvents on).</summary>
public sealed class TileActionPublisher(IApiClient api) : IPublisher
{
    public Task Publish(UserData user, long sequence, CancellationToken ct) => api.TileAction(user.AccessToken, user.DeviceId, sequence % 2 == 0, ct);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

If Step 2 found a different topic or payload than the one encoded in `DeviceTopic` / `DevicePayload`, change those two methods to match and record it.

```csharp
namespace PerfStream;

public sealed record Percentile3(double P50, double P95, double Max);
public sealed record Percentile5(double P50, double P90, double P95, double P99, double Max);

public sealed record StreamSummary(
    string Scenario, string Transport, int Rate, int Subscribers, Percentile3 ConnectSetupMs,
    long Published, long Delivered, long Lost, long Unmatched, long PublishFailures, long PublishBehind, Percentile5 LatencyMs);
```

```csharp
using System.Diagnostics;
using System.Text.Json;
using Perf.Client;
using Perf.Core;
using Perf.Core.Runs;
using Perf.Core.Streams;
using PerfStream;

var s = StreamSettings.FromEnvironment();
var data = JsonSerializer.Deserialize<RunData>(await File.ReadAllTextAsync(s.DataFile), PerfJson.Options)!;
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var clock = Stopwatch.StartNew();
long Now() => clock.Elapsed.Ticks;

var tracker = new DeliveryTracker();
await using var api = ApiClientFactory.Create(s.Transport, new ApiTarget(s.BaseUrl, s.Insecure));

// 1. One subscriber per user (its home), all confirmed before publishing starts.
var setup = new List<double>();
var subscriptions = new List<ILiveUpdateSubscription>();
foreach (var user in data.Users)
{
    var t0 = Now();
    subscriptions.Add(await api.Subscribe(user.AccessToken, user.HomeId, deviceId => tracker.Delivered(deviceId, Now()), cts.Token));
    setup.Add(TimeSpan.FromTicks(Now() - t0).TotalMilliseconds);
}

await using IPublisher publisher = s.Scenario == Scenario.S6a ? await MqttPublisher.Connect(s, cts.Token) : new TileActionPublisher(api);

// 2. Open-model publishing: event i is due at start + i/rate, round-robin over users. Warm-up events are not recorded.
var warmup = s.Mode == "verify" ? 0 : s.WarmupSeconds;
var total = (warmup + s.DurationSeconds) * (long)s.Rate;
var measuredFrom = warmup * (long)s.Rate;
long failures = 0, behind = 0;
var inflight = new List<Task>();
var start = Now();

for (long i = 0; i < total && !cts.IsCancellationRequested; i++)
{
    var due = start + TimeSpan.FromSeconds((double)i / s.Rate).Ticks;
    var wait = due - Now();
    if (wait > TimeSpan.TicksPerMillisecond)
        await Task.Delay(TimeSpan.FromTicks(wait), cts.Token);
    else if (wait < -TimeSpan.FromMilliseconds(100).Ticks)
        behind++;

    var user = data.Users[(int)(i % data.Users.Count)];
    tracker.Published(user.DeviceId, Now(), measured: i >= measuredFrom);
    var sequence = i;
    inflight.Add(Task.Run(async () =>
    {
        try { await publisher.Publish(user, sequence, cts.Token); }
        catch { Interlocked.Increment(ref failures); }
    }));
    if (inflight.Count > 2000)
        inflight.RemoveAll(t => t.IsCompleted);
}

await Task.WhenAll(inflight);
await Task.Delay(TimeSpan.FromSeconds(5));           // grace for in-flight deliveries
var result = tracker.Finish(Now(), TimeSpan.FromSeconds(5));

foreach (var subscription in subscriptions)
    await subscription.DisposeAsync();

var setupP = Percentiles.Of(setup.ToArray(), 50, 95, 100);
var latencyP = Percentiles.Of(result.LatenciesMs, 50, 90, 95, 99, 100);
var summary = new StreamSummary(s.Scenario.ToString(), s.Transport.ToString(), s.Rate, subscriptions.Count,
    new Percentile3(setupP[0], setupP[1], setupP[2]),
    result.Published, result.Delivered, result.Lost, result.Unmatched, failures, behind,
    new Percentile5(latencyP[0], latencyP[1], latencyP[2], latencyP[3], latencyP[4]));
await File.WriteAllTextAsync(s.Out, JsonSerializer.Serialize(summary, PerfJson.Options));

Console.WriteLine(JsonSerializer.Serialize(summary, PerfJson.Options));
return s.Mode == "verify" && result.Delivered < Math.Max(1, data.Users.Count / 10) ? 1 : 0;
```

In verify mode the runner passes `DURATION=5s`, `WARMUP=0` and `RATE` from the run, so a 5-second burst checks delivery end-to-end.

- [ ] **Step 4: Build** → `dotnet build performance/tools/Perf.sln` → Expected: Build succeeded (only after Step 3's required edit).

- [ ] **Step 5: Verify S6a and S6b on the local stack for all transports**

```bash
for t in Rest Grpc GrpcWeb; do
  curl -sk -X POST -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{\"transport\":\"$t\",\"otel\":false,\"runId\":\"stream-check\"}" https://localhost:9443/activate > /dev/null
  # regenerate run-data.json for this transport (tokens are per login, the transport does not matter for them, but keep the flow realistic)
  for sc in S6a S6b; do
    SCENARIO=$sc TRANSPORT=$t BASE_URL=https://localhost:8443 MODE=verify RATE=100 DURATION=5s WARMUP=0 INSECURE=true \
    DATA_FILE=<abs path to run-data.json> OUT=/tmp/$t-$sc.json MQTT_HOST=localhost MQTT_PORT=1883 \
    dotnet run --project performance/tools/PerfStream && echo "$t $sc OK"
  done
done
```
Expected: six `OK` lines; each summary shows `delivered` > 0 and `lost` close to 0.

- [ ] **Step 6: Record whether SignalR used WebSockets over HTTP/2**

If the `Rest` verify fails with a WebSocket/HTTP version error, Kestrel or the client does not support RFC 8441 here. Change `RestApiClient.Subscribe` to `HttpVersion.Version11` with `HttpVersionPolicy.RequestVersionOrLower`, rerun Step 5, and record in `NOTES-phase1.md` that S6 for REST + SignalR runs over HTTP/1.1 (this becomes a README "Problems and solutions" entry and a thesis limitation). If it passes, record that it ran over HTTP/2.

- [ ] **Step 7: Commit**

```bash
git add performance/tools performance/NOTES-phase1.md
git commit -m "feat: perfstream for s6a and s6b live-update scenarios"
```

---

### Task 11: Alloy, Grafana dashboard, export and archive

**Files:**
- Create: `performance/stack/alloy/config.alloy`, `performance/grafana/dashboard.json`
- Create: `performance/tools/PerfRunner/Export/PrometheusClient.cs`, `Export/ServerExporter.cs`, `Export/TraceExporter.cs`, `Export/Archiver.cs`
- Modify: `performance/stack/compose.yml` (add `alloy`), `performance/stack/.env.template` (Grafana Cloud vars), `performance/tools/PerfRunner/Program.cs` (add `export`, `archive`; export at the end of `run`)

**Interfaces:**
- Consumes: `ExportQueries.For`, `RunMeta`, `BlobUrl.Build`, `PerfJson` (Task 4); `RunnerSettings` Prom/Tempo/Archive fields (Task 8).
- Produces: `performance/results/<campaign>/<run>/server/*.csv`; optional `traces.jsonl`; a zip in Blob storage.

- [ ] **Step 1: Write `performance/stack/alloy/config.alloy`**

```alloy
// Grafana Alloy on the VM (Spec E): app OTLP + container (cAdvisor) + host metrics to Grafana Cloud. No logs.

otelcol.receiver.otlp "apps" {
  grpc { endpoint = "0.0.0.0:4317" }
  http { endpoint = "0.0.0.0:4318" }
  output {
    metrics = [otelcol.processor.transform.promote.input]
    traces  = [otelcol.processor.batch.default.input]
  }
}

// Grafana Cloud keeps resource attributes on target_info only; copy run_id and transport onto every data point
// so exports can filter by run.
otelcol.processor.transform "promote" {
  error_mode = "ignore"
  metric_statements {
    context    = "datapoint"
    statements = [
      `set(attributes["run_id"], resource.attributes["run_id"])`,
      `set(attributes["transport"], resource.attributes["senswave.transport"])`,
    ]
  }
  output { metrics = [otelcol.processor.batch.default.input] }
}

otelcol.processor.batch "default" {
  output {
    metrics = [otelcol.exporter.otlphttp.grafana_cloud.input]
    traces  = [otelcol.exporter.otlphttp.grafana_cloud.input]
  }
}

otelcol.auth.basic "grafana_cloud" {
  username = sys.env("GRAFANA_CLOUD_OTLP_USER")
  password = sys.env("GRAFANA_CLOUD_WRITE_TOKEN")
}

otelcol.exporter.otlphttp "grafana_cloud" {
  client {
    endpoint = sys.env("GRAFANA_CLOUD_OTLP_ENDPOINT")
    auth     = otelcol.auth.basic.grafana_cloud.handler
  }
}

prometheus.exporter.cadvisor "containers" {
  docker_host      = "unix:///var/run/docker.sock"
  docker_only      = true
  storage_duration = "2m"
}

prometheus.exporter.unix "host" {
  procfs_path = "/host/proc"
  sysfs_path  = "/host/sys"
  rootfs_path = "/host/root"
}

prometheus.scrape "containers" {
  targets         = prometheus.exporter.cadvisor.containers.targets
  scrape_interval = "10s"
  forward_to      = [prometheus.relabel.env.receiver]
}

prometheus.scrape "host" {
  targets         = prometheus.exporter.unix.host.targets
  scrape_interval = "10s"
  forward_to      = [prometheus.relabel.env.receiver]
}

prometheus.relabel "env" {
  rule {
    target_label = "env"
    replacement  = sys.env("PERF_ENV")
  }
  forward_to = [prometheus.remote_write.grafana_cloud.receiver]
}

prometheus.remote_write "grafana_cloud" {
  endpoint {
    url = sys.env("GRAFANA_CLOUD_PROM_URL")
    basic_auth {
      username = sys.env("GRAFANA_CLOUD_PROM_USER")
      password = sys.env("GRAFANA_CLOUD_WRITE_TOKEN")
    }
  }
}
```

If the Alloy version in use rejects `sys.env`, use `env(...)` instead (older syntax) and note it.

- [ ] **Step 2: Add Alloy to `performance/stack/compose.yml` and Grafana Cloud vars to `.env.template`**

```yaml
  alloy:
    image: grafana/alloy:v1.10.0
    container_name: Senswave.Perf.Alloy
    restart: unless-stopped
    cpuset: ${SIDE_CPUSET:-0-1}
    cpus: 1
    mem_limit: 1g
    memswap_limit: 1g
    privileged: true            # cAdvisor needs cgroup and device access
    networks: [ perf ]
    command: [ "run", "/etc/alloy/config.alloy", "--storage.path=/var/lib/alloy/data" ]
    volumes:
      - ./alloy/config.alloy:/etc/alloy/config.alloy:ro
      - /var/run/docker.sock:/var/run/docker.sock:ro
      - /:/host/root:ro
      - /proc:/host/proc:ro
      - /sys:/host/sys:ro
      - /var/lib/docker/:/var/lib/docker:ro
    environment:
      PERF_ENV: ${PERF_ENV:-local}
      GRAFANA_CLOUD_OTLP_ENDPOINT: ${GRAFANA_CLOUD_OTLP_ENDPOINT}
      GRAFANA_CLOUD_OTLP_USER: ${GRAFANA_CLOUD_OTLP_USER}
      GRAFANA_CLOUD_PROM_URL: ${GRAFANA_CLOUD_PROM_URL}
      GRAFANA_CLOUD_PROM_USER: ${GRAFANA_CLOUD_PROM_USER}
      GRAFANA_CLOUD_WRITE_TOKEN: ${GRAFANA_CLOUD_WRITE_TOKEN}
```

`.env.template` additions:
```dotenv
# Grafana Cloud (stack → "OpenTelemetry" and "Prometheus" details pages). Write token: metrics:write, traces:write.
GRAFANA_CLOUD_OTLP_ENDPOINT=https://otlp-gateway-prod-eu-west-2.grafana.net/otlp
GRAFANA_CLOUD_OTLP_USER=
GRAFANA_CLOUD_PROM_URL=https://prometheus-prod-XX-prod-eu-west-2.grafana.net/api/prom/push
GRAFANA_CLOUD_PROM_USER=
GRAFANA_CLOUD_WRITE_TOKEN=
```

- [ ] **Step 3: Write `Export/PrometheusClient.cs`, `Export/ServerExporter.cs`**

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace PerfRunner.Export;

/// <summary>Grafana Cloud Prometheus HTTP API (read token: metrics:read).</summary>
public sealed class PrometheusClient : IDisposable
{
    private readonly HttpClient _http;

    public PrometheusClient(string baseUrl, string user, string token)
    {
        // GRAFANA_PROM_URL is the query base, e.g. https://prometheus-prod-XX-prod-eu-west-2.grafana.net/api/prom
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{token}")));
    }

    public Task<JsonNode> Instant(string query, DateTimeOffset time, CancellationToken ct) =>
        Get($"api/v1/query?query={Uri.EscapeDataString(query)}&time={time.ToUnixTimeSeconds()}", ct);

    public Task<JsonNode> Range(string query, DateTimeOffset start, DateTimeOffset end, int stepSeconds, CancellationToken ct) =>
        Get($"api/v1/query_range?query={Uri.EscapeDataString(query)}&start={start.ToUnixTimeSeconds()}&end={end.ToUnixTimeSeconds()}&step={stepSeconds}", ct);

    private async Task<JsonNode> Get(string path, CancellationToken ct)
    {
        var response = await _http.GetAsync(path, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Prometheus {(int)response.StatusCode}: {body}");
        return JsonNode.Parse(body)!["data"]!;
    }

    public void Dispose() => _http.Dispose();
}
```

```csharp
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Perf.Core;
using Perf.Core.Export;
using Perf.Core.Runs;

namespace PerfRunner.Export;

/// <summary>Writes server/*.csv for every valid run of a campaign. Re-runnable: overwrites the files.</summary>
public sealed class ServerExporter(PrometheusClient prometheus)
{
    public async Task ExportCampaign(string campaignDir, CancellationToken ct)
    {
        foreach (var metaPath in Directory.GetFiles(campaignDir, "meta.json", SearchOption.AllDirectories))
        {
            var meta = JsonSerializer.Deserialize<RunMeta>(await File.ReadAllTextAsync(metaPath, ct), PerfJson.Options)!;
            if (meta.Status != "valid" || meta.MeasureStartUtc is null || meta.MeasureEndUtc is null)
                continue;

            var serverDir = Path.Combine(Path.GetDirectoryName(metaPath)!, "server");
            Directory.CreateDirectory(serverDir);
            var files = new Dictionary<string, StringBuilder>();

            foreach (var query in ExportQueries.For(meta))
            {
                var csv = files.TryGetValue(query.File, out var existing) ? existing : files[query.File] = new StringBuilder("query,series,timestamp,value\n");
                var data = query.Range
                    ? await prometheus.Range(query.PromQl, meta.MeasureStartUtc.Value, meta.MeasureEndUtc.Value, 10, ct)
                    : await prometheus.Instant(query.PromQl, meta.MeasureEndUtc.Value, ct);
                Append(csv, query.PromQl, data);
            }

            foreach (var (file, csv) in files)
                await File.WriteAllTextAsync(Path.Combine(serverDir, file), csv.ToString(), ct);

            Console.WriteLine($"exported {meta.RunId}");
        }
    }

    private static void Append(StringBuilder csv, string query, JsonNode data)
    {
        foreach (var series in data["result"]!.AsArray())
        {
            var labels = Quote(series!["metric"]!.ToJsonString());
            var points = series["values"]?.AsArray() ?? new JsonArray(series["value"]!.DeepClone());
            foreach (var point in points)
                csv.Append(Quote(query)).Append(',').Append(labels).Append(',')
                    .Append(point![0]!.ToString()).Append(',').Append(point[1]!.GetValue<string>()).Append('\n');
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
```

- [ ] **Step 4: Write `Export/TraceExporter.cs` and `Export/Archiver.cs`**

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Perf.Core;
using Perf.Core.Runs;

namespace PerfRunner.Export;

/// <summary>Optional: up to 500 traces per valid run from the Grafana Cloud Tempo HTTP API, one JSON trace per line.</summary>
public sealed class TraceExporter(string tempoUrl, string user, string token)
{
    public async Task ExportCampaign(string campaignDir, CancellationToken ct)
    {
        using var http = new HttpClient { BaseAddress = new Uri(tempoUrl.TrimEnd('/') + "/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{token}")));

        foreach (var metaPath in Directory.GetFiles(campaignDir, "meta.json", SearchOption.AllDirectories))
        {
            var meta = JsonSerializer.Deserialize<RunMeta>(await File.ReadAllTextAsync(metaPath, ct), PerfJson.Options)!;
            if (meta.Status != "valid" || meta.MeasureStartUtc is null)
                continue;

            var q = Uri.EscapeDataString($"{{ resource.run_id = \"{meta.RunId}\" }}");
            var search = JsonNode.Parse(await http.GetStringAsync(
                $"api/search?q={q}&start={meta.MeasureStartUtc.Value.ToUnixTimeSeconds()}&end={meta.MeasureEndUtc!.Value.ToUnixTimeSeconds()}&limit=500", ct))!;

            await using var output = File.CreateText(Path.Combine(Path.GetDirectoryName(metaPath)!, "server", "traces.jsonl"));
            foreach (var trace in search["traces"]?.AsArray() ?? [])
                await output.WriteLineAsync(await http.GetStringAsync($"api/traces/{trace!["traceID"]!.GetValue<string>()}", ct));
        }
    }
}
```

```csharp
using System.IO.Compression;
using Perf.Core.Export;

namespace PerfRunner.Export;

/// <summary>Zips a campaign directory and uploads it with one Put Blob request to a container SAS URL.</summary>
public static class Archiver
{
    public static async Task<Uri> Upload(string campaignDir, string containerSasUrl, CancellationToken ct)
    {
        var zip = Path.Combine(Path.GetTempPath(), Path.GetFileName(campaignDir.TrimEnd('/', '\\')) + ".zip");
        File.Delete(zip);
        ZipFile.CreateFromDirectory(campaignDir, zip, CompressionLevel.Optimal, includeBaseDirectory: true);

        var url = BlobUrl.Build(new Uri(containerSasUrl), Path.GetFileName(zip));
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        await using var file = File.OpenRead(zip);
        using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = new StreamContent(file) };
        request.Headers.Add("x-ms-blob-type", "BlockBlob");
        request.Headers.Add("x-ms-version", "2021-08-06");
        var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Blob upload {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");

        return new Uri(url.GetLeftPart(UriPartial.Path));
    }
}
```

- [ ] **Step 5: Wire into `Program.cs`**

Add before `default:`:
```csharp
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
```
and at the end of the `run` case, before `return 0;`: `await Export(settings, dir, campaign.Export.Traces, cts.Token);`. Add the local function at the bottom of `Program.cs`:

```csharp
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
```

- [ ] **Step 6: Verify metric names and the `run_id` label against Grafana Cloud**

Start Alloy locally (`docker compose -f performance/local/compose.yml --env-file performance/stack/.env up -d alloy`, with Grafana Cloud values in `.env`), activate `Grpc` with `runId` `names-check`, send a few requests (k6 verify `s1.js`), wait 60 s, then:
```bash
curl -s -u "$GRAFANA_PROM_USER:$GRAFANA_READ_TOKEN" "$GRAFANA_PROM_URL/api/v1/label/__name__/values" | tr ',' '\n' | grep -E "http_server_request_duration|process_cpu|dotnet_gc|dotnet_thread_pool|kestrel|container_cpu|container_memory_working|container_network"
curl -s -u "$GRAFANA_PROM_USER:$GRAFANA_READ_TOKEN" "$GRAFANA_PROM_URL/api/v1/query" --data-urlencode 'query=count by (run_id, transport) (http_server_request_duration_seconds_count{run_id="names-check"})'
```
Expected: each metric family used in `ExportQueries.For` is listed (fix any differing name in `Perf.Core/Export/ExportQueries.cs` and its test), and the second query returns a series with `run_id="names-check"` and `transport="Grpc"` (confirms `OTEL_RESOURCE_ATTRIBUTES` reaches the resource and Alloy promotes it). Record results in `NOTES-phase1.md`. On Docker Desktop, cAdvisor may report container metrics without the `name` label or not at all: record it; the Azure VM is the reference.

- [ ] **Step 7: Write `performance/grafana/dashboard.json`**

A Grafana dashboard (schemaVersion 39) with a `$run_id` textbox variable and a `$transport` custom variable (`Rest,Grpc,GrpcWeb`), datasource `${DS_PROMETHEUS}` as an input, and these timeseries panels (one target each, PromQL):
1. Server latency p50/p95/p99 — `histogram_quantile(0.99, sum by (le) (rate(http_server_request_duration_seconds_bucket{run_id=~"$run_id"}[1m])))` (+ p50, p95 targets)
2. Requests/s — `sum by (transport) (rate(http_server_request_duration_seconds_count{run_id=~"$run_id"}[1m]))`
3. CPU-ms per request — `1000 * sum(rate(process_cpu_time_seconds_total{run_id=~"$run_id"}[1m])) / sum(rate(http_server_request_duration_seconds_count{run_id=~"$run_id"}[1m]))`
4. Allocated bytes per request — `sum(rate(dotnet_gc_heap_total_allocated_bytes_total{run_id=~"$run_id"}[1m])) / sum(rate(http_server_request_duration_seconds_count{run_id=~"$run_id"}[1m]))`
5. Container CPU — `rate(container_cpu_usage_seconds_total{name=~"Senswave.Perf.*"}[1m])`
6. Container memory — `container_memory_working_set_bytes{name=~"Senswave.Perf.*"}`
7. Network bytes/s — `rate(container_network_transmit_bytes_total{name=~"Senswave.Perf.*"}[1m])`
8. Host CPU steal — `rate(node_cpu_seconds_total{mode="steal"}[1m])`

Write it out in full as valid JSON (each panel: `type`, `title`, `gridPos`, `datasource`, `targets[{expr, legendFormat, refId}]`). Validate with `python -m json.tool performance/grafana/dashboard.json > /dev/null`.

- [ ] **Step 8: Build, test, commit**

```bash
dotnet build performance/tools/Perf.sln && dotnet test performance/tools/Perf.sln
git add performance/stack performance/grafana performance/tools performance/NOTES-phase1.md
git commit -m "feat: alloy pipeline, grafana dashboard, server export and archive"
```

---

### Task 12: Runner image and compose

**Files:**
- Create: `performance/tools/PerfRunner/Dockerfile`, `performance/runner/compose.yml`, `performance/runner/.env.template`

**Interfaces:**
- Consumes: PerfRunner + PerfStream projects, k6 scripts, protos, campaigns.
- Produces: `docker compose -f performance/runner/compose.yml run --rm runner run runner/campaigns/smoke.json` (working dir `/work/performance`).

- [ ] **Step 1: Write the Dockerfile** (build context: repo root)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY Directory.Packages.props global.json ./
COPY src/Shared/Senswave.Contracts src/Shared/Senswave.Contracts
COPY performance/tools performance/tools
RUN dotnet publish performance/tools/PerfRunner/PerfRunner.csproj -c Release -o /app \
 && dotnet publish performance/tools/PerfStream/PerfStream.csproj -c Release -o /app/stream

FROM mcr.microsoft.com/dotnet/runtime:10.0
COPY --from=grafana/k6:1.3.0 /usr/bin/k6 /usr/bin/k6
COPY --from=build /app /app
COPY src/Shared/Senswave.Contracts/Protos /work/src/Shared/Senswave.Contracts/Protos
COPY performance/k6 /work/performance/k6
WORKDIR /work/performance
ENTRYPOINT ["dotnet", "/app/PerfRunner.dll"]
```

PerfStream is published framework-dependent; set `STREAM_BINARY=/app/stream/PerfStream` (apphost). If the apphost is missing in the runtime image, use `dotnet /app/stream/PerfStream.dll` by making `StreamTool` start `dotnet` with the dll as first argument when `STREAM_BINARY` ends in `.dll`, and note it.

- [ ] **Step 2: Write `performance/runner/compose.yml` and `.env.template`**

```yaml
name: senswave-perf-runner

# Load side: run on your machine or the separate 2-vCPU box.
#   docker compose -f performance/runner/compose.yml run --rm runner prepare runner/campaigns/smoke.json
#   docker compose -f performance/runner/compose.yml run --rm runner run runner/campaigns/smoke.json
services:
  runner:
    image: senswave-perf-runner
    build:
      context: ../..
      dockerfile: performance/tools/PerfRunner/Dockerfile
    env_file: .env
    extra_hosts:
      - host.docker.internal:host-gateway
    volumes:
      - ../results:/work/performance/results
      - ./campaigns:/work/performance/runner/campaigns:ro
```

`.env.template`:
```dotenv
AGENT_TOKEN=change-me-to-a-long-random-string
# Event source for S6a (the external broker; locally the stack's mosquitto on the host)
MQTT_HOST=host.docker.internal
MQTT_PORT=1883
MQTT_USERNAME=
MQTT_PASSWORD=
MQTT_TLS=false
STREAM_BINARY=/app/stream/PerfStream
# Grafana Cloud read access for the export (token scopes: metrics:read, traces:read)
GRAFANA_PROM_URL=https://prometheus-prod-XX-prod-eu-west-2.grafana.net/api/prom
GRAFANA_PROM_USER=
GRAFANA_TEMPO_URL=https://tempo-prod-XX-prod-eu-west-2.grafana.net/tempo
GRAFANA_TEMPO_USER=
GRAFANA_READ_TOKEN=
# k6 client metrics to Grafana Cloud (optional)
K6_PROMETHEUS_RW_SERVER_URL=
K6_PROMETHEUS_RW_USERNAME=
K6_PROMETHEUS_RW_PASSWORD=
K6_PROMETHEUS_RW_TREND_STATS=p(50),p(95),p(99),max
# Archive: container SAS URL with write permission
ARCHIVE_SAS_URL=
```

- [ ] **Step 3: Build and smoke the image**

```bash
cp performance/runner/.env.template performance/runner/.env   # set AGENT_TOKEN to the stack's value
docker compose -f performance/runner/compose.yml build
docker compose -f performance/runner/compose.yml run --rm runner
```
Expected: the usage line (`usage: perfrunner run|prepare ...`) and exit code 2. `docker compose -f performance/runner/compose.yml run --rm --entrypoint k6 runner version` prints `k6 v1.3.0`.

- [ ] **Step 4: Commit**

```bash
git add performance/tools/PerfRunner/Dockerfile performance/runner
git commit -m "feat: perf runner image with k6 and perfstream"
```

---

### Task 13: README

**Files:**
- Rewrite: `performance/README.md`
- Delete: `performance/NOTES-phase1.md` (content folded into the README)
- Modify: `docs/comparison.md` (the "Running instances" section points to the new setup)

- [ ] **Step 1: Write `performance/README.md`** with these sections, in order. All commands are real, copy-paste-ready, from the repo root, one command per fenced `bash` block.

1. **Overview**: the architecture diagram from the spec; one run = reset → verification → warm-up → measurement → gates; the scenario table (S1–S6b with REST endpoint and rpc, copied from the spec); rates 100/500/1000; validity gates.
2. **Local quick start**: `generate-cert.sh`; copy both `.env.template` files and set `AGENT_TOKEN`; `docker compose -f performance/local/compose.yml --env-file performance/stack/.env --profile seed up --no-start --build`; `... up -d database mqtt perf-agent alloy`; runner `prepare` then `run runner/campaigns/smoke.json`; where results appear.
3. **Azure setup**, step by step: resource group; VNet + subnet; VM **D4as_v5** (Ubuntu 24.04, accelerated networking on, static public IP, NSG allowing 22 from your IP and `API_PORT`/`AGENT_PORT` from the load machine's IP); Flexible Server **General Purpose D2ds_v5**, PostgreSQL 17, same region and **same availability zone**, private access in the VNet; Docker install; clone the repo at the commit under test; Cloudflare A record **DNS only**; Let's Encrypt with the Cloudflare DNS-01 challenge (certbot `dns-cloudflare` plugin run as a container, API token scoped to the zone), then `openssl pkcs12 -export` into `senswave.api.pfx` in `CERT_DIR`; `.env` values for Azure (`PERF_API_HOST=<dns name>`, `PERF_ENV=azure`, `POSTGRES_HOST=<flexible server host>`, `SEED_SSL_CERT_FILE=/etc/ssl/certs/ca-certificates.crt`, broker values, `GIT_SHA=$(git rev-parse HEAD)`); `lscpu -e` and adjusting `API_CPUSET`/`SIDE_CPUSET`; `up --no-start`, `up -d perf-agent alloy`; `prepare`.
4. **Practical points (don't forget)**: the checklist from the spec's README section, verbatim.
5. **Running campaigns**: campaign JSON reference (every field), results layout (every file), what `invalid` / `failed-verification` mean and what to do, `export` and `archive` commands, backup routine (export after every run is automatic at the end of `run`; archive after every session; aggregated CSV to the thesis repo).
6. **Monitoring**: the Alloy pipeline table, labels (`run_id`, `transport`, `env`), how to import `grafana/dashboard.json`, Grafana Cloud retention warning.
7. **Implementation problems and solutions**: every known item from the spec plus every finding in `NOTES-phase1.md` (feasibility checks, seed index, Mosquitto/TLS, SignalR HTTP/2 result, metric names, Docker Desktop cAdvisor limits, image tags changed), each as **Problem** / **Solution**.

- [ ] **Step 2: Update `docs/comparison.md`**

Replace the paragraph under "Running instances (load and latency)" that describes "one container per transport ... ready setup" with a short paragraph linking `performance/README.md`: one API container active at a time, switched by PerfAgent; keep the rest of the section (rate limiting, HTTPS/HTTP/2, sampling ratio).

- [ ] **Step 3: Commit**

```bash
git rm performance/NOTES-phase1.md
git add performance/README.md docs/comparison.md
git commit -m "docs: performance platform readme and azure guide"
```

---

### Task 14: Local smoke campaign (acceptance)

**Files:** none new; fixes go to the file at fault, each with its own commit.

- [ ] **Step 1: Fresh local stack**

```bash
docker compose -f performance/local/compose.yml --env-file performance/stack/.env down -v
docker compose -f performance/local/compose.yml --env-file performance/stack/.env --profile seed up --no-start --build
docker compose -f performance/local/compose.yml --env-file performance/stack/.env up -d database mqtt perf-agent alloy
docker compose -f performance/runner/compose.yml run --rm runner prepare runner/campaigns/smoke.json
```
Expected: seed log ends with `Initial seed finished successfully.`

- [ ] **Step 2: Run the smoke campaign**

```bash
docker compose -f performance/runner/compose.yml run --rm runner run runner/campaigns/smoke.json
```
Expected: 21 runs (3 transports × 7 scenarios × rate 100), each printed as `valid`; total time about 21 × (activation + discovery + 10 s + 30 s). On a machine with too few cores the CPU gate may fail; that is a correct `invalid`, not a bug — rerun with the runner on a separate machine or note it.

- [ ] **Step 3: Check the results**

```bash
ls performance/results/smoke-*/
cat performance/results/smoke-*/r1-*-s3-100-otel-a1/meta.json
```
Expected: one directory per run; every `meta.json` has `"status": "valid"`, limits for all three containers, and UTC windows; k6 runs have `k6-summary.json`, S6 runs `stream-summary.json`; `server/*.csv` exist when Grafana Cloud read settings are configured (otherwise `export` was skipped with a message).

- [ ] **Step 4: Run the full test suites**

```bash
dotnet test performance/tools/Perf.sln
dotnet build Senswave.sln
dotnet test test/Shared/Senswave.ArchitectureTests
```
Expected: all pass. Report any failure verbatim.

- [ ] **Step 5: Commit any fixes and record the run**

Add the smoke run's date, commit SHA and outcome to the README "Problems and solutions" section only if something had to be fixed; otherwise no commit is needed. Phase 1 is done when Steps 2–4 pass.
