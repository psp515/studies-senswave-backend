# Performance tests

Three instances of the same API image, one per transport, for load testing with [k6](https://k6.io).

| Container | `Transport__Mode` | Host port (default) | Database |
|---|---|---|---|
| `Senswave.Perf.Rest` | `Rest` | `REST_PORT` (8001) | `senswave_rest` |
| `Senswave.Perf.Grpc` | `Grpc` | `GRPC_PORT` (8002) | `senswave_grpc` |
| `Senswave.Perf.GrpcWeb` | `GrpcWeb` | `GRPCWEB_PORT` (8003) | `senswave_grpcweb` |

All three:

- use the same image (`senswave-perf-api`, built from `src/Presentation/Senswave.Presentation.Api/Dockerfile`) and the same certificate (`docker/https`)
- serve HTTPS only on container port `8443`, with HTTP/1.1 and HTTP/2
- run with the comparison profile from [`docs/comparison.md`](../docs/comparison.md): ASP.NET Core rate limiting off
- use the in-memory message bus, no OpenTelemetry export and no SMTP
- get the same CPU and memory limits (`API_CPUS`, `API_MEMORY`)

They share one Postgres container, with a separate database each. No RabbitMQ, data source worker or seed runs.

## Start

Run from the repository root:

```bash
sh docker/generate-cert.sh
```

```bash
cp performance/.env.template performance/.env
```

```bash
docker compose -f performance/docker-compose.yml up -d --build
```

Wait until all three API containers are `healthy` (`docker compose -f performance/docker-compose.yml ps`).

## Run k6

`k6/legal-terms.js` fetches the current terms and conditions. `TRANSPORT` selects the client:

- `rest`: `GET /api/v1/legal/terms`
- `grpc`: native gRPC `senswave.users.v1.LegalService/GetTermsAndConditions`, with the proto loaded from `src/Shared/Senswave.Contracts/Protos`
- `grpcweb`: the same rpc as a hand-framed `application/grpc-web+proto` POST (k6 has no gRPC-Web client); the check reads `grpc-status` from the trailer frame

Optional variables: `VUS` (default 10), `DURATION` (default 30s), `RAMP` (default 10s). The threshold is `checks` rate above 99%.

With k6 installed on the host, run from the repository root:

```bash
k6 run -e TRANSPORT=rest -e BASE_URL=https://localhost:8001 performance/k6/legal-terms.js
```

```bash
k6 run -e TRANSPORT=grpc -e BASE_URL=https://localhost:8002 performance/k6/legal-terms.js
```

```bash
k6 run -e TRANSPORT=grpcweb -e BASE_URL=https://localhost:8003 performance/k6/legal-terms.js
```

Or with the k6 image (Docker Desktop: the host is `host.docker.internal`). Mount the repository so the script can load the proto:

```bash
docker run --rm -v "$(pwd):/repo:ro" -w /repo grafana/k6:1.3.0 run -e TRANSPORT=grpc -e BASE_URL=https://host.docker.internal:8002 performance/k6/legal-terms.js
```

On Git Bash for Windows, prefix the command with `MSYS_NO_PATHCONV=1`.

## Rules for comparable results

- **Load one transport at a time.** The instances share Postgres, so parallel runs compete for it.
- **Record the commit SHA** (`git rev-parse HEAD`) with every run. Compare only runs from the same SHA.
- **Use the same k6 settings** (`VUS`, `DURATION`, `RAMP`) and the same machine for all three transports.
- **Account for HTTP versions.** k6 negotiates HTTP/2 over TLS for `rest` and `grpcweb` too, so all three transports run over HTTP/2 here. A browser client may use HTTP/1.1 for gRPC-Web.
- **TLS verification is off** in the script (`insecureSkipTLSVerify`), because the certificate is self-signed.
- **Checks count differently per transport.** The `grpcweb` scenario runs three checks per iteration, the others two. Compare `iterations` and request duration (`http_req_duration` or `grpc_req_duration`), not `checks_total`.

## Stop

```bash
docker compose -f performance/docker-compose.yml down
```

Add `-v` to also delete the Postgres volume.
