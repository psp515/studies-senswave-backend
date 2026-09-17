# Spec B — gRPC-Web

**Branch:** `main/grpc-web`, branched from `main/grpc`
**Depends on:** Spec 0 (contracts and plumbing) and Spec A (gRPC services), both of which live on `main/grpc`
**Status:** design approved 2026-09-16

## Purpose

Make the gRPC surface from Spec A reachable from a browser. Browsers cannot speak gRPC over HTTP/2 directly; gRPC-Web is the transport that bridges that gap. This spec adds the server-side gRPC-Web middleware, the CORS changes browsers need to read gRPC status trailers, and an EndTest suite that exercises the gRPC-Web transport specifically.

## Branch dependency

This branch is cut from `main/grpc`, not from `main`. The middleware alone is untestable — there is nothing to call over gRPC-Web until Spec A's services exist. Starting work in parallel with Spec A is possible, but the branch cannot be verified until Spec A's services are on `main/grpc` and this branch has rebased onto them.

## Scope

In scope:

- `Grpc.AspNetCore.Web` server middleware, enabled for all gRPC services.
- CORS policy extension so browser clients can read gRPC status and message trailers.
- Kestrel configuration serving HTTP/1.1 and HTTP/2 on the same port.
- A gRPC-Web EndTest suite covering one rpc per module, success path only, plus the live-updates server stream.
- A check that the deployment path — Docker images and the GitHub workflows — does not break gRPC-Web traffic.

Out of scope:

- JSON transcoding. Considered and rejected on 2026-09-16; it would require `google.api.http` annotations across all 73 rpcs.
- A separate presentation host or port for browser traffic. Considered and rejected; the existing host serves both.
- Any change to the proto contracts or the service implementations. If a service needs changing to work over gRPC-Web, that is a defect in Spec A and belongs on `main/grpc`.
- Rate limiting. Same gap as Spec A.

## Architecture

### Middleware

In `src/Presentation/Senswave.Presentation.Api/Startup.cs`:

- `ConfigureServices`: add `services.AddGrpcWeb();`
- `Configure`: add `app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });`

Ordering matters and is the most likely source of a silent failure. `UseGrpcWeb` must run after `UseRouting` and after `UseCors`, and before the endpoint execution that `UseEndpoints` sets up. The current pipeline order is:

```
UseMiddleware<DiagnosticsMiddleware>
UseHttpsRedirection
UseCors
UseRouting
UseRateLimiter (conditional)
UseMiddleware<HealthCheckMiddleware>
UseAuthentication
UseAuthorization
UseMiddleware<LegalMiddleware>
UseEndpoints
```

Place `UseGrpcWeb` directly after `UseRouting`. `DefaultEnabled = true` turns it on for every gRPC service without per-service opt-in; with all module services intended for browser access, per-service enablement would be noise.

### CORS

`src/Presentation/Senswave.Presentation.Api/Cors/CorsExtensions.cs` defines `SenswaveWebsitePolicy`. gRPC-Web returns status information in trailers that a browser will not expose to JavaScript unless they are listed as exposed headers. Extend the policy:

```csharp
.WithExposedHeaders("grpc-status", "grpc-message", "grpc-encoding", "grpc-accept-encoding")
```

Without this, browser calls appear to hang or fail with an opaque error even though the server handled them correctly. This is the single most common gRPC-Web misconfiguration; treat its absence as a review blocker.

The policy's existing allowed origins, methods, and headers are unchanged. If the policy currently restricts request headers, confirm `content-type`, `x-grpc-web`, and `x-user-agent` are permitted, since the gRPC-Web client sends them.

### Server streaming

`LiveUpdatesService.Subscribe` is the one streaming call in the contract set, and it is the part of this branch most likely to break in a real browser while passing in tests.

Spec 0 constrains the contract to server streaming precisely so this works: gRPC-Web supports unary and server streaming, and nothing else. Confirm during implementation that no client-streaming or bidirectional rpc has crept into the contracts. If one has, it is a Spec 0 defect and must be fixed on `main/grpc`, not worked around here.

Two transport modes exist. `GrpcWebMode.GrpcWeb` sends binary; `GrpcWebMode.GrpcWebText` base64-encodes. Browsers that cannot read binary response bodies progressively need the text mode for server streaming, because a binary stream is not delivered incrementally to JavaScript in every browser. The server accepts both — `UseGrpcWeb` handles the `application/grpc-web` and `application/grpc-web-text` content types alike — so this is a client-side choice, but it must be verified, not assumed.

Cover both modes in the streaming test. It is the one place where testing two variants earns its cost, since a mode mismatch on a stream presents as the stream never delivering rather than as an error.

Buffering is the other hazard. Any proxy or middleware that buffers responses turns a live stream into a batch delivered at completion — which for a long-lived subscription means never. Check that `DiagnosticsMiddleware` and `HealthCheckMiddleware`, both of which wrap the pipeline, do not buffer the response body. If either does, exempt streaming paths.

### Kestrel

gRPC-Web travels over HTTP/1.1. Native gRPC needs HTTP/2. Both must be served from the same port, so Kestrel's endpoint must use `Http1AndHttp2`. Check `appsettings.json`, `appsettings.Development.json`, and any Docker or container configuration for an explicit protocol pin, and set `Http1AndHttp2` where a pin exists. Where nothing is pinned, the default already permits both over TLS — verify rather than assume, because HTTP/2 without TLS requires explicit configuration.

### Deployment

`.github/workflows/` holds `dev-tests.yaml`, `staging-tests.yaml`, `prod-worker-deploy.yaml`, and `prod-deploy.yaml`. Confirm:

- The Docker image exposes the port with both protocols available.
- Any reverse proxy or ingress in front of the API forwards `application/grpc-web` and `application/grpc-web-text` content types unmodified and does not strip trailers.

If no proxy is under this repository's control, record that as an operational note in the PR rather than a code change.

## Testing

### Suite placement

Add a `GrpcWeb/` folder to `test/Presentation/Senswave.Api.EndTests/Senswave.Presentation.Api.EndTests/`, sitting alongside the existing `RateLimiter/` folder. This suite belongs at the presentation level, not per module: it tests the transport, not module behavior. Module behavior is already covered by Spec A's per-module gRPC EndTests, and duplicating it here would double the maintenance for no added signal.

### Coverage shape

One rpc per module, five tests total, success path only:

| Module | Rpc under test |
|---|---|
| Devices | `CreateDevice` |
| Homes | a create or fetch rpc with a simple arrange step |
| DataSources | as above |
| Automations | as above |
| Users | an authenticated, non-auth rpc |
| LiveUpdates | `Subscribe`, in both `GrpcWeb` and `GrpcWebText` modes |

Each test asserts that the call completes over gRPC-Web and returns the expected payload. The point is to prove the transport carries request, response, authorization metadata, and a success status — not to re-test business rules.

The `Subscribe` tests assert that `Initialized` arrives, then that one published update arrives as a typed `LiveUpdate`. Two messages is enough to prove the stream delivers incrementally rather than at completion, which is the property gRPC-Web actually puts at risk. Bound every read with a timeout, as Spec A requires, so a broken stream fails rather than hangs.

Add one further test asserting that an authenticated rpc called over gRPC-Web with no credentials returns `Unauthenticated`. This is the sole exception to the success-paths-only rule across all four specs, and it is justified: authorization crossing the gRPC-Web boundary is exactly the thing this transport can silently break, and no other test in the repository would catch it.

### Client setup

Use `GrpcWebHandler` from `Grpc.Net.Client.Web` wrapping the test server's handler:

```csharp
var handler = new GrpcWebHandler(GrpcWebMode.GrpcWeb, Factory.Server.CreateHandler());
var channel = GrpcChannel.ForAddress(Factory.Server.BaseAddress, new GrpcChannelOptions
{
    HttpHandler = handler
});
```

Add this as a helper on `BaseFeatureTest` next to the `CreateChannel` helper Spec A introduces, so the two transports are visibly parallel.

Note that `WebApplicationFactory` exercises the middleware pipeline but not Kestrel. The Kestrel protocol configuration is therefore **not** covered by these tests; verify it by running the app and issuing a gRPC-Web call from a browser or from `grpcurl`-equivalent tooling. State the result of that manual check in the PR description — an automated suite passing is not evidence that a browser can reach the server.

### Verification

1. `dotnet build Senswave.sln`
2. `dotnet test` for `Senswave.Presentation.Api.EndTests`
3. The full suite, since middleware ordering changes affect every EndTest: `dotnet test`
4. Manual browser or CLI check against a running instance, per the note above.

## Known gaps

- **Kestrel configuration is not covered by automated tests.** Manual verification required.
- **gRPC-Web methods are unthrottled**, same as Spec A.
- **No JSON transcoding.** Clients that cannot use gRPC-Web have no path to the 73 converted endpoints.

## Acceptance criteria

- `AddGrpcWeb` and `UseGrpcWeb` are wired with `UseGrpcWeb` positioned after `UseRouting` and `UseCors`.
- `SenswaveWebsitePolicy` exposes the four gRPC trailer headers.
- Kestrel is confirmed to serve HTTP/1.1 and HTTP/2 on the same port, in development configuration and in the container.
- Five unary success-path transport tests pass, plus the one `Unauthenticated` test, plus `Subscribe` streaming tests in both gRPC-Web modes.
- No client-streaming or bidirectional rpc exists anywhere in the contracts.
- Response buffering is confirmed absent from the middleware in front of streaming calls.
- The full existing suite passes, confirming the middleware insertion broke nothing.
- Manual browser or CLI verification is recorded in the PR description.
