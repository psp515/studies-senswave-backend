# Performance test platform

Measures REST, gRPC and gRPC-Web **from the server's point of view**, under identical conditions, with results that can be reproduced and fed into the thesis. Design: [`docs/specs/2026-10-31-performance-platform-design.md`](../docs/specs/2026-10-31-performance-platform-design.md).

## Contents

1. [Overview](#1-overview)
2. [Local quick start](#2-local-quick-start)
3. [Azure setup](#3-azure-setup)
4. [Practical points (don't forget)](#4-practical-points-dont-forget)
5. [Running campaigns](#5-running-campaigns)
6. [Monitoring](#6-monitoring)
7. [Implementation problems and solutions](#7-implementation-problems-and-solutions)

## 1. Overview

- One API instance is under load at a time, the other two are stopped. They are switched by PerfAgent.
- Every run starts from the same database state (`senswave_template` copied to `senswave_run`).
- Server telemetry (OpenTelemetry, container and host metrics) goes through Grafana Alloy to Grafana Cloud and is exported to local files after each run.
- Client metrics (k6, PerfStream) are paired with server metrics for the same time window.
- All three transports run over **HTTP/2 over TLS**.

Out of scope: measurements on the Android device, horizontal scaling, staircase or ramp load models.

### Architecture

```
LOAD SIDE (local PC or 2-vCPU box) ─ docker compose: runner
 ┌──────────────────────────────────────────────┐
 │ PerfRunner (.NET) + k6 binary + PerfStream    │
 │  campaign.json → for round, transport, scen., │
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

The VM's public name is a Cloudflare record in **DNS-only** mode (no proxy). The agent and the API each have their own TLS port (`AGENT_PORT` 9443, `API_PORT` 8443). The local phase uses the same layout on one machine: the `database` (Postgres) and `mqtt` (Mosquitto) containers stand in for Flexible Server and the external broker.

| Component | Where | What it does |
|---|---|---|
| `api-rest`, `api-grpc`, `api-grpcweb` | VM (`stack/compose.yml`) | Same image, `Transport__Mode` differs. Created once, started one at a time. |
| PerfAgent (`Senswave.Perf.Agent`) | VM | Bearer-token API: `/status`, `/activate`, `/stop`, `/prepare`, `/logs/{transport}`. Recreates containers through the Docker Engine API and resets the database. |
| Grafana Alloy (`Senswave.Perf.Alloy`) | VM | Ships OTLP, container and host metrics to Grafana Cloud. |
| PerfRunner | load side (`runner/compose.yml`) | Commands `prepare`, `run`, `export`, `archive`. Image contains k6 and PerfStream. |
| k6 (`k6/s1.js` ... `s5.js`) | load side | S1-S5 with `constant-arrival-rate`. |
| PerfStream | load side | S6a and S6b: real SignalR / gRPC / gRPC-Web subscribers plus a publisher. |

### One run

**reset → verification → warm-up → measurement → gates**

1. **Reset (`activate`).** Stop all APIs, drop and recreate `senswave_run` from `senswave_template`, recreate and start the target API (with `Diagnostics__OpenTelemetry__Enabled` and `OTEL_RESOURCE_ATTRIBUTES=run_id=<runId>`), wait until healthy.
2. **Verification.** Fail fast, no measurement. `/status` must report the expected transport, healthy; log in as the seeded users over the transport under test, discover device, dashboard and tile IDs; one successful call per scenario. For S5/S6 a test message must also arrive through the broker.
3. **Warm-up** (30 s, excluded; smoke: 10 s).
4. **Measurement** (5 min; smoke: 30 s). Both windows are recorded in UTC.
5. **Validity gates:** `dropped_iterations == 0`, error rate below 1%, load-generator CPU below 80% on average.
6. **Export** of the server metrics for the measurement window (automatic at the end of `run`).

### Scenarios and load

| Id | Name | REST | gRPC rpc |
|---|---|---|---|
| S1 | Flat data | `GET /api/v1/users` | `senswave.users.v1.UsersService/GetUser` |
| S2 | List of objects | `GET /api/v1/devices/operations/display?deviceId=…&size=30` | `senswave.devices.v1.OperationsService/DisplayOperations` |
| S3 | Nested list | `GET /api/v1/devices/dashboards/{id}/display` | `senswave.devices.v1.DashboardsService/DisplayDashboard` |
| S4 | String | `GET /api/v1/legal/terms` | `senswave.users.v1.LegalService/GetTermsAndConditions` |
| S5 | Write command | `POST /api/v1/devices/{id}/tile/action` | `senswave.devices.v1.DevicesService/DeviceTileAction` |
| S6a | Stream, device path | SignalR (WebSocket over HTTP/2) | `senswave.liveupdates.v1.LiveUpdatesService/Subscribe` |
| S6b | Stream, user-action path | SignalR (WebSocket over HTTP/2) | `senswave.liveupdates.v1.LiveUpdatesService/Subscribe` |

The rpc names come from `src/Shared/Senswave.Contracts/Protos`.

- **Load model:** open model, k6 `constant-arrival-rate`. Each scenario runs **three separate runs at 100, 500 and 1000 per second**.
- For S1-S5 the rate is requests per second.
- For S6a and S6b the rate is **events published per second**. There is one subscriber per seeded user's device, so delivered events per second equals published events per second.
  - **S6a:** PerfStream acts as the device and publishes MQTT messages to the external broker. The API receives them and LiveUpdate pushes them to subscribers.
  - **S6b:** PerfStream calls the tile action (S5) with `SendEvents` on, and the API broadcasts to subscribers. The publishing requests also load the API, which is part of what S6b measures.
- **Campaign:** rounds × transports (order shuffled per round) × scenarios × rates × OTel on/off.

### Layout

```
performance/
  stack/compose.yml            # VM: 3 APIs, seed (profile), agent, alloy
  stack/alloy/config.alloy
  stack/.env.template
  local/compose.yml            # includes stack/, adds postgres + mosquitto
  local/mosquitto/             # mosquitto.conf, generate-cert.sh
  runner/compose.yml           # load side: PerfRunner image
  runner/.env.template
  runner/campaigns/            # smoke.json, full.json
  k6/lib/                      # rest, grpc, grpc-web clients, options builder
  k6/s1.js ... s5.js
  grafana/dashboard.json
  tools/Perf.sln               # PerfAgent, PerfRunner, PerfStream, Perf.Core, Perf.Client, Perf.UnitTests
  results/                     # git-ignored
```

The tools stay outside `Senswave.sln`, and their names do not start with `Senswave` (see [section 7](#7-implementation-problems-and-solutions)).

## 2. Local quick start

Requirements: Docker with Compose v2, `openssl`, a POSIX shell (Git Bash on Windows). All commands run from the repository root.

**1. Certificates.** The API certificate (SAN `api`, `localhost`) and the Mosquitto stand-in certificate (SAN `mqtt`, `localhost`) are both written to `docker/https`. Both are valid 365 days.

```bash
sh docker/generate-cert.sh
```

```bash
sh performance/local/mosquitto/generate-cert.sh
```

**2. Environment files.** Copy both templates first: `POSTGRES_*` and `SEED_BROKER_*` have no defaults.

```bash
cp performance/stack/.env.template performance/stack/.env
```

```bash
cp performance/runner/.env.template performance/runner/.env
```

Edit both files:

- `AGENT_TOKEN` in `performance/stack/.env` and `performance/runner/.env`: the same random string of at least 16 characters.
- The local defaults in `stack/.env.template` already match the local stack: `POSTGRES_HOST=database`, `SEED_BROKER_URL=mqtt`, `SEED_BROKER_PORT=8883`, `API_SSL_CERT_FILE=/https/mqtt.crt`, `SEED_SSL_CERT_FILE=/https/senswave.api.crt`.
- Machines with fewer than 4 vCPUs: set `API_CPUSET=0-1` and `SIDE_CPUSET=0-1`. The Grafana Cloud variables can stay empty locally.

**3. Create all containers (nothing starts).** Only `api-rest` has a `build` section, so this builds the API image once and also creates the grpc, grpcweb, seed, agent and alloy containers.

```bash
docker compose -f performance/local/compose.yml --env-file performance/stack/.env --profile seed up --no-start --build
```

**4. Start the long-running services.** The API containers are not started here; PerfAgent starts one per run.

```bash
docker compose -f performance/local/compose.yml --env-file performance/stack/.env up -d database mqtt perf-agent alloy
```

**5. Prepare the data.** Seeds `senswave_seed` (100 users, one shared broker, homes, devices, dashboards; about 70 s) and creates `senswave_template` from it.

```bash
docker compose -f performance/runner/compose.yml run --rm --build runner prepare runner/campaigns/smoke.json
```

**6. Smoke campaign.** 1 round, rate 100, 10 s warm-up, 30 s measurement, every scenario on every transport.

```bash
docker compose -f performance/runner/compose.yml run --rm runner run runner/campaigns/smoke.json
```

Without Grafana Cloud read credentials the run ends with `GRAFANA_PROM_URL / GRAFANA_PROM_USER / GRAFANA_READ_TOKEN not all set; skipping server export`. That is expected locally; client-side results are complete.

**Where results appear.** `performance/results/smoke-<yyyyMMdd-HHmm>/<runId>-a<attempt>/` on the host (see [Results layout](#results-layout)). Smoke results are an acceptance check, not measurements: the first run after an activation is cold.

Notes for the local setup:

- Git Bash rewrites absolute container paths (`/work/...`). Prefix such commands with `MSYS_NO_PATHCONV=1`.
- `*.sh` files are LF through `.gitattributes`. An older checkout may need `git add --renormalize .` or a re-checkout of `docker/generate-cert.sh`.
- Docker Desktop's cAdvisor exposes only the root cgroup, so the container panels are empty locally (section 7).

## 3. Azure setup

Phase 2 of the platform. The VM hosts the stack, Flexible Server holds the databases, and the load side stays on your machine or a separate 2-vCPU box. Read [section 4](#4-practical-points-dont-forget) before creating anything.

> **Untested commands.** The `az`, NSG and certbot commands below were written from documentation and were **not run end to end**. Check flags with `az <command> --help` before use. In particular check: (1) the **Standard** public IP together with `az vm create --zone`: the IP may need to be zonal or zone-redundant to match the VM; (2) `az postgres flexible-server create --vnet/--subnet`: it auto-creates a private DNS zone and delegates the subnet, so confirm the zone is linked to the VNet and that the server host resolves from the VM (`nslookup <server-name>.postgres.database.azure.com`).

Examples use `westeurope`, resource group `rg-senswave-perf`, DNS name `perf.example.com`. Replace them.

### 3.1 Resource group and network

```bash
az group create --name rg-senswave-perf --location westeurope
```

```bash
az network vnet create --resource-group rg-senswave-perf --name vnet-perf --address-prefixes 10.20.0.0/16 --subnet-name snet-vm --subnet-prefixes 10.20.1.0/24
```

Flexible Server with private access needs its own delegated subnet:

```bash
az network vnet subnet create --resource-group rg-senswave-perf --vnet-name vnet-perf --name snet-db --address-prefixes 10.20.2.0/24 --delegations Microsoft.DBforPostgreSQL/flexibleServers
```

### 3.2 VM

Check the regional vCPU quota for the **Dasv5** family first (Subscription > Usage + quotas), otherwise creation fails.

```bash
az network public-ip create --resource-group rg-senswave-perf --name pip-perf --sku Standard --allocation-method Static
```

```bash
az network nsg create --resource-group rg-senswave-perf --name nsg-perf
```

Allow SSH from your IP, and the API and agent ports from the load machine's IP only (`API_PORT` 8443, `AGENT_PORT` 9443). Replace `<your-ip>` and `<load-ip>`.

```bash
az network nsg rule create --resource-group rg-senswave-perf --nsg-name nsg-perf --name ssh --priority 100 --access Allow --protocol Tcp --destination-port-ranges 22 --source-address-prefixes <your-ip>
```

```bash
az network nsg rule create --resource-group rg-senswave-perf --nsg-name nsg-perf --name perf-ports --priority 110 --access Allow --protocol Tcp --destination-port-ranges 8443 9443 --source-address-prefixes <load-ip>
```

Create the VM: **Standard_D4as_v5**, Ubuntu 24.04, **accelerated networking on**, the static public IP, and a zone you will reuse for Flexible Server (zone `1` below).

```bash
az vm create --resource-group rg-senswave-perf --name vm-perf --image Canonical:ubuntu-24_04-lts:server:latest --size Standard_D4as_v5 --zone 1 --vnet-name vnet-perf --subnet snet-vm --public-ip-address pip-perf --nsg nsg-perf --accelerated-networking true --admin-username azureuser --generate-ssh-keys
```

### 3.3 PostgreSQL Flexible Server

**General Purpose D2ds_v5**, PostgreSQL 17, the **same region and the same availability zone** as the VM, private access in `snet-db`. Pick a strong admin password.

```bash
az postgres flexible-server create --resource-group rg-senswave-perf --name <server-name> --location westeurope --tier GeneralPurpose --sku-name Standard_D2ds_v5 --version 17 --zone 1 --admin-user senswaveadmin --admin-password <password> --vnet vnet-perf --subnet snet-db --yes
```

The server host is `<server-name>.postgres.database.azure.com` (Azure resolves it inside the VNet through a private DNS zone it creates). The agent's admin role needs `CREATE DATABASE ... TEMPLATE`; the server admin has it. This was not verified against Flexible Server during implementation (section 7); check it at the first `prepare`.

### 3.4 Docker and the repository

SSH to the VM and install Docker (Compose v2 included):

```bash
curl -fsSL https://get.docker.com | sudo sh
```

```bash
sudo usermod -aG docker $USER
```

Log out and back in, then clone the repository at the **commit under test**:

```bash
git clone <repo-url> senswave && cd senswave && git checkout <commit-sha>
```

### 3.5 DNS (Cloudflare, DNS only)

Create an **A record** `perf.example.com` pointing to the VM's static public IP, with the proxy **off** (grey cloud, "DNS only"). Proxying would break HTTP/2 end to end and the non-HTTP agent behaviour, and add a hop to every measurement.

### 3.6 TLS certificate (Let's Encrypt, DNS-01 through Cloudflare)

No port 80 is needed: the DNS-01 challenge proves ownership through the Cloudflare API.

1. In Cloudflare create an **API token** with `Zone / DNS / Edit` limited to the one zone.
2. On the VM store it (replace the value):

```bash
mkdir -p ~/letsencrypt && printf 'dns_cloudflare_api_token = <token>\n' > ~/letsencrypt/cloudflare.ini && chmod 600 ~/letsencrypt/cloudflare.ini
```

3. Obtain the certificate with the certbot `dns-cloudflare` plugin, run as a container:

```bash
docker run --rm -v ~/letsencrypt:/etc/letsencrypt certbot/dns-cloudflare certonly --dns-cloudflare --dns-cloudflare-credentials /etc/letsencrypt/cloudflare.ini -d perf.example.com --agree-tos -m <your-email> --non-interactive
```

4. Convert it to the PFX Kestrel reads. `CERT_DIR` (default `docker/https` in the repository) must contain `senswave.api.pfx`; the password must equal `API_CERTIFICATE_PASSWORD` in `.env`.

```bash
mkdir -p docker/https && sudo openssl pkcs12 -export -inkey ~/letsencrypt/live/perf.example.com/privkey.pem -in ~/letsencrypt/live/perf.example.com/fullchain.pem -out docker/https/senswave.api.pfx -passout pass:<API_CERTIFICATE_PASSWORD>
```

```bash
sudo chmod 644 docker/https/senswave.api.pfx
```

The certificate is valid for 90 days. Renew with `certbot renew` (same image) and redo step 4 if a session starts later than that.

### 3.7 Stack `.env`

```bash
cp performance/stack/.env.template performance/stack/.env
```

Set at least these values in `performance/stack/.env`:

| Variable | Azure value |
|---|---|
| `PERF_API_HOST` | `perf.example.com` (the DNS name; also the in-network alias the seed uses) |
| `PERF_ENV` | `azure` (Alloy label `env`) |
| `GIT_SHA` | the commit under test; a `.env` file does not expand `$(...)`, so export it in the shell before `up --build` (see 3.9) |
| `API_CERTIFICATE_PASSWORD` | the PFX password from 3.6 |
| `SEED_SSL_CERT_FILE` | `/etc/ssl/certs/ca-certificates.crt` (seed trusts the Let's Encrypt chain through the system CAs) |
| `API_SSL_CERT_FILE` | `/etc/ssl/certs/ca-certificates.crt` (broker certificate must be publicly trusted) |
| `POSTGRES_HOST` | `<server-name>.postgres.database.azure.com` |
| `POSTGRES_USER`, `POSTGRES_PASSWORD` | the Flexible Server admin |
| `SEED_BROKER_URL`, `SEED_BROKER_PORT` | the external broker's host and **TLS** port |
| `SEED_BROKER_USERNAME`, `SEED_BROKER_PASSWORD` | broker credentials |
| `AGENT_TOKEN` | random string, at least 16 characters |
| `GRAFANA_CLOUD_*` | from the Grafana Cloud stack (see [section 6](#6-monitoring)) |

`API_SSL_CERT_FILE` and `SEED_SSL_CERT_FILE` **replace** the system CA bundle inside the container. With the local value (`/https/mqtt.crt`) only the Mosquitto stand-in is trusted.

### 3.8 CPU pinning

Check which logical CPUs share a physical core:

```bash
lscpu -e
```

Rows with the same `CORE` value are hyperthread siblings. Set `API_CPUSET` to a **full physical core pair** (both siblings of one or two cores) and `SIDE_CPUSET` to the rest, for example `API_CPUSET=2-3` and `SIDE_CPUSET=0-1` when `0,1` and `2,3` are the sibling pairs. If the siblings are interleaved (`0,2` and `1,3`), use `API_CPUSET=1,3`, `SIDE_CPUSET=0,2`.

### 3.9 Create and start

Export the commit SHA first. It becomes the `senswave.git-sha` image label, which is **baked at build time**, so it must be set when `--build` runs (an exported variable overrides `.env`):

```bash
export GIT_SHA=$(git rev-parse HEAD)
```

Create every container without starting any (builds the API image from the checked-out commit):

```bash
docker compose -f performance/stack/compose.yml --env-file performance/stack/.env --profile seed up --no-start --build
```

Start the agent and Alloy; PerfAgent starts exactly one API per run:

```bash
docker compose -f performance/stack/compose.yml --env-file performance/stack/.env up -d perf-agent alloy
```

### 3.10 Load side and `prepare`

On the load machine copy `performance/runner/.env.template` to `performance/runner/.env` and set `AGENT_TOKEN` (same as the VM), `MQTT_HOST`/`MQTT_PORT`/`MQTT_USERNAME`/`MQTT_PASSWORD`/`MQTT_TLS` for the S6a publisher (the external broker), the `GRAFANA_*` read settings, and `ARCHIVE_SAS_URL`. Make a campaign copy or edit `runner/campaigns/full.json`: set `target.api` / `target.agent` to `https://perf.example.com:8443` / `:9443` and `insecureTls` to `false`.

```bash
docker compose -f performance/runner/compose.yml run --rm --build runner prepare runner/campaigns/full.json
```

`prepare` is slow (seeding); repeat it only when the data model or the commit changes.

Before the first campaign, verify that the Alloy logs (`docker logs Senswave.Perf.Alloy`) show no export errors. `GRAFANA_CLOUD_OTLP_ENDPOINT` and `GRAFANA_CLOUD_PROM_URL` have no fallback in `stack/compose.yml`, so a forgotten value fails loudly; only the local compose supplies placeholders (`local/alloy-defaults.env`).

Then verify the Grafana Cloud metric names once ([section 6](#6-monitoring)), run the smoke campaign against Azure (`runner/campaigns/smoke.json` with the target edited), and only then the full campaign.

## 4. Practical points (don't forget)

- VM **D4as_v5**; never B-series (burstable CPU credits)
- check the regional vCPU quota for the D-series family before creating the VM
- VM and Flexible Server in the **same region and availability zone**; Flexible Server on **General Purpose** (D2ds_v5 or larger), never Burstable
- accelerated networking on the VM's network interface
- check the hyperthread sibling pairs with `lscpu -e` and adjust the `cpuset` values so the API owns a full physical core pair
- **deallocate** the VM and **stop** Flexible Server after each session; Flexible Server restarts automatically after 7 days
- outbound traffic from Azure is billed per GB; check the estimate before a full campaign
- record the git SHA, use the same campaign file and the same time of day for every session
- run `archive` before deallocating

Deallocate and stop (after `archive`):

```bash
az vm deallocate --resource-group rg-senswave-perf --name vm-perf
```

```bash
az postgres flexible-server stop --resource-group rg-senswave-perf --name <server-name>
```

## 5. Running campaigns

All runner commands: `docker compose -f performance/runner/compose.yml run --rm runner <command>`. The container's working directory is `/work/performance`; campaign files are mounted from `performance/runner/campaigns`, results are written to `performance/results` on the host.

| Command | Does |
|---|---|
| `prepare <campaign.json>` | Calls the agent's `POST /prepare`: recreate empty `senswave_seed`, start `api-rest` on it, run the Seed tool, wait for `Initial seed finished`, stop, then create `senswave_template` from `senswave_seed`. |
| `run <campaign.json>` | Runs the campaign, then exports the server metrics. |
| `export <resultsDir> [--traces]` | Repeats the server export for the runs of one results directory. Overwrites existing files. |
| `archive <resultsDir>` | Zips the results directory and uploads it with one `PUT` to `ARCHIVE_SAS_URL`. |

Exit codes of `run`: `0` ok, `3` the server export failed (data is safe; run `export` again), `130` cancelled with Ctrl+C (the API under test is stopped), `2` usage error.

### Campaign file

JSON, read with `System.Text.Json` (`runner/campaigns/smoke.json`, `full.json`).

| Field | Type | Meaning |
|---|---|---|
| `target.api` | URL | API base URL (`https://<host>:8443`). |
| `target.agent` | URL | PerfAgent base URL (`https://<host>:9443`). |
| `target.insecureTls` | bool | Skip certificate validation (local self-signed certificate only). |
| `rounds` | int > 0 | How many times the whole matrix repeats. |
| `transports` | `Rest`, `Grpc`, `GrpcWeb` | Transports under test. Order is shuffled per round; the shuffle seed is recorded in `meta.json` and `campaign.json`. |
| `scenarios` | `S1` ... `S5`, `S6a`, `S6b` | Scenarios to run. |
| `rates` | int[] | Requests per second (S1-S5) or published events per second (S6). |
| `otel` | bool[] | `[true]` OpenTelemetry on; `[true, false]` adds the overhead control runs. |
| `warmupSeconds` | int >= 0 | Warm-up, excluded from the measurement. |
| `durationSeconds` | int > 0 | Measurement window. |
| `users.emailPattern` | string | Must contain `{0}`, e.g. `user{0}@gmail.com`. |
| `users.firstIndex`, `users.count` | int | Index of the first seeded user and number of users (and subscribers). |
| `users.password` | string | Seeded users' password (a test value). |
| `export.traces` | bool | Also export up to 500 traces from Tempo. |
| `export.rawK6` | bool | Also write `k6-raw.csv.gz` per k6 run. |

Run IDs have the form `r<round>-<transport>-<scenario>-<rate>-<otel|nootel>`, e.g. `r1-grpc-s2-500-otel`.

### Results layout

```
results/<campaign-file>-<yyyyMMdd-HHmm>/
  campaign.json                  # expanded campaign, shuffle seed, list of run ids
  <runId>-a<attempt>/            # one directory per attempt (a1, a2)
    meta.json
    run-data.json
    verify-summary.json
    warmup-summary.json
    k6-summary.json              # S1-S5
    stream-summary.json          # S6a, S6b
    k6-raw.csv.gz                # only with export.rawK6
    agent-logs.txt               # only for runs that are not valid
    server/                      # valid runs, written by export
      latency.csv  throughput.csv  cpu.csv  memory.csv
      gc.csv  network.csv  connections.csv
      traces.jsonl               # only with export.traces
```

| File | Content |
|---|---|
| `campaign.json` | The campaign, shuffle seed and run IDs. Contains the seeded test password (a test value). |
| `meta.json` | Run id, attempt, round, transport, scenario, rate, OTel, shuffle seed, git SHA, image ID, container limits from `/status`, UTC warm-up start / measurement start / measurement end, `status`, `failures`. |
| `run-data.json` | Per-user IDs discovered at the start of the attempt. Access tokens are blanked after the attempt. |
| `verify-summary.json`, `warmup-summary.json` | k6 summaries of the verification and warm-up phases. |
| `k6-summary.json` | k6 `--summary-export` of the measurement: p50/p90/p95/p99/max, `http_reqs`, `dropped_iterations`, checks, data sent/received. |
| `stream-summary.json` | PerfStream: delivery latency p50/p95/p99/max, published, delivered, lost (still pending 5 s after the run), connection setup time. Same shape as the k6 summary. |
| `k6-raw.csv.gz` | Raw k6 samples. |
| `agent-logs.txt` | API container log, last 2000 lines only (the start of the attempt can be cut). |
| `server/latency.csv` | p50/p95/p99 computed once over the whole measurement window from `http_server_request_duration_seconds_bucket`, so percentiles are never averaged. |
| `server/throughput.csv` | Requests per second, by `error_type` and by `http_response_status_code`. |
| `server/cpu.csv`, `memory.csv` | Container CPU and working set (cAdvisor), process CPU (OTel). |
| `server/gc.csv` | Allocated bytes, GC counts, ThreadPool queue length. |
| `server/network.csv` | Container rx/tx bytes. |
| `server/connections.csv` | `kestrel.active_connections`, TLS handshake duration. |
| `server/traces.jsonl` | Up to 500 traces for the window (optional). |

Client / server pairs for the thesis:

| Aspect | Client | Server | Derived |
|---|---|---|---|
| Latency | `http_req_duration` / `grpc_req_duration` | `http.server.request.duration` | network + client share = client − server, per percentile |
| Time to first byte | `http_req_waiting` (REST, gRPC-Web) | same histogram | approximate RTT + queueing |
| Throughput | `http_reqs`, `iterations` | request count | lost or queued requests |
| Errors | `http_req_failed`, `checks` | `error.type`, gRPC status | consistency check |
| Bytes | `data_sent`, `data_received` | cAdvisor rx/tx | bytes per request, including protocol and TLS |
| Connections | `http_req_connecting`, `http_req_tls_handshaking` | Kestrel connection metrics | connection reuse |

Native gRPC in k6 reports only total duration, so cross-transport comparisons use total duration; the breakdown applies to REST and gRPC-Web only. Headline numbers come from metrics (never sampled). Traces (1% sampling) are only for the span breakdown of S2 and S3 at 1000/s.

### Run status, gates and invalid runs

| `meta.json` `status` | Meaning | What to do |
|---|---|---|
| `valid` | Verification passed and all gates passed. | Nothing. Only `valid` runs get a server export. |
| `invalid` | Measured, but a gate failed: `dropped_iterations > 0` (load generator could not keep the rate), error rate >= 1%, or average load-generator CPU > 80%. `failures` lists which. | Not used for results. If dropped iterations or CPU, the load side is too small: use a stronger load machine, or lower the rate. If errors, read `agent-logs.txt`. |
| `failed-verification` | Activation unhealthy, wrong transport reported, or a verification call or broker test failed. No measurement was taken. | Read `failures` and `agent-logs.txt`; check broker reachability and credentials, and that `prepare` was run on this commit. |
| `failed` | Exception or a failing measured run. | Same as above. |

Any run that is not `valid` is repeated **once** (attempt `a2`). If it is still not valid, its status stays and the campaign continues. Both attempts stay on disk. A `prepare` can be re-run at any time; a new campaign always creates a new results directory.

Before aggregating, filter to `status == "valid"` and, per run id, take the latest valid attempt.

### Backup routine

- **After every run:** the server export runs automatically at the end of `run` (it waits 60 s for the last metrics to reach Grafana Cloud). If it failed (exit code 3) or credentials were missing:

```bash
docker compose -f performance/runner/compose.yml run --rm runner export results/<campaign-dir>
```

- **After every session, before deallocating:** upload to Azure Blob storage (container SAS URL with write permission in `ARCHIVE_SAS_URL`). Keep a second copy elsewhere.

```bash
docker compose -f performance/runner/compose.yml run --rm runner archive results/<campaign-dir>
```

- **Thesis repo:** commit the aggregated CSV (`data/aggregated/*.csv`) to `studies-masters-thesis`.
- **Once, at the end:** export `grafana/dashboard.json` from Grafana.

## 6. Monitoring

Grafana is for live monitoring; thesis data comes from the exported files.

### Alloy pipeline

`stack/alloy/config.alloy`. No logs are shipped; container logs are fetched through the agent and stored for failed runs only.

| Source | Components | Destination |
|---|---|---|
| App OTLP (metrics, traces) on 4317 / 4318 | `otelcol.receiver.otlp` → `otelcol.processor.transform` (copies `run_id` and `senswave.transport` onto every data point as `run_id` and `transport`) → `otelcol.processor.batch` | Grafana Cloud OTLP (`GRAFANA_CLOUD_OTLP_ENDPOINT`) |
| Container CPU, memory, network, OOM kills, restarts | `prometheus.exporter.cadvisor` (`docker_only`) → `prometheus.scrape` (10 s) → `prometheus.relabel` | Prometheus remote write (`GRAFANA_CLOUD_PROM_URL`) |
| Host CPU (including steal), memory, disk, network | `prometheus.exporter.unix` → `prometheus.scrape` (10 s) → `prometheus.relabel` | Prometheus remote write |

### Labels

| Label | Source |
|---|---|
| `run_id` | `OTEL_RESOURCE_ATTRIBUTES=run_id=<runId>`, set by PerfAgent per activation; promoted to data points by Alloy |
| `transport` | the `senswave.transport` resource attribute, promoted by Alloy; k6 also tags `transport`, `scenario`, `rate` |
| `env` | `PERF_ENV` (`local` or `azure`), added to container and host metrics |

### Grafana Cloud credentials

Create a free Grafana Cloud stack. From its "OpenTelemetry" and "Prometheus" detail pages take the endpoints and user IDs for `GRAFANA_CLOUD_OTLP_*` and `GRAFANA_CLOUD_PROM_*` in `stack/.env`. Create two access policies: a **write** token (`metrics:write`, `traces:write`) for `GRAFANA_CLOUD_WRITE_TOKEN`, and a **read** token (`metrics:read`, `traces:read`) for `GRAFANA_READ_TOKEN` in `runner/.env`. `GRAFANA_PROM_URL` is the query base ending in `/api/prom`; `GRAFANA_TEMPO_URL` ends in `/tempo`.

### First step after configuring Grafana Cloud: verify metric names

The metric names in `tools/Perf.Core/Export/ExportQueries.cs` and in the dashboard, and the promotion of `run_id` / `transport` onto data points, are **not verified** against a live Grafana Cloud (no credentials existed during implementation). Check once, before the first real campaign. Activate `Grpc` with run id `names-check` through the agent, send a few requests (for example a k6 verify run of `s1.js`), wait 60 s, then:

```bash
curl -s -u "$GRAFANA_PROM_USER:$GRAFANA_READ_TOKEN" "$GRAFANA_PROM_URL/api/v1/label/__name__/values" | tr ',' '\n' | grep -E "http_server_request_duration|process_cpu|dotnet_gc|dotnet_thread_pool|kestrel|container_cpu|container_memory_working|container_network"
```

```bash
curl -s -u "$GRAFANA_PROM_USER:$GRAFANA_READ_TOKEN" "$GRAFANA_PROM_URL/api/v1/query" --data-urlencode 'query=count by (run_id, transport) (http_server_request_duration_seconds_count{run_id="names-check"})'
```

Expected: every metric family used in `ExportQueries.cs` is listed, and the second query returns a series with `run_id="names-check"` and `transport="Grpc"`. If names differ, **adjust `ExportQueries.cs` (and its unit test in `tools/Perf.UnitTests`) and the dashboard queries** before measuring. If `transport` is missing, the `senswave.transport` resource attribute is not reaching Alloy.

### Dashboard

`grafana/dashboard.json` (title "Senswave transport comparison", variables `run_id` and `transport`). Panels: server latency p50/p95/p99, requests/s, CPU-ms per request, allocated bytes per request, container CPU, container memory, network bytes/s, host CPU steal.

Import: Grafana > Dashboards > New > Import > Upload JSON file > select `performance/grafana/dashboard.json` > pick the stack's Prometheus data source (`grafanacloud-<stack>-prom`) > Import. Choose `run_id` and `transport` at the top. Container panels need cAdvisor per-container series (Azure VM only).

### Retention warning

Grafana Cloud's free tier keeps metrics only for a limited time (check your plan; 14 days at the time of writing). Do not rely on Grafana as storage: the per-run CSV export and `archive` are the record. Export the data **before** the retention window ends, and re-run `export` for a campaign only while its data still exists.

## 7. Implementation problems and solutions

### Platform design

**Problem:** k6 has no gRPC-Web client.
**Solution:** requests are hand-framed as `application/grpc-web+proto` in `k6/lib/`, and the trailer frame is parsed to read `grpc-status`.

**Problem:** k6 cannot read responses incrementally and its WebSockets are HTTP/1.1 only, so it cannot subscribe to live updates.
**Solution:** PerfStream (S6a, S6b) uses the real SignalR, gRPC and gRPC-Web clients.

**Problem:** S5 and S6 need a reachable MQTT broker.
**Solution:** the stack uses an external broker (Azure) or Mosquitto (local); verification checks that a test message arrives.

**Problem:** `ModuleLoader` loads every `Senswave*.dll` it finds.
**Solution:** tool projects and assemblies are named `Perf*`, never `Senswave*`, and live in `tools/Perf.sln`, outside `Senswave.sln`.

**Problem:** changing an environment variable requires recreating the container, and running `docker compose` inside the agent breaks relative bind-mount paths.
**Solution:** the agent talks to the Docker Engine API (plain `HttpClient` over `/var/run/docker.sock`): it inspects the container, removes it and creates it again from its own `Config`, `HostConfig` and network aliases with the overridden variables. Bind mounts keep the host paths compose resolved. All containers are created once with `up --no-start`.

**Problem:** a database cannot be dropped while it has open connections.
**Solution:** the agent stops all APIs first and uses `DROP DATABASE IF EXISTS senswave_run WITH (FORCE)`.

**Problem:** TLS without the Cloudflare proxy.
**Solution:** Let's Encrypt with the DNS-01 challenge through the Cloudflare API ([3.6](#36-tls-certificate-lets-encrypt-dns-01-through-cloudflare)); the DNS record stays DNS-only.

**Problem:** `Microsoft.AspNetCore.SignalR.Common` 10.0.7, pinned centrally, produces the NU1903 known-vulnerability warning in the tool projects.
**Solution:** not bumped, because of the dependency policy (no incidental version changes). The warning is expected.

**Problem:** the API's MQTT broker client throttles by default (`MilisecondSpanBetweenMessagesOnSameTopic: 100`, `MessageRateLimiter.TokenLimit: 150` per 60 s), which would cap S6 far below 1000 events/s across 100 devices.
**Solution:** `stack/api/appsettings.Production.json` (mounted into every API container) sets `MilisecondSpanBetweenMessagesOnSameTopic` to `0` and `TokenLimit` to `100000`. It also turns off the ASP.NET rate limiter and SMTP, uses the in-memory bus, and sets trace sampling 0.01 and finer latency histogram boundaries.

**Problem:** a pulled image tag may not exist.
**Solution:** the images used are `postgres:17.6`, `eclipse-mosquitto:2.0.21`, `grafana/alloy:v1.10.0`, `grafana/k6:1.3.0`, `mcr.microsoft.com/dotnet/sdk:10.0` and `aspnet:10.0`. No tag had to be changed. The `certbot/dns-cloudflare` image in section 3.6 is a one-off tool outside the repository; use any current tag.

### Findings from the local feasibility phase

**Problem:** the first `prepare` failed with "Initial seed finished with 101 errors" and `Refit.ApiException 400` on `CreateBroker` for every user. The seed hard-codes `UseTls = true`, and `Modules:DataSource:Brokers:TestConnectionOnChange` is true by default, so the API tested the connection with TLS against plain-text Mosquitto on 1883 (`MqttCommunicationException: Connection reset by peer`). The API also validates the broker certificate.
**Solution:** local Mosquitto has a **TLS listener on 8883** next to the plain 1883 (`local/mosquitto/mosquitto.conf`). `local/mosquitto/generate-cert.sh` creates the certificate (SAN `mqtt`, `localhost`, `127.0.0.1`) in `docker/https/mqtt.{crt,key}` (git-ignored, valid 365 days). The local `.env` needs `API_SSL_CERT_FILE=/https/mqtt.crt` and `SEED_BROKER_PORT=8883`. The key is `chmod 644` on purpose: the Mosquitto container user is not the owner. Plain 1883 stays for host tooling and PerfStream.

**Problem:** `API_SSL_CERT_FILE` / `SEED_SSL_CERT_FILE` set `SSL_CERT_FILE`, which **replaces** the system CA bundle.
**Solution:** locally they point to the self-signed certificates. On Azure set both to `/etc/ssl/certs/ca-certificates.crt`, and set `SEED_BROKER_PORT` to the external broker's TLS port.

**Problem:** `POSTGRES_*` and `SEED_BROKER_*` have no defaults, so compose fails to start on an empty environment.
**Solution:** copy both `.env.template` files first. On machines with fewer than 4 vCPUs set `API_CPUSET=0-1` and `SIDE_CPUSET=0-1`.

**Problem:** building all three API services in parallel from the same tag failed.
**Solution:** only `api-rest` has a `build` section; the others reuse the `senswave-perf-api` image. `--profile seed up --no-start --build` creates everything.

**Problem:** the API's per-user MQTT broker clients live in the API process. Seeding creates the broker but no running client, and clients are lost on every reset or restart. Without a client S5 returns 400 `FailedToPublishMessage` / "Broker client does not exist". The DataSource worker is not needed because the API hosts the client.
**Solution:** PerfRunner's discovery step starts the client for every user on each run (`POST /api/v1/datasources/brokers/{brokerId}/clients`, credentials `BROKER_CLIENT_USERNAME` / `BROKER_CLIENT_PASSWORD`, default `perf` / `perf`). A second start returns 500 `ClientIsRunning` over REST, which is treated as success; over gRPC and gRPC-Web the same error arrives only as `Internal`, so the client then checks `GetClientState` and treats a running client as success.

**Problem:** SignalR over HTTP/2 WebSockets: the default .NET client throws `ArgumentException: An HttpMessageInvoker instance must be passed to ConnectAsync when using HTTP/2`, and with `SkipNegotiation` the token is not sent (401).
**Solution:** a custom `HttpConnectionOptions.WebSocketFactory` creates the socket (HTTP 2.0, `RequestVersionExact`), connects with an `HttpMessageInvoker` over `SocketsHttpHandler` and sets `Authorization: Bearer` itself. Kestrel accepts WebSockets over HTTP/2 with the current configuration, no HTTP/1.1 fallback is needed, and S6 over REST therefore runs over HTTP/2 like the other scenarios.

**Problem:** live updates carry only a `device_id`; the Pico state operation produces both a device-tile update and a dashboard widget update, so counting both would double-deliver.
**Solution:** only device-tile updates are counted (gRPC `DeviceTile`, SignalR update type `deviceTileActionUpdate`, key `deviceId`). Delivery is matched FIFO per device. Limitation: after a real loss, later matches for that device are offset. Publishes that fail are retracted from the pending queue.

**Problem:** percentile rank off by one (p=7, n=100 returned 8).
**Solution:** `Percentiles.Of` uses `Math.Ceiling(p * n / 100.0)`; unit test added.

**Problem:** the first run after an activation is cold: multi-second latency, dropped iterations and some publish failures (for example the first `s5` gRPC run: average 2.2 s, 141 dropped iterations; the third run: 34 ms).
**Solution:** keep warm-up at 30 s or more for real runs. Smoke results (10 s warm-up) are not measurements.

**Problem:** on Windows, `Task.Delay` granularity (about 15 ms) bursts PerfStream at 500-1000 events/s.
**Solution:** use a Linux load machine as the reference.

**Problem:** the container panels and `container_*` export queries return nothing locally. Docker Desktop's cAdvisor exposes only the root cgroup (`id="/"`), with no per-container series and no `name` label.
**Solution:** the Azure VM is the reference for container metrics. Alloy itself loads and runs locally (`sys.env(...)` is accepted by v1.10.0; the unix exporter logs one harmless `diskstats` error for the missing `/run/udev/data`).

**Problem:** `ExportQueries.cs` metric names and `run_id` / `transport` promotion are unverified against Grafana Cloud.
**Solution:** run the two `curl` checks in [section 6](#first-step-after-configuring-grafana-cloud-verify-metric-names) and adjust `ExportQueries.cs` if names differ. ASP.NET sets `error_type` only for exceptions, so `throughput.csv` also contains `sum by (http_response_status_code)`.

**Problem:** the runner image cannot run on `dotnet/runtime`: `Senswave.Contracts` needs `Microsoft.AspNetCore.App`.
**Solution:** the runner image is based on `mcr.microsoft.com/dotnet/aspnet:10.0` with the k6 binary copied from `grafana/k6:1.3.0`.

**Problem:** `status` from the agent shows stopped API containers as `exited` with the stale health value `unhealthy` (Docker keeps the last health result).
**Solution:** trust the `active` and `healthy` fields, not the per-container health of stopped containers.

**Problem:** `Seed__Users__1__Email` collides: the static seed user list contains `user1@gmail.com`, the same as generated user 1.
**Solution:** the stack renames the static user to `static.user1@gmail.com`.

**Problem:** results hold sensitive-looking data.
**Solution:** `run-data.json` has access tokens blanked after each attempt, so results and archives carry no tokens. `campaign.json` contains the seeded test password (a test value only).

**Problem:** on Linux the runner container runs as root, so bind-mounted `performance/results` is root-owned.
**Solution:** use `sudo chown -R $USER performance/results` before editing or moving results.

**Problem:** Git Bash converts absolute container paths in arguments.
**Solution:** prefix with `MSYS_NO_PATHCONV=1`. `*.sh` files are LF through `.gitattributes`; renormalize an older checkout with `git add --renormalize .`.

**Problem:** `CREATE DATABASE ... TEMPLATE` with the Flexible Server admin role is unverified (open point of the design).
**Solution:** check at the first Azure `prepare`. Fallback if it is refused: `pg_dump` / `pg_restore` for the reset step (not implemented).

### Findings from the local smoke campaign (2026-11-01, smoke run at commit 05e94ac, Windows + Docker Desktop, 12 logical CPUs)

**Problem:** the agent's `GET /logs/{transport}` ran out of memory (agent limit 128 MB, API log about 15 MB after an S5 run). The agent crashed and restarted, so the runner got "connection refused" / "SSL connection could not be established" for the rest of the campaign.
**Solution:** the agent now asks Docker for the last 2000 lines only (`tail=2000`).

**Problem:** Alloy crash-looped locally ("at least one endpoint must be specified") because `GRAFANA_CLOUD_OTLP_ENDPOINT` / `GRAFANA_CLOUD_PROM_URL` are empty without Grafana Cloud, contrary to "the Grafana Cloud variables can stay empty locally".
**Solution:** `local/alloy-defaults.env` (loaded first by `local/compose.yml`'s `include`; `stack/.env` overrides it) supplies `http://localhost:1...` placeholders. Alloy now starts locally and its exports fail (expected). The Azure `stack/compose.yml` has no fallback, so missing values still fail loudly. Also note: `runner/.env.template` ships `AGENT_TOKEN=change-me-...`; it must equal the value in `stack/.env`, otherwise every agent call is rejected.

**Result:** 21 runs, 18 valid. S5 (write command) was `invalid` on all three transports at 100/s (dropped iterations 5 to 330): during S5 and S6b the API container sits at its 200% CPU limit (the write path runs automations, widget updates and MQTT publish), so latency grows (S5 p50 60-105 ms, S6b delivery p50 13 s on Grpc/Rest). This is consistent with CPU saturation of the 2-vCPU API (`docker stats` sampling); unconfirmed until the Azure re-run. Gates were not loosened. S6b runs are marked `valid` although delivery latency is very high: the gates do not look at stream latency.

**Known issue:** `run` throws an unhandled exception (exit code 139 in the container) if the agent is unreachable at the final `agent.Stop`; per-run results written before that are intact.
