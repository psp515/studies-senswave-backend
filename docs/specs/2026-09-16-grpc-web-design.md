# Spec B — gRPC-Web

**Branch:** one short-lived branch per step, each merged to `main` by its own PR after owner review — see Spec A, "Delivery"
**Depends on:** Spec 0 (contracts and plumbing) and Spec A (gRPC services and the transport switch)
**Status:** design approved 2026-09-16; revised 2026-10-10 for the config-switched transport design

## Purpose

Make the gRPC surface from Spec A reachable from a browser, as the `GrpcWeb` value of `Transport:Mode`. Browsers cannot speak gRPC over HTTP/2 directly; gRPC-Web bridges that gap.

This spec gives `GrpcWeb` its meaning: everything `Grpc` serves, plus the gRPC-Web middleware. Native gRPC keeps working in `GrpcWeb` mode, so the mode is a strict superset of `Grpc`. It also adds the CORS changes browsers need to read gRPC status trailers, and an EndTest suite exercising the gRPC-Web transport specifically.

## Scope

In scope:

- `Grpc.AspNetCore.Web` server middleware, enabled in `GrpcWeb` mode only.
- CORS policy extension so browser clients can read gRPC trailers.
- Kestrel configuration serving HTTP/1.1 and HTTP/2 on the same port.
- Minimal `Senswave.<Module>.EndTestsGrpcWeb` projects: success scenarios only, plus the live-updates stream in both gRPC-Web modes.
- Deployment check: Docker images and GitHub workflows can carry gRPC-Web traffic, and the mode is settable per environment.

Out of scope:

- JSON transcoding. Rejected 2026-09-16; it would require `google.api.http` annotations on every rpc.
- A separate host or port for browser traffic. Rejected; the existing host serves it.
- Any change to the proto contracts or the gRPC service implementations. A service needing changes to work over gRPC-Web is a Spec A defect.
- Rate limiting. Same gap as Spec A.

## Architecture

### Middleware

In `src/Presentation/Senswave.Presentation.Api/Startup.cs`:

- `ConfigureServices`: call `services.AddGrpcWeb()` only when `Transport:Mode == GrpcWeb`, in line with Spec A's mode-conditional registration.
- `Configure`: when `Transport:Mode == GrpcWeb`, call `app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });`. Otherwise skip it.
- Endpoint mapping for `GrpcWeb` is identical to `Grpc` (Spec A), so no further branching.

Ordering is the most likely source of a silent failure. `UseGrpcWeb` must run after `UseRouting` and `UseCors`, and before the endpoint execution `UseEndpoints` sets up. Current order:

```
UseMiddleware<DiagnosticsMiddleware>
UseHttpsRedirection
UseCors
UseRouting
UseGrpcWeb (GrpcWeb mode only)   <- here
UseRateLimiter (conditional)
UseMiddleware<HealthCheckMiddleware>
UseAuthentication
UseAuthorization
UseMiddleware<LegalMiddleware> (Rest mode only — Spec A)
UseEndpoints
```

`DefaultEnabled = true` turns it on for every gRPC service without per-service opt-in.

### CORS

`src/Presentation/Senswave.Presentation.Api/Cors/CorsExtensions.cs` defines `SenswaveWebsitePolicy`. Browsers hide gRPC-Web trailers from JavaScript unless they are exposed. Extend the policy:

```csharp
.WithExposedHeaders("grpc-status", "grpc-message", "grpc-encoding", "grpc-accept-encoding")
```

Apply this in every mode. Exposing extra response headers has no effect on REST clients, and one policy shape for all modes is simpler than a mode-dependent CORS policy. Without it, browser calls appear to hang or fail opaquely even though the server succeeded. Its absence is a review blocker.

Allowed origins and methods are unchanged. If the policy restricts request headers, confirm `content-type`, `x-grpc-web`, and `x-user-agent` are permitted.

### Server streaming

`LiveUpdatesService.Subscribe` is the one streaming call and the part most likely to break in a real browser while passing in tests.

Confirm no client-streaming or bidirectional rpc has crept into the contracts. If one has, it is a Spec 0 defect.

Two client transport modes exist: `GrpcWebMode.GrpcWeb` (binary) and `GrpcWebMode.GrpcWebText` (base64). Some browsers deliver server streams incrementally only in text mode. The server accepts both content types, so this is a client-side choice, but it must be verified, not assumed. Cover both modes in the streaming test.

Buffering is the other hazard. Any middleware that buffers responses turns a live stream into a batch delivered at completion, which for a long-lived subscription means never. Check that `DiagnosticsMiddleware` and `HealthCheckMiddleware` do not buffer response bodies. If either does, exempt streaming paths.

### Kestrel

gRPC-Web travels over HTTP/1.1, and native gRPC needs HTTP/2. Both are served from the same port, so the endpoint must use `Http1AndHttp2`. Set it explicitly in `appsettings.json` (`Kestrel:EndpointDefaults:Protocols`), regardless of mode, since it is also correct for REST. Check `appsettings.Development.json` and the Docker configuration for any conflicting pin. HTTP/2 without TLS needs `Http2`-only or prior knowledge, so verify the container's TLS setup rather than assume.

### Deployment

`.github/workflows/` holds `dev-tests.yaml`, `staging-tests.yaml`, `prod-worker-deploy.yaml`, and `prod-deploy.yaml`. Confirm:

- The transport mode is set per environment through `Transport__Mode`, documented in `docker/.env.template` with a default of `Rest`.
- The Docker image exposes the port with both protocols.
- Any reverse proxy in front of the API forwards `application/grpc-web` and `application/grpc-web-text` unmodified and does not strip trailers.

If no proxy is under this repository's control, record that as an operational note in the PR.

## Testing

### Principle

Same as Spec A: success scenarios only, as few tests as possible, proving the gRPC-Web transport works. No failure-path, authorization-denied, or edge-case tests (decided 2026-10-10). The gRPC-Web suites prove the *transport*; Spec A's `EndTestsGrpc` already proves every rpc's mapping.

### Projects

One project per module, next to the `EndTestsGrpc` one:

```
test/Modules/Automations/Senswave.Automations.EndTestsGrpcWeb
test/Modules/DataSources/Senswave.DataSources.EndTestsGrpcWeb
test/Modules/Devices/Senswave.Devices.EndTestsGrpcWeb
test/Modules/Homes/Senswave.Homes.EndTestsGrpcWeb
test/Modules/LiveUpdates/Senswave.LiveUpdates.EndTestsGrpcWeb
test/Modules/Users/Senswave.Users.EndTestsGrpcWeb
```

### Test environment

In `test/Shared/Senswave.TestInfrastructure/TestEnvironments/GrpcWeb/`: `GrpcWebTestEnvironment` setting `Transport:Mode=GrpcWeb`, with its own database (`SenswaveEndTestsGrpcWeb`), collection, and a `GrpcWebFeatureTest` base exposing:

```csharp
protected GrpcChannel CreateGrpcWebChannel(GrpcWebMode mode = GrpcWebMode.GrpcWeb)
{
    var handler = new GrpcWebHandler(mode, Factory.Server.CreateHandler());
    return GrpcChannel.ForAddress(Factory.Server.BaseAddress, new GrpcChannelOptions
    {
        HttpHandler = handler
    });
}
```

plus authorized-client helpers parallel to Spec A's. Reuse Spec A's gRPC arrange helpers by passing the gRPC-Web channel.

### Coverage shape

One scenario test per proto service, success path only: a short lifecycle (create → read → assert payload) that exercises request, response, bearer metadata, and success status over gRPC-Web. Not every rpc needs a gRPC-Web call — the transport is the same for every unary method of a service.

| Project | Tests |
|---|---|
| Users | 2: `AuthService.Login` → `UsersService.GetUser` with the issued token (proves token issuance and bearer metadata over gRPC-Web), and `LegalService.GetPrivacyPolicy` (anonymous call) |
| Automations, DataSources | 1 each |
| Homes | 1 per proto service (homes, rooms, sharing) |
| Devices | 1 per proto service (devices, dashboards, widgets, operations, sharing) |
| LiveUpdates | 2 — `Subscribe` in `GrpcWeb` and in `GrpcWebText` |

The `Subscribe` tests assert `Initialized` arrives, then one published update arrives as a typed `LiveUpdate`. Two messages prove incremental delivery, which is the property gRPC-Web puts at risk. Every read is bounded by a timeout.

`WebApplicationFactory` exercises the middleware pipeline but not Kestrel, so Kestrel protocol configuration is not covered by these tests. Verify it manually by running the app with `Transport__Mode=GrpcWeb` and issuing a gRPC-Web call from a browser or equivalent tooling; state the result in the PR description.

### Verification

1. `dotnet build Senswave.sln`
2. `dotnet test` for every `*.EndTestsGrpcWeb` project.
3. The full suite, since middleware changes affect every host: `dotnet test`
4. Manual browser or CLI check against a running `GrpcWeb`-mode instance.

## Known gaps

- **Kestrel configuration is not covered by automated tests.** Manual verification required.
- **gRPC-Web methods are unthrottled**, same as Spec A.
- **No JSON transcoding.**

## Acceptance criteria

- `Transport:Mode=GrpcWeb` serves everything `Grpc` does plus gRPC-Web; `Rest` and `Grpc` modes do not run `UseGrpcWeb`.
- `UseGrpcWeb` is positioned after `UseRouting` and `UseCors`.
- `SenswaveWebsitePolicy` exposes the four gRPC trailer headers.
- Kestrel serves HTTP/1.1 and HTTP/2 on the same port, in development and in the container.
- `Transport__Mode` is documented in `docker/.env.template`.
- Every `*.EndTestsGrpcWeb` project passes, including both-mode `Subscribe` tests; no failure-path tests exist.
- No client-streaming or bidirectional rpc exists in the contracts.
- Response buffering is confirmed absent in front of streaming calls.
- The full existing suite passes.
- Manual browser or CLI verification is recorded in the PR description.
