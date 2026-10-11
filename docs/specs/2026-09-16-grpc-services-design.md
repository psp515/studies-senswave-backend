# Spec A — gRPC Services and the Transport Switch

**Branch:** one short-lived branch per step, each merged to `main` by its own PR after owner review — see Spec A, "Delivery"
**Depends on:** Spec 0 (gRPC contracts and plumbing)
**Status:** design approved 2026-09-16; revised 2026-10-10 for the config-switched transport design

## Purpose

Add gRPC services for the five feature modules **alongside** the existing REST surface. Add a configuration switch that selects which surface the host serves. After this spec, the same build runs as a REST API or a gRPC API depending on one setting, and nothing that exists today is removed.

The handlers do not change. Every module already routes through MediatR: `CreateDeviceEndpoint` builds a `CreateDeviceCommand` and sends it. A gRPC service method builds the same command from a proto message and sends it to the same handler. Both transports are thin mapping layers over shared Application code.

## Scope

In scope:

- The transport switch: options, startup wiring, per-mode mapping.
- One gRPC service implementation per proto service, in the owning module's `Api` project.
- Proto-to-command and result-to-proto mapping, following the existing `*Extensions` convention.
- A gRPC live-updates path (`LiveUpdatesService.Subscribe`) alongside the SignalR hub, selected by the switch.
- gRPC equivalents of the legal endpoints and the ASP.NET Identity API (`AuthService`, `AccountService`), plus the consent check that `LegalMiddleware` performs for REST.
- A minimal `Senswave.<Module>.EndTestsGrpc` project per module: success scenarios only, proving every rpc works.

Out of scope:

- Deleting or changing any REST endpoint, REST DTO, or the SignalR hub. **All of them stay.**
- `ConfirmEmailV2Endpoint`, the email-link navigation target that is REST in every mode, plus `DiagnosticModule` and health checks. See Spec 0.
- Google login (`GoogleAuthEndpoint`, `GoogleJwtAuthEndpoint`). It is not used, gets no gRPC equivalent, stays REST-only, and is not mapped in gRPC modes. Its handlers are untouched.
- Changes to unit or integration tests, which do not touch transport.
- Rate limiting for gRPC methods. See "Known gaps".
- gRPC-Web. That is Spec B, which adds a third mode value.

## The transport switch

### Options

Add to `Senswave.Abstractions` (a pure contract, readable by modules and the host without a web dependency):

```csharp
public enum TransportMode { Rest, Grpc, GrpcWeb }

public sealed class TransportOptions
{
    public const string SectionName = "Transport";
    public TransportMode Mode { get; set; } = TransportMode.Rest;
}
```

`appsettings.json`:

```json
"Transport": {
  "Mode": "Rest"
}
```

Override per environment with `Transport__Mode=Grpc`, through `.env` in the Docker setup or a deployment variable. The default is `Rest`, so an environment that sets nothing behaves exactly as it does today.

`GrpcWeb` is defined here so the enum is complete, but Spec B gives it meaning. Until Spec B lands, `GrpcWeb` behaves as `Grpc`.

### What each mode serves

| Surface | `Rest` | `Grpc` / `GrpcWeb` |
|---|---|---|
| 78 `IEndpoint` operations (all except `ConfirmEmailV2`; includes the 2 REST-only Google logins) | mapped | **not mapped** |
| `MapIdentityApi<User>()` (`api/v1/auth/*`) | mapped | **not mapped** |
| gRPC services: modules, `UsersService`, `LegalService`, `AuthService`, `AccountService` | **not mapped** | mapped |
| SignalR hub `signalr/liveupdates/live` | mapped | **not mapped** |
| `LiveUpdatesService.Subscribe` | **not mapped** | mapped |
| `ConfirmEmailV2` (`GET api/v2/auth/confirmEmail`), diagnostics, health | mapped | mapped |
| `MapApiPublicEndpoints`: `/.well-known/assetlinks.json`, `/.well-known/apple-app-site-association`, `GET api/version` | mapped | mapped (unchanged) |

Exactly one API surface is live at a time. Every operation a client calls, including login and the legal documents, goes through the selected transport. Google login exists only in `Rest` mode. The HTTP routes left in gRPC modes are `ConfirmEmailV2`, which users open from an email in a browser, and the host's `MapApiPublicEndpoints`. The `.well-known` files are fetched by Android and iOS for app-link verification. `api/version` stays HTTP and gets no gRPC equivalent (decided 2026-10-10). A comparison run therefore measures one transport without the other present. Serving both at once is deliberately not a mode: it would make "which transport handled this" ambiguous in telemetry, and nothing needs it.

The always-REST set must not be a hand-maintained route list. Introduce a marker interface `INavigationEndpoint` in `Senswave.Infrastructure.Web/Endpoints` for endpoints that are browser navigation targets rather than API calls. Implement it on `ConfirmEmailV2Endpoint` only, and have the mapping filter on it.

### Startup wiring

In `src/Presentation/Senswave.Presentation.Api/Startup.cs`:

- `ConfigureServices`: bind `TransportOptions` and read the mode once. Registration is **mode-conditional at the `Startup.cs` level**, so each mode's container holds only what that mode uses:

  | Registration in `Startup.ConfigureServices` | `Rest` | `Grpc` / `GrpcWeb` |
  |---|---|---|
  | `services.AddScoped<LegalMiddleware>()` | yes | **no** |
  | `services.AddGrpcInfrastructure()` (`AddGrpc` + `ErrorHandlingInterceptor`) | **no** | yes |
  | `LegalConsentInterceptor` added to the `AddGrpc` interceptor list | **no** | yes |
  | `services.AddGrpcWeb()` (Spec B) | **no** | `GrpcWeb` only |

  Pass the mode to modules that register transport-specific services (see LiveUpdates).
- `Configure`, inside `UseEndpoints`:
  - `Rest`: `defaultGroutBuilder.MapEndpoints(minimalApiEndpoints)` exactly as today, then `endpoints.UseLiveUpdates()`.
  - `Rest` also: the public versioned group (`MapPublicEndpoints`) and `UseAuthEndpoints`, exactly as today.
  - Every mode: `MapApiPublicEndpoints`, unchanged.
  - `Grpc`/`GrpcWeb`: `defaultGroutBuilder.MapEndpoints(minimalApiEndpoints.OfType<INavigationEndpoint>())`, then `module.MapGrpcServices(endpoints)` for each module. No `UseLiveUpdates()`, no `UseAuthEndpoints` (the Identity API), and no `MapPublicEndpoints` (the legal `IPublicEndpoint`s).
- `Configure`: `app.UseMiddleware<LegalMiddleware>()` runs **only in `Rest` mode**, matching its registration. In gRPC modes, `LegalMiddleware` is neither registered nor in the pipeline, and `LegalConsentInterceptor` is the only consent check. This keeps each mode's consent check in exactly one place.
- `DiagnosticsMiddleware` stays in every mode. Its `Response.OnStarting` timing is correct for unary calls. For the live-updates stream it fires when the stream opens, so the logged "Executed total" is the time to first response, not the stream's lifetime. Accept this and note it in `docs/comparison.md`; don't add stream-specific timing.
- Log the active mode at startup next to the existing `"Modules: {modules}"` line, and add it as a resource attribute (`senswave.transport`) on the OpenTelemetry resource so every span records which transport produced it. That attribute is what makes cross-mode comparison in traces possible.
- Kestrel must serve HTTP/2 for gRPC. Over HTTPS the default (`Http1AndHttp2`) already does. Confirm `appsettings*` and the Docker setup do not pin HTTP/1.1. Spec B owns the explicit protocol setting.

Swagger stays registered in every mode. In gRPC modes, the module documents are simply empty, which is accurate. No Swagger code is removed.

### Module registration

Add to `Senswave.Abstractions.Modules.ISenswaveModule`:

```csharp
void MapGrpcServices(IEndpointRouteBuilder endpoints);
```

Each module implements it with explicit `endpoints.MapGrpcService<T>()` calls. `UsersModule` maps `UsersGrpcService`, `LegalGrpcService`, `AuthGrpcService`, and `AccountGrpcService`. Explicit beats reflection here: the list is short, and a missing service fails at compile time. `LiveUpdatesModule` maps `LiveUpdatesGrpcService`. Modules with no gRPC surface (none today) implement an empty body.

`Senswave.Abstractions` must not take an ASP.NET dependency to carry `IEndpointRouteBuilder`. If it does not already reference the ASP.NET framework, put `MapGrpcServices` on a separate interface `ISenswaveGrpcModule` in `Senswave.Infrastructure.Grpc`, and have the host call it via `_modules.OfType<ISenswaveGrpcModule>()`. Check the existing references before choosing; prefer the option that adds no new dependency edge.

## Service implementations

Each module's `Api` project gains a `Grpc/` folder, one class per proto service:

```
src/Modules/Devices/Senswave.Devices.Api/Grpc/
  DevicesGrpcService.cs
  DashboardsGrpcService.cs
  WidgetsGrpcService.cs
  OperationsGrpcService.cs
  SharingGrpcService.cs
```

```csharp
[Authorize(AuthenticationSchemes = "Identity.Bearer")]
internal sealed class DevicesGrpcService(IMediator mediator, IRequestContext context)
    : DevicesService.DevicesServiceBase
{
    public override async Task<CreateDeviceResponse> CreateDevice(
        CreateDeviceRequest request, ServerCallContext callContext)
    {
        var command = request.ToCommand(context.UserId);
        var result = await mediator.Send(command, callContext.CancellationToken);
        result.ThrowIfFailure();
        return result.ToCreateDeviceResponse();
    }
}
```

`[Authorize]` sits on the class. gRPC has no equivalent of the `MapGroup` inheritance the REST surface relies on, so audit each endpoint's effective authorization (attribute plus group) before writing its method. Every gRPC method must enforce authorization equivalent to the REST endpoint it mirrors.

If a service class grows past roughly 400 lines, split the proto service rather than let the class sprawl.

### Mapping

The REST feature folder keeps its files untouched and gains a gRPC mapping file beside them:

```
src/Modules/Devices/Senswave.Devices.Api/Devices/CreateDevice/
  CreateDeviceEndpoint.cs        (unchanged)
  CreateDeviceRequest.cs         (unchanged, REST DTO)
  DeviceCreatedResponse.cs       (unchanged, REST DTO)
  CreateDeviceExtensions.cs      (unchanged, REST mapping)
  CreateDeviceGrpcExtensions.cs  (new, proto mapping)
```

Generated proto types and REST DTOs share simple names (`CreateDeviceRequest`). Inside `*GrpcExtensions` and the gRPC services, reference proto types through a namespace alias (`using Proto = Senswave.Contracts.Devices.V1;`) so the two never get confused.

```csharp
internal static class CreateDeviceGrpcExtensions
{
    internal static CreateDeviceCommand ToCommand(this Proto.CreateDeviceRequest dto, Guid userId) => new()
    {
        HomeId = Guid.Parse(dto.HomeId),
        Icon = dto.Icon,
        Name = dto.Name,
        UserId = userId,
    };

    internal static Proto.CreateDeviceResponse ToCreateDeviceResponse(this Result<Device> result) => new()
    {
        Id = result.Data.Id.ToString(),
    };
}
```

Guid and timestamp conversion lives in `*GrpcExtensions` and nowhere else. A malformed Guid in a request must surface as `InvalidArgument`, not as an unhandled `FormatException`. Use a shared `ParseGuid(string value, string field)` helper in `Senswave.Infrastructure.Grpc` that throws `SenswaveRpcException` with a `Validation` error.

The REST DTO freeze in CLAUDE.md is untouched, because no REST DTO changes.

## Users: legal, authentication, account

### Legal

`LegalGrpcService` carries `[AllowAnonymous]` and calls `ILegalService` exactly as the REST endpoints do, with the same 10-second timeout.

### Consent check (`LegalMiddleware` equivalent)

`LegalMiddleware` blocks authenticated users who lack the latest consents. It selects requests by matching the REST path `^/api/v\d+/(?!users|auth|legal)...`. gRPC paths (`/senswave.devices.v1.DevicesService/CreateDevice`) never match, so **without an equivalent, gRPC mode silently skips the consent check.**

Add `LegalConsentInterceptor` in `Senswave.Users.Api/Grpc` (*revised during implementation*: `Users.Infrastructure` may not reference `Senswave.Infrastructure.Grpc` under the module-layering architecture rule, and the interceptor needs its error trailers), added to the `AddGrpc` interceptor list in `Startup.ConfigureServices` in gRPC modes only. It is the **only** consent check in gRPC modes; `LegalMiddleware` is not registered there (see Startup wiring). It applies the same rule. If `IRequestContext.UserId` is set and the method's service is **not** in package `senswave.users.v1` (the gRPC counterpart of the `users|auth|legal` exemption) or `senswave.liveupdates.v1`, it calls `IUserService.UserHasLatestConsents`. Live updates are exempt because REST does not check them either: the SignalR path `signalr/...` never matches `LegalMiddleware`'s regex (decided 2026-10-10). The interceptor wraps both `UnaryServerHandler` and `ServerStreamingServerHandler`. On failure it throws `RpcException(PermissionDenied)` with the errors in the error trailer. Keep the middleware's log line.

### Issuing tokens over gRPC

REST login (the Identity API's `/login`) works by calling `SignInManager.SignInAsync` with the bearer scheme. The `BearerTokenHandler` then **writes the `AccessTokenResponse` JSON directly into the HTTP response**. Under gRPC that would corrupt the response, so gRPC must issue tokens explicitly.

Add `IAccessTokenIssuer` in `Senswave.Users.Infrastructure`:

```csharp
Task<AccessTokenResponse> IssueAsync(User user, CancellationToken ct);
Task<Result<AccessTokenResponse>> RefreshAsync(string refreshToken, CancellationToken ct);
```

`IssueAsync` builds the principal with `SignInManager.CreateUserPrincipalAsync`. It protects the ticket with the protectors in `IOptionsMonitor<BearerTokenOptions>.Get(IdentityConstants.BearerScheme)` (`BearerTokenProtector`, `RefreshTokenProtector`) and uses that options instance's `BearerTokenExpiration` and `RefreshTokenExpiration`. Tokens are therefore **byte-compatible with REST tokens**: a token issued over gRPC authenticates REST calls and vice versa. `RefreshAsync` mirrors the framework's `/refresh`: unprotect, check expiry, `SignInManager.ValidateSecurityStampAsync`, reissue.

### Identity API port (`AuthService`, `AccountService`)

Port each `MapIdentityApi` endpoint's logic from the framework source (`IdentityApiEndpointRouteBuilderExtensions`, .NET 10) onto `UserManager<User>`, `SignInManager<User>`, and `IEmailSender<User>`. Keep the behavior; report failures as gRPC statuses instead of `ValidationProblem`.

| rpc | Behavior to preserve |
|---|---|
| `Register` | `UserManager.CreateAsync`, then send the confirmation link. Build the link to the `ConfirmEmailV2` route with the same `userId` and `code` query as Identity, so `HtmlEmailSender.UpdateConfirmLink` rewrites it as it does today. |
| `Login` | `PasswordSignInAsync` semantics: lockout, `RequireConfirmedEmail`, and the 2FA code and recovery-code paths. On success, `IAccessTokenIssuer.IssueAsync`. Failure → `Unauthenticated`. |
| `Refresh` | `IAccessTokenIssuer.RefreshAsync`. |
| `ConfirmEmail` | `UserManager.ConfirmEmailAsync` with the Base64Url-decoded code. |
| `ResendConfirmationEmail`, `ForgotPassword` | Always succeed, without revealing whether the user exists, as the framework does. |
| `ResetPassword` | `UserManager.ResetPasswordAsync` with the decoded code. |
| `AccountService.GetInfo` / `UpdateInfo` / `TwoFactor` | `[Authorize]`; same logic as `/manage/*`. |

`IdentityResult` errors map to `InvalidArgument`, with each `IdentityError` carried as a `common.v1.Error` (`code` = `IdentityError.Code`, type `Validation`).

## Live updates — SignalR in REST mode, gRPC streaming otherwise

`Senswave.LiveUpdates.Api` keeps its SignalR hub for `Rest` mode and gains a server-streaming gRPC equivalent for gRPC modes. Exactly one is mapped.

Server streaming is required, because gRPC-Web supports only unary and server streaming (Spec 0).

### Concept mapping

| SignalR (REST mode) | gRPC (gRPC modes) |
|---|---|
| Hub connection, then `Initialize(homeReferenceId)` | one `Subscribe` call carrying the home id |
| Switching home: call `Initialize` again | cancel the stream, open a new `Subscribe` |
| `Initialized()` callback | first `LiveUpdate` on the stream, `initialized` case |
| `FailedToInitialize(reason)` callback | the call fails with an `RpcException` (below) |
| `Update(updateType, data)` callback | subsequent `LiveUpdate` messages, typed by `oneof` |
| `Groups.AddToGroupAsync(connectionId, group)` | registry subscribes the stream to the same group names |
| `IHubContext.Clients.Group(g).Update(...)` | registry fans the update out to that group's streams |
| `Context.ConnectionId` | a per-stream subscription id generated on `Subscribe` |
| `Context.UserIdentifier` | `IRequestContext.UserId` (see Spec 0) |
| `Context.ConnectionAborted` | `ServerCallContext.CancellationToken` |

`FailedToInitialize` becomes call failure, because a subscription that cannot be established should not produce a stream:

| Condition | gRPC status |
|---|---|
| `homeReferenceId` is not a valid Guid | `InvalidArgument` |
| home not found, or user neither owner nor in `AllowedUsers` | `PermissionDenied` |

The hub's own behavior in REST mode is not changed. It still returns silently on an unparseable Guid. The gRPC path surfaces these as statuses. Record the difference in the PR description, because it is a behavioral difference between modes that a comparison should know about.

### Subscription registry

```csharp
public interface ILiveUpdatesSubscriptionRegistry
{
    ILiveUpdatesSubscription Subscribe(IReadOnlyCollection<string> groupNames);
    Task PublishAsync(string groupName, LiveUpdate update, CancellationToken cancellationToken);
}
```

A singleton. Each subscription owns a bounded `Channel<LiveUpdate>` with a drop-oldest policy. Read the bound from `LiveUpdatesOptions`; an unbounded channel lets a stalled client grow memory without limit. The service method drains the channel into `IServerStreamWriter<LiveUpdate>` until the call is cancelled, then disposes the subscription, which removes it from every group.

Group names are unchanged, from `GuidExtensions`: `senswave-devices-{id}`, `senswave-datasources-{id}`, `senswave-homes-{id}`.

### Update services: one implementation per transport

`IDevicesUpdateService` and `IDataSourcesUpdateService` keep their interfaces. The existing `DevicesUpdateService` and `DataSourcesUpdateService` (SignalR, `JsonObject`) stay **unchanged**. Add `GrpcDevicesUpdateService` and `GrpcDataSourcesUpdateService`, which build typed `LiveUpdate` messages and publish through the registry.

`LiveUpdatesModule.Register` reads `TransportOptions` from configuration and registers:

| Mode | Registered |
|---|---|
| `Rest` | `AddSignalR()`, the two SignalR update services, `RateLimitingService` (as today) |
| `Grpc` / `GrpcWeb` | the registry singleton, the two gRPC update services |

The four MassTransit consumers (`DeviceTileActionEventConsumer`, `WidgetActionEventConsumer`, `DataSourceStateConsumer`, `DevicePresenceEventConsumer`) depend only on the interfaces and stay untouched.

### The work

1. **`LiveUpdatesGrpcService`** in `Senswave.LiveUpdates.Api/Grpc/`, `[Authorize(AuthenticationSchemes = "Identity.Bearer")]` as on the hub. `Subscribe` runs the hub's `Initialize` checks in the same order, except the rate limiter: Guid parse, `HomeRequest` access check, `DevicesRequest` group resolution. It then writes `Initialized` and drains the subscription until cancellation. Keep the same log statements.
2. **Registry** and its implementation.
3. **gRPC update services**, as above.
4. **Telemetry.** The SignalR path keeps its `"SignalR /update"` activity and `signalr.*` tags unchanged. The gRPC path uses `"LiveUpdates /update"` with `rpc.*` tags. Both go through `ILiveUpdatesActivityProvider`; add a method or parameter rather than duplicate the provider.
5. **Rate limiter.** The gRPC path does **not** call `IRateLimiterService` (decided 2026-10-10: rate limiting is out of scope for gRPC). Its key is per SignalR connection, and there is no meaningful gRPC equivalent. A per-subscribe key would also grow its never-cleaned dictionary without bound. Register `RateLimitingService` in `Rest` mode only, and leave the service unchanged.
6. **Health check.** If `AspNetCore.HealthChecks.SignalR` probes the hub, it fails in gRPC modes because the hub is not mapped. Register that check only in `Rest` mode. Do not remove the package.

### HA behavior is unchanged, and still limited

`MessagingExtensions` gives each consumer one receive endpoint. Across instances, that is competing consumers: an event reaches one instance, and only clients connected there see it. This is true of SignalR today (no backplane) and of the in-process registry equally. Neither mode fixes or worsens it. Do not attempt HA here.

## Testing

### Principle

The gRPC tests verify that **the transport works**: every rpc is reachable, carries auth, and returns the expected payload on a success path. They are not a second copy of the business-rule suite; the REST EndTests, unit tests, and integration tests already own that. Write as few tests as possible that still call every rpc successfully at least once. No edge cases, no failure paths, no refusal or authorization-denied assertions (decided 2026-10-10).

Existing REST EndTests, unit tests, and integration tests are untouched. Needing to edit one means something leaked out of the transport layer.

### Projects

One new EndTest project per module, next to the existing REST one:

```
test/Modules/Automations/Senswave.Automations.EndTestsGrpc
test/Modules/DataSources/Senswave.DataSources.EndTestsGrpc
test/Modules/Devices/Senswave.Devices.EndTestsGrpc
test/Modules/Homes/Senswave.Homes.EndTestsGrpc
test/Modules/LiveUpdates/Senswave.LiveUpdates.EndTestsGrpc
test/Modules/Users/Senswave.Users.EndTestsGrpc
```

Each is added to `Senswave.sln`, mirrors the existing EndTests csproj (configurations, `TESTS` constant, package and fixture references), and references `Senswave.Contracts`. Spec B adds the matching `*.EndTestsGrpcWeb` projects.

### Shared gRPC test environment

In `test/Shared/Senswave.TestInfrastructure/TestEnvironments/Grpc/`:

- `GrpcTestEnvironment` — derives from or mirrors `BaseTestEnvironment`, sets `Transport:Mode=Grpc` via `ConfigureAppConfiguration`, and uses its own database name (`SenswaveEndTestsGrpc`), following the `LimitTestEnvironment` precedent, so it cannot interfere with REST collections.
- `GrpcTestCollection` and `GrpcFeatureTest`, mirroring the `Base` trio.

`GrpcFeatureTest` exposes:

```csharp
protected GrpcChannel CreateChannel() =>
    GrpcChannel.ForAddress(Factory.Server.BaseAddress, new GrpcChannelOptions
    {
        HttpHandler = Factory.Server.CreateHandler()
    });

protected Task<T> CreateAuthorizedClientAsUser<T>() where T : ClientBase<T>;
protected Task<T> CreateAuthorizedClientAsAdmin<T>() where T : ClientBase<T>;
```

The authorized variants obtain a token by calling `AuthService.Login` over gRPC (in `Grpc` mode the Identity REST API is not mapped) and attach `Authorization: Bearer <token>` via `CallCredentials` or a client interceptor. Like REST's `AuthorizeClientWithConsent`, they then call `UsersService.CreateConsents` for the latest documents (fetched through `LegalService`). Otherwise `LegalConsentInterceptor` blocks every module call in the test.

Arrange steps use gRPC calls — required, since in `Grpc` mode module REST routes are not mapped. Put small gRPC arrange helpers (`CreateHomeAsync`, `CreateBrokerAsync`, …) in `TestInfrastructure` so every project shares them.

### Shape: scenario tests, one per proto service

To keep the count low, group rpcs into **one scenario test per proto service** that walks the resource lifecycle and asserts each step's payload, e.g. for `DevicesService`:

```
CreateDevice → GetDevice (assert fields) → DisplayDevices (assert present)
→ UpdateDevice → GetDevice (assert changed) → DeleteDevice (assert success)
```

Rules:
- Every rpc in the module is called at least once on a success path. Track this with a checklist in the PR description (rpc → test).
- Split a scenario only when an rpc needs incompatible arrange state, not for readability.
- One owner-path assertion proves `UserRequestContext` resolves the caller under gRPC: after `CreateDevice`, the persisted owner equals the test user.
- Auth scenarios use the existing test-host mocks: `EmailServiceMock` captures the confirmation link and reset code (Register → ConfirmEmail → Login → Refresh; ForgotPassword → ResetPassword → Login).
- Expected size: roughly 1–3 tests per proto service, ~25 tests across all six projects.

### Live updates

`Senswave.LiveUpdates.EndTestsGrpc`: one test subscribes with a valid home, asserts `Initialized`, then publishes each of the four update kinds through the corresponding update service and asserts the matching typed `LiveUpdate` arrives — covering the whole `oneof` in a single test. Every stream read is bounded by a timeout and fails the test when it elapses; a hung suite is worse than a failing one.

### Architecture tests

`test/Shared/Senswave.ArchitectureTests` keeps every `IEndpoint` rule, since endpoints stay. Add gRPC rules: services live in a `Grpc` namespace, derive from a generated `*ServiceBase`, carry `[Authorize]`, and are `internal sealed`.

### Verification

1. `dotnet build Senswave.sln`
2. `dotnet test` for the touched module's REST EndTests (must be unchanged and green) and its new `EndTestsGrpc` project.
3. `test/Shared/Senswave.ArchitectureTests`.
4. Before the final Spec A PR: full `dotnet test` and coverage per CLAUDE.md. No test is deleted, so coverage should not drop. Report it.

## Delivery: one PR to `main` per step

Every step below is its own short-lived branch and its own PR to `main` (decided 2026-10-10). Each PR must leave `main` green, with `Transport:Mode=Rest` (the default) behaviorally identical to before. The author never merges their own PR; the owner reviews and merges, and the next step branches from the updated `main`.

| PR | Content |
|---|---|
| 1 | **Spec 0 remainder** — missing Users protos (`UsersService.UpdateSettings`, `legal.proto`, `auth.proto`, `account.proto`), `Senswave.Infrastructure.Grpc`, packages, telemetry, `ErrorType` guard test |
| 2 | **Switch** — `TransportOptions`, `INavigationEndpoint`, host wiring, `MapGrpcServices`, gRPC test environment and helpers |
| 3 | **Auth** — `IAccessTokenIssuer`, `AuthService`, `AccountService`, `Senswave.Users.EndTestsGrpc` (login scenarios) |
| 3b | **Users + Legal** — `UsersService` (4 rpcs), `LegalService`, `LegalConsentInterceptor`, tests added to `Senswave.Users.EndTestsGrpc` |
| 4 | **Automations** — 10 rpcs + `EndTestsGrpc` |
| 5 | **DataSources** — 14 rpcs + `EndTestsGrpc` |
| 6 | **Homes** — 18 rpcs + `EndTestsGrpc` |
| 7 | **Devices** — 28 rpcs + `EndTestsGrpc` |
| 8 | **LiveUpdates** — streaming path + `EndTestsGrpc`; full-suite coverage report |
| 9 | **Spec B** — gRPC-Web + `*.EndTestsGrpcWeb` projects |
| 10 | **Spec D** — `FailurePath`/`RateLimiting` tags, `docs/comparison.md` |

`Grpc` mode is incomplete on `main` between PRs 2 and 8 (unconverted modules simply have no gRPC surface yet). That is acceptable because the default stays `Rest`; say so in each of those PR descriptions.

Auth goes first. In `Grpc` mode the Identity REST API is not mapped, so every gRPC test logs in through `AuthService.Login`, and nothing else can be tested until it exists. The consent interceptor (3b) must exist before any module service is exposed. LiveUpdates goes last because its test publishes device updates.

*Implementation note (2026-10-11):* steps 1–8 were delivered as separate commits on one branch (`grpc/pr1-contracts-infrastructure`), by owner decision, in dependency order: 1, 2, 3, 3b, then **DataSources (5), Homes (6), Devices (7), Automations (4)**, then LiveUpdates (8). Automations moved after Devices because its tests need a home with a data source, a device, and operations, which in `Grpc` mode can only be created through those services. Known test gaps: `RestartClient` has no success-path test (it succeeds only for a client that lost its connection; the REST suite has none either), and the owner-path assertion is `GetHome.is_owner` because devices carry no owner of their own.

## Known gaps

- **gRPC methods are unthrottled**, auth and live-update subscriptions included. Accepted (2026-10-10); rate limiting can be disabled in any mode (Spec D).
- **No JSON transcoding.** In gRPC modes, non-gRPC clients have no path to module operations; they use a REST-mode deployment.
- **Two mapping layers per operation.** Each converted endpoint has a REST `*Extensions` and a `*GrpcExtensions`. They are thin and share handlers; the duplication is the price of the switch.
- **Live-update error semantics differ between modes** (silent return in SignalR, status codes in gRPC). Documented above.

## Acceptance criteria

- `Transport:Mode` selects `Rest` or `Grpc`. The default is `Rest`, and with the default the API is behaviorally identical to `main` before this work.
- In `Grpc` mode, the 76 `IEndpoint` operations with a gRPC equivalent and the 10 Identity API operations are served as gRPC methods. No module, users, legal, Google, or Identity REST route is mapped. Only `ConfirmEmailV2`, `MapApiPublicEndpoints` (`.well-known` files and `api/version`), diagnostics, and health remain on HTTP.
- gRPC tokens and REST tokens are interchangeable (same protectors).
- The consent check applies to gRPC module calls exactly as `LegalMiddleware` applies it to REST: users, auth, legal, and live updates are exempt in both.
- In `Rest` mode, no gRPC service is mapped.
- No REST endpoint, REST DTO, or SignalR code is deleted.
- Live updates: SignalR in `Rest`, `LiveUpdatesService.Subscribe` in `Grpc`. The four consumers are unchanged.
- The active mode is logged at startup and attached to telemetry as `senswave.transport`.
- Every gRPC service enforces authorization equivalent to its REST endpoint.
- No stream test can hang; every read is bounded.
- Every rpc is called successfully at least once by an `EndTestsGrpc` project; no failure-path or edge-case gRPC tests exist.
- `dotnet build Senswave.sln` is green; `dotnet test` is green; no existing test modified.
- Architecture tests assert both endpoint and gRPC service conventions.
- Coverage is reported.
