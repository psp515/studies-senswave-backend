# Comparing REST, gRPC and gRPC-Web

How to produce comparable runs of the three transports (Spec D, `docs/specs/2026-09-16-rest-baseline-design.md`). All three live in one build. Runs differ only in `Transport:Mode` and, for REST, in a test filter. Nothing is deleted to make them comparable.

## Ground rules

- **One commit per comparison.** Record `git rev-parse HEAD` with every run. Results from different SHAs are not comparable.
- **Rate limiting off** in every comparison run (`RateLimiter__Enabled=false`). gRPC methods are not throttled, so REST must not be either. With the flag off, `UseRateLimiter()` is not called and the endpoint `RequireRateLimiting(...)` metadata is inert. No code change is needed, and any environment may do this.
- **Success paths only.** The gRPC and gRPC-Web suites are success-only by construction. REST failure-path tests are tagged and filtered out.

## Test-suite comparison

Each transport has its own EndTest projects:

| Transport | Projects | Filter |
|---|---|---|
| REST | `Senswave.<Module>.EndTests`, `Senswave.Modules.EndTests`, `Senswave.Presentation.Api.EndTests` | `Category!=FailurePath&Category!=RateLimiting` |
| gRPC | `Senswave.<Module>.EndTestsGrpc` | none |
| gRPC-Web | `Senswave.<Module>.EndTestsGrpcWeb` | none |

```bash
dotnet test test/Modules/Devices/Senswave.Devices.EndTests --filter "Category!=FailurePath&Category!=RateLimiting"
dotnet test test/Modules/Devices/Senswave.Devices.EndTestsGrpc
dotnet test test/Modules/Devices/Senswave.Devices.EndTestsGrpcWeb
```

Repeat for `Automations`, `DataSources`, `Homes`, `LiveUpdates` and `Users`. On the REST side, also run `test/Modules/Senswave.Modules.EndTests` and `test/Presentation/Senswave.Api.EndTests/Senswave.Presentation.Api.EndTests`. The latter holds only rate-limiter tests, so the filter leaves nothing to run.

### Tags

- `[Trait("Category", "FailurePath")]` goes on every REST EndTest whose subject assertion is a refusal: a non-success status code, a SignalR connection or call that is refused, or a 302 to the failure page. Success responses in arrange steps don't count. A "delete, then GET returns 404" test is a success: the 404 only confirms the delete.
- `[Trait("Category", "RateLimiting")]` goes on the ASP.NET Core rate-limiter test classes, which comparison runs disable.
- The LiveUpdates module's own `IRateLimiterService` (SignalR `Initialize` throttling) is module logic. It stays active in `Rest` mode, and its refusal test is tagged `FailurePath`.
- Tests that assert a side effect did **not** happen (automation not triggered while offline, event not published) have no refusal status, so they stay untagged and run in comparisons.

New REST failure-path tests must carry the tag. An untagged one silently enters comparison runs.

## Running instances (load and latency)

Use the same image and configuration for each instance. Only the mode changes:

```bash
Transport__Mode=Rest     RateLimiter__Enabled=false
Transport__Mode=Grpc     RateLimiter__Enabled=false
Transport__Mode=GrpcWeb  RateLimiter__Enabled=false
```

Kestrel serves HTTP/1.1 and HTTP/2 on every endpoint (`Kestrel:EndpointDefaults:Protocols=Http1AndHttp2`), but **HTTP/2 needs TLS** there. A plain-HTTP endpoint serves HTTP/1.1 only. Native gRPC therefore needs an HTTPS endpoint, while REST and gRPC-Web work on either. The Docker setup serves the API over HTTPS only (container port `8443`, host port `API_HTTPS_PORT`), so every mode, native gRPC included, runs over TLS with HTTP/1.1 or HTTP/2. Run `docker/generate-cert.sh` first and give clients `docker/https/senswave.api.crt` (see `docker/README.md`). Compare transports over HTTPS in every mode, so TLS cost is the same for all three.

Traces from all modes can share one collector. Each instance tags its resource with `senswave.transport` = `Rest` | `Grpc` | `GrpcWeb`, which is how to tell them apart.

## Coverage

Collect coverage per suite with the same filters, and report the results side by side with the unfiltered run:

```bash
dotnet test <project> --filter "<same filter>" --collect:"XPlat Code Coverage" --results-directory TestResults/<suite>
```

`dotnet-coverage collect "dotnet test ..." -f cobertura` works too (see CLAUDE.md). Exclude test assemblies, `Senswave.TestInfrastructure` and EF migrations, as CI does. Report test counts next to coverage: the suites are not the same size. The gRPC and gRPC-Web projects are deliberately small smoke suites, while filtered REST keeps every success-path test. Treat suite results as "does each transport work", and compare runtime behaviour on running instances.

## Behavioural differences to keep in mind

- **Live-update errors.** SignalR returns silently. gRPC ends the stream with a status code.
- **Auth runs over the selected transport.** In gRPC modes, `AuthService.Login` issues the same bearer tokens as the REST Identity API.
- **Google login** exists in `Rest` mode only.
- **gRPC modes are unthrottled**, auth and live-update subscriptions included.
- **The email confirmation link** (`ConfirmEmailV2`) stays HTTP in every mode.

## Last recorded run

See the Spec D PR description for counts and coverage at the commit it was produced on.
