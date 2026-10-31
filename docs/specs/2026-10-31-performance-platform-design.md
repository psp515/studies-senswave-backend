# Spec E — Performance Test Platform

**Branch:** `perf/platform`, one commit per delivery step (see "Delivery")
**Depends on:** Spec A (transport switch), Spec B (`GrpcWeb`), Spec D (comparison profile)
**Status:** design approved 2026-10-31. Replaces the current `performance/` setup (three always-on APIs, OTel off, one k6 script).

## Purpose

Measure REST, gRPC and gRPC-Web **from the server's point of view**, under identical conditions, with results that can be reproduced and fed straight into the thesis.

- One API instance under load at a time, the other two stopped.
- Every run starts from the same database state.
- Server telemetry (OpenTelemetry, container and host metrics) goes through Grafana Alloy to Grafana Cloud, and is exported to local files after each run.
- Client metrics (k6, PerfStream) are paired with server metrics for the same time window.
- All three transports run over **HTTP/2 over TLS**.

Out of scope: measurements on the Android device (mode B in the thesis is dropped), horizontal scaling, and a staircase or continuous ramp load model.

## Scenarios and load

| Id | Name | REST | gRPC rpc |
|---|---|---|---|
| S1 | Flat data | `GET /api/v1/users` | `senswave.users.v1.AccountService/GetInfo` |
| S2 | List of objects | `GET /api/v1/devices/operations/display?deviceId=…&size=30` | `senswave.devices.v1.OperationsService/DisplayOperations` |
| S3 | Nested list | `GET /api/v1/devices/dashboards/{id}/display` | `senswave.devices.v1.DashboardsService/DisplayDashboard` |
| S4 | String | `GET /api/v1/legal/terms` | `senswave.users.v1.LegalService/GetTermsAndConditions` |
| S5 | Write command | `POST /api/v1/devices/{id}/tile/action` | `senswave.devices.v1.DevicesService/DeviceTileAction` |
| S6a | Stream, device path | SignalR (WebSocket over HTTP/2) | `senswave.liveupdates.v1.LiveUpdatesService/Subscribe` |
| S6b | Stream, user-action path | SignalR (WebSocket over HTTP/2) | `senswave.liveupdates.v1.LiveUpdatesService/Subscribe` |

The rpc names come from `src/Shared/Senswave.Contracts/Protos`.

- **Load model:** open model, k6 `constant-arrival-rate`. Each scenario runs **three separate runs at 100, 500 and 1000 per second**.
- For S1–S5 the rate is requests per second.
- For S6a and S6b the rate is **events published per second**. There is one subscriber per seeded user's device, so delivered events per second equals published events per second.
  - **S6a:** PerfStream acts as the device and publishes MQTT messages to the external broker. The API receives them and LiveUpdate pushes them to subscribers.
  - **S6b:** PerfStream calls the tile action (S5) with `SendEvents` on, and the API broadcasts to subscribers. The publishing requests also load the API, which is part of what S6b measures.
- **One run:** reset → verification → 30 s warm-up (excluded) → 5 min measurement.
- **Campaign:** rounds × transports (order shuffled per round) × scenarios × rates × OTel on/off.

## Architecture

```
LOAD SIDE (local PC or 2-vCPU box) ─ docker compose: runner
 ┌──────────────────────────────────────────────┐
 │ PerfRunner (.NET) + k6 binary + PerfStream    │
 │  campaign.yaml → for round, transport, scen., │
 │  rate: reset → verify → warm-up → measure     │
 │  → gates → export → results/<campaign>/<run>/ │
 └───────┬───────────────────────┬──────────────┘
   HTTPS agent (token)      HTTPS API (h2) / MQTT
 ┌───────▼────────── TEST VM (Azure D4as_v5) ───┐      ┌──────────────────┐
 │ vCPU 0-1: PerfAgent · Grafana Alloy           │      │ External MQTT     │
 │ vCPU 2-3: api-rest | api-grpc | api-grpcweb   │◄────►│ broker            │
 │           (only one running)                  │      └──────────────────┘
 └───────┬───────────────────────────────────────┘
         │ VNet                   ┌──────────────────────────────┐
         └───────────────────────►│ Azure PG Flexible (GP D2ds)  │
   Alloy ─► Grafana Cloud          │ senswave_template → per-run  │
                                   └──────────────────────────────┘
```

- The VM's public name is a Cloudflare record in **DNS-only** mode (no proxy). The agent and the API each have their own TLS port.
- The **local phase** uses the same layout on one machine. Local Postgres and Mosquitto containers stand in for Flexible Server and the external broker.

## Components

### Layout

```
performance/
  README.md
  stack/compose.yml            # VM: 3 APIs, agent, alloy
  stack/alloy/config.alloy
  stack/.env.template
  local/compose.yml            # includes stack/, adds postgres + mosquitto
  runner/compose.yml           # load side: PerfRunner image
  runner/campaigns/*.yaml      # smoke.yaml, full.yaml
  k6/lib/                      # rest, grpc, grpc-web clients, options builder
  k6/s1.js … s5.js
  grafana/dashboard.json
  tools/Perf.sln
  tools/PerfAgent/
  tools/PerfRunner/
  tools/PerfStream/
  tools/Perf.UnitTests/
```

- The tools are .NET, use only packages already in `Directory.Packages.props`, and stay **outside `Senswave.sln`**.
- Their names don't start with `Senswave`, because `ModuleLoader` loads every `Senswave*.dll`.
- The existing `performance/k6/legal-terms.js` becomes `s4.js`, built on the shared `lib/`.

### PerfAgent (VM)

An ASP.NET minimal API over TLS. Every call needs `Authorization: Bearer <AGENT_TOKEN>`. Limits: vCPU 0–1, `cpus: 0.25`, `128m`.

| Endpoint | Behaviour |
|---|---|
| `GET /status` | which API is running and whether it's healthy; enforced limits for every container (`cpuset`, `cpus`, `mem_limit`); image digest; git SHA label |
| `POST /activate` `{transport, otel, runId}` | stop all three APIs → `DROP DATABASE IF EXISTS senswave_run WITH (FORCE)` → `CREATE DATABASE senswave_run TEMPLATE senswave_template` → recreate and start the target with `Diagnostics__OpenTelemetry__Enabled`, `OTEL_RESOURCE_ATTRIBUTES=run_id=<runId>` → wait until healthy |
| `POST /stop` | stop all APIs |
| `POST /template` `{source}` | stop all APIs, create `senswave_template` from a seeded database |
| `GET /logs/{transport}?since=` | log tail of the API container |

- **Containers are switched** with `docker compose -f stack.yml up -d --force-recreate --no-deps api-<t>`. Recreating the container is the only way to change its environment. The agent image therefore contains the official Docker CLI and the Compose plugin (copied from `docker:cli`), and mounts `/var/run/docker.sock` and the stack directory.
- **DB access** goes through Npgsql. The admin connection string comes from `.env`.
- `activate` is serialized: one at a time, and a second call gets `409`.

### PerfRunner (load side)

A .NET console app. Its image contains the k6 binary and PerfStream. Commands:

- `prepare`:
  1. activate `api-rest` against an empty `senswave_seed`
  2. run the existing Seed tool (100 generated users, a shared broker, homes, devices, dashboards)
  3. call `POST /template`
- `run <campaign.yaml>`: runs the campaign.
- `export <campaign>`: repeats the server export for runs that are missing it.
- `archive <campaign>`: zips `results/<campaign>` and uploads it to Azure Blob storage (connection string in `.env`).

Campaign file:

```yaml
target: { api: https://perf.example.com:8443, agent: https://perf.example.com:9443 }
rounds: 3
transports: [Rest, Grpc, GrpcWeb]   # shuffled per round, seed recorded
scenarios: [S1, S2, S3, S4, S5, S6a, S6b]
rates: [100, 500, 1000]
otel: [on]                           # [on, off] for overhead control runs
warmup: 30s
duration: 5m
export: { traces: true, rawK6: false }
```

Each run goes through these steps:

1. `activate`.
2. **Verification.** Fail fast, no measurement.
   - `/status` reports the expected transport, healthy, and the expected limits.
   - Log in as the seeded users **over the transport under test** in REST and gRPC alike, then look up each user's device, dashboard and tile IDs and write them to a data file for k6 and PerfStream.
   - One successful call per scenario. For S5/S6 also check the broker: a test message arrives over a subscription.
3. Warm-up for 30 s, then measure for 5 min. Both windows are recorded in UTC.
4. **Validity gates:** `dropped_iterations == 0`, error rate below 1%, and load-generator CPU below 80% on average. A run that fails a gate is repeated once, then marked `invalid` and the campaign continues.
5. **Export** (see "Data export").

k6 streams to Grafana Cloud through its built-in Prometheus remote-write output, tagged `run_id`, `transport`, `scenario`, `rate`.

### PerfStream (S6a, S6b)

A .NET console app started by the runner instead of k6.

- **Subscribers:** one per seeded user, using the real clients: SignalR (`Microsoft.AspNetCore.SignalR.Client`, WebSockets over HTTP/2), gRPC (`Grpc.Net.Client`), gRPC-Web (`Grpc.Net.Client.Web`).
- **Publisher:**
  - S6a: MQTT (`MQTTnet`) to the external broker, as the device.
  - S6b: tile action calls through the API over the transport under test.
- **Every event carries its send timestamp.** PerfStream records delivery latency (p50/p95/p99/max), lost events and connection setup time, written to `stream-summary.json` in the same shape as the k6 summary.
- **Clocks:** both sides use the load machine's clock in S6b. In S6a the publisher and the subscriber are the same process, so there's no clock skew.

### k6 scripts

- `k6/lib/` holds the three clients: REST, native gRPC, and gRPC-Web hand-framed as `application/grpc-web+proto` with the trailer frame parsed.
- It also builds the options: `constant-arrival-rate`, `preAllocatedVUs` sized from the rate, and `summaryTrendStats: [p(50), p(90), p(95), p(99), max]`.
- `insecureSkipTLSVerify` is set only when the local self-signed certificate is in use.

## Target stack

`stack/compose.yml`:

| Service | cpuset | Limits | Notes |
|---|---|---|---|
| `api-rest`, `api-grpc`, `api-grpcweb` | `2-3` | `cpus: 2`, `mem_limit: 4g`, `memswap_limit: 4g`, `pids_limit: 512` | Same image; `restart: "no"`; HTTPS 8443 with HTTP/1.1 + HTTP/2; `RateLimiter__Enabled=false`, `MessageBus__Type=InMemory`; DB `senswave_run`; trace sampling `0.01`, metric export `10` s; `OTEL_SERVICE_NAME` = `senswave-api-rest`, `senswave-api-grpc`, `senswave-api-grpcweb` |
| `perf-agent` | `0-1` | `cpus: 0.25`, `128m` | Docker socket and stack directory mounted |
| `alloy` | `0-1` | `cpus: 1`, `1g` | — |

- All APIs publish the same host port, which is possible because only one runs at a time.
- The ASP.NET Core histogram buckets for `http.server.request.duration` get finer boundaries in the perf configuration, so p99 has real resolution. This uses the view configuration in `DiagnosticsExtensions` and is enabled by configuration only.

### Grafana Alloy

| Source | Component | Destination |
|---|---|---|
| App OTLP (metrics, traces) | `otelcol.receiver.otlp` → `otelcol.processor.batch` | Grafana Cloud OTLP |
| Container CPU, memory, network, OOM kills, restarts | `prometheus.exporter.cadvisor` (Docker only) | Prometheus remote write |
| Host CPU (including steal), memory, disk, network | `prometheus.exporter.unix` | Prometheus remote write |

- No logs are shipped. Container logs are fetched through the agent and stored with failed runs only.
- External labels: `env` (`local` | `azure`). Container metrics get `transport` from a container label.

### TLS

- **Azure:** a Let's Encrypt certificate for the Cloudflare DNS name, obtained with a DNS-01 challenge through the Cloudflare API, so no proxy is needed.
- **Local:** the self-signed certificate from `docker/generate-cert.sh`.

## Data export and thesis pipeline

Grafana is for live monitoring. Thesis data comes from files.

After every run, PerfRunner queries Grafana Cloud's Prometheus API (`/api/v1/query_range`, with a read-only token) for that run's **measurement window**. It writes `results/<campaign>/<run>/server/`:

| File | Content |
|---|---|
| `latency.csv` | p50/p95/p99 from `histogram_quantile(…, sum by (le)(increase(http_server_request_duration_seconds_bucket{run_id=…}[window])))`, computed once over the whole window, so percentiles are never averaged |
| `throughput.csv` | req/s and error rate over time |
| `cpu.csv`, `memory.csv` | container CPU seconds and working set (cAdvisor), process CPU (OTel) |
| `gc.csv` | allocated bytes, GC counts, ThreadPool queue length |
| `network.csv` | container rx/tx bytes |
| `connections.csv` | `kestrel.active_connections`, `kestrel.tls_handshake.duration` |
| `traces.jsonl` | optional: up to 500 traces from the Tempo API for the run window |

Next to it:
- `meta.json`: SHA, image digest, transport, scenario, rate, round, OTel state, shuffle seed, limits from `/status`, UTC windows, gate results
- `k6-summary.json` or `stream-summary.json`
- optionally `k6-raw.csv.gz`
- `agent-logs.txt` for failed runs

### Client vs server pairs

| Aspect | Client | Server | Derived |
|---|---|---|---|
| Latency | `http_req_duration` / `grpc_req_duration` | `http.server.request.duration` | network + client share = client − server, per percentile |
| Time to first byte | `http_req_waiting` (REST, gRPC-Web) | same histogram | approximate RTT + queueing |
| Throughput | `http_reqs`, `iterations` | request count | lost or queued requests |
| Errors | `http_req_failed`, `checks` | `error.type`, gRPC status | consistency check |
| Bytes | `data_sent`, `data_received` | cAdvisor rx/tx | bytes per request, including protocol and TLS |
| Connections | `http_req_connecting`, `http_req_tls_handshaking` | Kestrel connection metrics | connection reuse |

Native gRPC in k6 reports only total duration, so cross-transport comparisons use total duration. The breakdown is shown for REST and gRPC-Web only.

### Traces

Headline numbers come from metrics, which are never sampled. Traces (1% sampling, about 3000 per run at 1000/s) are used for a **span breakdown**: server span → Mediator handler → EF Core/Npgsql. The rest is transport and serialization. The breakdown is reported for S2 and S3 at 1000/s. If the Tempo export proves impractical, DB time comes from Npgsql's `db.client.operation.duration` metric instead.

### Backup

- **After every run:** the export runs automatically.
- **After every session:** `archive` uploads to Azure Blob. Keep a second copy elsewhere. Commit the aggregated CSV to the thesis repo.
- **Once, at the end:** export `grafana/dashboard.json`.

### Thesis side (phase 3, in `studies-masters-thesis`)

```
data/aggregated/*.csv      ← committed
scripts/analyze.py         ← results/ → aggregated CSV (median of 3 rounds, min–max)
scripts/plots.py           ← CSV → tex/img/results/*.pdf (matplotlib, vector, thesis fonts)
tex/tables/*.tex           ← generated booktabs tables
```

Charts:
1. Latency vs rate per scenario, p50 and p99, client next to server.
2. CPU-ms per request vs rate.
3. Allocated bytes per request and working set.
4. Bytes on the wire per request, with a payload size table.
5. Latency CDF at 1000/s for S2 and S3.
6. S6a/S6b delivery latency vs events/s, and RAM per subscription.
7. Table: OTel on vs off overhead.
8. Stacked bar: server time + network/client = client latency.
9. Appendix: one run's time series.

A summary table per scenario.

## Testing the platform

- **Unit tests** in `tools/Perf.UnitTests` (xUnit), covering:
  - campaign expansion and shuffling
  - validity gates
  - measurement windows in `meta.json`
  - export query building
  - the agent's activation order, with the Docker and DB calls behind interfaces
- **Acceptance:** `runner/campaigns/smoke.yaml` (1 round, rate 100, 30 s, all scenarios × transports) passes verification and the gates on the local stack. Phase 2 does not start before this.
- `dotnet build Senswave.sln` and the architecture tests are unaffected.

## README

`performance/README.md` replaces the current one:
1. Overview: diagram, what a run is, the scenario table, rates.
2. Local quick start: certificate, `.env`, `local/compose.yml`, `prepare`, smoke campaign.
3. Azure setup: VM, Flexible Server, network, Cloudflare DNS-only record, Let's Encrypt DNS-01, `.env`, `prepare`. Then **Practical points (don't forget):**
   - VM **D4as_v5**; never B-series (burstable CPU credits)
   - check the regional vCPU quota for the D-series family before creating the VM
   - VM and Flexible Server in the **same region and availability zone**; Flexible Server on **General Purpose** (D2ds_v5 or larger), never Burstable
   - accelerated networking on the VM's network interface
   - check the hyperthread sibling pairs with `lscpu -e` and adjust the `cpuset` values so the API owns a full physical core pair
   - **deallocate** the VM and **stop** Flexible Server after each session; Flexible Server restarts automatically after 7 days
   - outbound traffic from Azure is billed per GB; check the estimate before a full campaign
   - record the git SHA, use the same campaign file and the same time of day for every session
   - run `archive` before deallocating
4. Running campaigns: campaign file reference, results layout, gates, handling invalid runs.
5. Monitoring: the Alloy pipeline, labels, dashboard import.
6. **Implementation problems and solutions**, updated during implementation. Known so far:
   - k6 has no gRPC-Web client, so requests are hand-framed.
   - k6 can't read responses incrementally and its WebSockets are HTTP/1.1 only, hence PerfStream.
   - S5 needs a reachable MQTT broker.
   - `ModuleLoader` loads `Senswave*.dll`, hence the tool names.
   - Changing an env var requires recreating the container, hence the Docker CLI in the agent.
   - A database can't be dropped while it has open connections, so the APIs are stopped first and `WITH (FORCE)` is used.
   - TLS without the Cloudflare proxy, via DNS-01.

## Delivery

One branch, `perf/platform`, one commit per step:
1. Stack, PerfAgent, local compose.
2. PerfRunner, k6 S1–S5, verification, gates.
3. PerfStream S6a/S6b.
4. Alloy, Grafana dashboard, data export.
5. README and Azure guide.

- **Phase 1** ends when the local smoke campaign passes.
- **Phase 2** is the Azure environment.
- **Phase 3** is the thesis analysis scripts.

## Open points to verify in phase 1

- That each REST endpoint and its rpc in the scenario table return equivalent data (same DTO fields, same query parameters such as `size=30`).
- Whether `OTEL_RESOURCE_ATTRIBUTES` reaches the resource with the current `ConfigureResource` setup in `Program.cs` / `DiagnosticsExtensions`.
- Whether `CREATE DATABASE … TEMPLATE` works with the Flexible Server admin role. Fallback: `pg_dump` / `pg_restore`.
- Which process holds the MQTT client connections with `MessageBus=InMemory` (the API references `DataSources.BrokerConnection`), and that S5 succeeds without the DataSource worker.
- Whether Kestrel accepts WebSockets over HTTP/2 for the SignalR hub with the current configuration.
