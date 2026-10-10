# Spec 0 — gRPC Contracts and Plumbing

**Branch:** one short-lived branch per step, each merged to `main` by its own PR after owner review — see Spec A, "Delivery"
**Depends on:** nothing
**Status:** design approved 2026-09-16; revised 2026-10-10 for the single-branch, config-switched transport design. Contracts for 73 operations landed in `605afa2`. The Users proto additions, Deliverables 2–3, and the guard test remain, and all of them ship together as PR 1.

## Purpose

Introduce the protobuf contracts and the shared gRPC infrastructure that Spec A (gRPC services) and Spec B (gRPC-Web) both build on. This spec adds no user-visible behavior: after it lands, the solution still serves exactly the REST surface it serves today. Its success criterion is that `dotnet build Senswave.sln` and the full existing test suite stay green while the generated protobuf types and the shared interceptors become available to every module.

Splitting this out keeps the contract discussion separate from the service-implementation work. A reviewer of this spec is reviewing API shape, not handler wiring.

## Transport switch (context for all four specs)

Senswave serves REST, gRPC, and gRPC-Web from **one codebase on one branch**. Which surface is live is chosen at startup by configuration (`Transport:Mode`, overridable as the environment variable `Transport__Mode`), defined in Spec A. Nothing is deleted to make room for gRPC: the REST endpoints, REST DTOs, and the SignalR hub all stay. This replaces the earlier plan of separate `main/grpc`, `main/grpc-web`, and never-merged `main/rest` branches. A git branch named `main/grpc` cannot coexist with `main` anyway.

Consequences for this spec: none to the contracts. The infrastructure below must be safe to register in every mode, because the host registers it unconditionally and only maps gRPC services when gRPC is selected.

## Scope

In scope:

- A new shared project holding every `.proto` file and the generated C# types. **Done (`605afa2`).**
- Protobuf definitions for every operation with a gRPC equivalent. **Mostly done.** `605afa2` covers 73 operations. The Users additions below (`UpdateSettings`, legal, auth, account management) are part of this spec's remaining work.
- The protobuf contract for the gRPC live-updates stream, the gRPC counterpart of the SignalR hub. **Done.**
- A new shared project holding gRPC cross-cutting infrastructure: error mapping and telemetry.
- Central package registration for the gRPC dependency set.

Out of scope (deliberately):

- Implementing any gRPC service, and the transport switch itself. That is Spec A.
- gRPC-Web transport. That is Spec B.
- Rate limiting for gRPC methods. Known gap, documented below.

## Endpoint inventory

*Revised 2026-10-10.* Every API operation clients use gets a gRPC equivalent, including legal documents and the ASP.NET Identity API. There are two exceptions. **Browser navigation targets** (URLs a user opens from an email, which cannot make gRPC calls) stay HTTP in every mode. **Google login** is not used and gets no gRPC equivalent.

The host's `MapApiPublicEndpoints` (`.well-known` app-link files and `GET api/version`) is not `IEndpoint`. It stays HTTP in every mode and gets no gRPC equivalent.

`IEndpoint` implementations (recounted 2026-10-10; the original count missed `Users/UpdateSettings`):

| Module | `IEndpoint` | gRPC equivalent | REST in every mode |
|---|---|---|---|
| `Senswave.Devices.Api` | 28 | 28 | 0 |
| `Senswave.Homes.Api` | 18 | 18 | 0 |
| `Senswave.DataSources.Api` | 14 | 14 | 0 |
| `Senswave.Automations.Api` | 10 | 10 | 0 |
| `Senswave.Users.Api` | 9 | 6 | 3 |
| **Total** | **79** | **76** | **3** |

Users, endpoint by endpoint:

| Endpoint | gRPC |
|---|---|
| `Users/GetUser`, `Users/CreateConsents`, `Users/DeleteAccount` | `UsersService` (landed) |
| `Users/UpdateSettings` | `UsersService.UpdateSettings` (**to add**) |
| `Legal/GetPrivacyPolicy`, `Legal/GetTermsAndConditions` (`IPublicEndpoint`) | `LegalService` (**to add**), anonymous |
| `Auth/Google/GoogleAuthEndpoint`, `Auth/GoogleV2/GoogleJwtAuthEndpoint` | **None.** Google login is not used by clients (decided 2026-10-10). REST-only, and not mapped in gRPC modes. |
| `Auth/ConfirmEmailV2/ConfirmEmailV2Endpoint` | **REST in every mode.** It is the link in the confirmation email and answers with a redirect. A gRPC `AuthService.ConfirmEmail` also exists for clients that capture the link via deep link. |

### ASP.NET Identity API

`UsersModuleExtensions.UseAuthEndpoints` maps `MapIdentityApi<User>()` under `api/v1/auth`. These are framework-generated, not `IEndpoint`. Each gets a gRPC equivalent, implemented in Spec A by porting the framework's logic onto `UserManager` / `SignInManager`:

| Identity route | gRPC |
|---|---|
| `POST /register` | `AuthService.Register` |
| `POST /login` (bearer only; cookie modes not ported) | `AuthService.Login` |
| `POST /refresh` | `AuthService.Refresh` |
| `GET /confirmEmail` | `AuthService.ConfirmEmail`; the REST route is also a navigation target (see Spec A) |
| `POST /resendConfirmationEmail` | `AuthService.ResendConfirmationEmail` |
| `POST /forgotPassword` | `AuthService.ForgotPassword` |
| `POST /resetPassword` | `AuthService.ResetPassword` |
| `POST /manage/2fa` | `AccountService.TwoFactor` |
| `GET /manage/info` | `AccountService.GetInfo` |
| `POST /manage/info` | `AccountService.UpdateInfo` |

`Senswave.LiveUpdates.Api` implements no `IEndpoint`. It exposes a SignalR hub, which Spec A keeps for REST mode and pairs with the `LiveUpdatesService` streaming contract for gRPC modes.

## Deliverable 1 — `src/Shared/Senswave.Contracts` (mostly done)

A `net10.0` class library in `Senswave.sln`, targeting `Debug;Release;Tests`.

Layout (landed, plus the Users additions marked):

```
src/Shared/Senswave.Contracts/
  Senswave.Contracts.csproj
  Protos/
    common/v1/common.proto
    automations/v1/automations.proto
    datasources/v1/datasources.proto
    devices/v1/devices.proto
    devices/v1/dashboards.proto
    devices/v1/widgets.proto
    devices/v1/operations.proto
    devices/v1/sharing.proto
    homes/v1/homes.proto
    homes/v1/rooms.proto
    homes/v1/sharing.proto
    users/v1/users.proto        (add UpdateSettings)
    users/v1/legal.proto        (to add: LegalService)
    users/v1/auth.proto         (to add: AuthService)
    users/v1/account.proto      (to add: AccountService)
    liveupdates/v1/liveupdates.proto
```

Devices is split per tag (`Devices`, `Dashboards`, `Widgets`, `Operations`, `Sharing`). Homes, at 18 rpcs, is split the same way (`homes`, `rooms`, `sharing`) under the "more than roughly 15 rpcs" rule.

The csproj registers every proto with `GrpcServices="Both"`, so one compilation produces the server base classes Spec A derives from and the typed clients the EndTests and Spec B consume.

```xml
<ItemGroup>
  <Protobuf Include="Protos/**/*.proto" GrpcServices="Both" ProtoRoot="Protos" />
</ItemGroup>
```

### Proto conventions

These conventions are binding; treat a violation as a review blocker.

- **Package naming:** `senswave.<module>.v1`. The version lives in the package. The REST surface keeps `Asp.Versioning` unchanged.
- **C# namespace:** `option csharp_namespace = "Senswave.Contracts.<Module>.V1";` on every file, so generated types do not collide with hand-written module types.
- **Service naming:** one service per proto file, named after the tag: `DevicesService`, `DashboardsService`, `HomesService`, and so on.
- **Rpc naming:** the existing feature folder name, unchanged: `CreateDevice`, `DisplayDashboards`, `SetWidgetOnDashboard`. This keeps a one-to-one trace from proto to `IEndpoint` class to MediatR command.
- **Message naming:** `<Rpc>Request` and `<Rpc>Response`. Every rpc declares both, even when one side is empty.
- **Field derivation:** fields come from the existing REST `*Request` / `*Response` DTOs. The CLAUDE.md rule against renaming REST DTO fields applies to the REST DTOs, which stay. The proto surface is a separate contract, so it uses idiomatic `lower_snake_case`. Where a name differs, the mapping is explicit in the gRPC `*Extensions` class Spec A writes.
- **Identifiers:** `Guid` maps to `string` in canonical `D` format. No custom UUID message.
- **Timestamps:** `google.protobuf.Timestamp`.
- **Optional values:** `optional` on proto3 scalars where the REST DTO is nullable.
- **Enums:** mirror the domain enums, with the zero value suffixed `_UNSPECIFIED`.

### `common/v1/common.proto`

- `IdResponse`: a single `string id`.
- `Error` and `ErrorType`: mirroring `Senswave.Abstractions.Resulting.Error` and `ErrorType`, used in the error trailer described below.
- `Empty` is not redefined; use `google.protobuf.Empty`.

### `liveupdates/v1/liveupdates.proto`

The one streaming contract:

```proto
service LiveUpdatesService {
  rpc Subscribe(SubscribeRequest) returns (stream LiveUpdate);
}

message SubscribeRequest {
  string home_reference_id = 1;
}

message LiveUpdate {
  oneof update {
    Initialized initialized = 1;
    DeviceTileUpdate device_tile = 2;
    WidgetsUpdate widgets = 3;
    DevicePresenceUpdate device_presence = 4;
    DataSourceStateUpdate data_source_state = 5;
  }
}

message Initialized {}
message DeviceTileUpdate { string device_id = 1; }
message WidgetsUpdate { string device_id = 1; repeated string widget_ids = 2; }
message DevicePresenceUpdate { string device_id = 1; }
message DataSourceStateUpdate { string data_source_id = 1; string state = 2; }
```

**Server streaming only.** gRPC-Web supports unary and server-streaming calls only. No rpc anywhere in the contract set may use client or bidirectional streaming. This is a transport limit, not a preference.

**The `oneof` is the typed counterpart of `Update(string updateType, JsonNode data)`.** The SignalR hub keeps its magic-string update kinds in REST mode. The field shapes above come directly from the `JsonObject` construction in `DevicesUpdateService` and `DataSourcesUpdateService`.

### Users additions: `legal.proto`, `auth.proto`, `account.proto`, `UpdateSettings`

All are in package `senswave.users.v1`. Fields derive from the REST DTOs (`GetPrivacyPolicyResponse`, `GetTermsAndConditionsResponse`, `UpdateSettingsRequest`) and from the Identity API DTOs in `Microsoft.AspNetCore.Identity.Data` (`RegisterRequest`, `LoginRequest`, `RefreshRequest`, `ResendConfirmationEmailRequest`, `ForgotPasswordRequest`, `ResetPasswordRequest`, `TwoFactorRequest`/`TwoFactorResponse`, `InfoRequest`/`InfoResponse`), using the same conventions as above.

- **`LegalService`**: `GetPrivacyPolicy` and `GetTermsAndConditions`, each returning `id`, `version`, `summary`, `content`, and `created_at_utc` (`Timestamp`).
- **`AuthService`**: `Register`, `Login`, `Refresh`, `ConfirmEmail`, `ResendConfirmationEmail`, `ForgotPassword`, `ResetPassword`.
- **`AccountService`** (authenticated): `GetInfo`, `UpdateInfo`, `TwoFactor`.
- **`UsersService.UpdateSettings`**: added to the existing service.

`Login` and `Refresh` return one shared message mirroring the framework's `AccessTokenResponse`:

```proto
message AccessTokenResponse {
  string token_type = 1;      // "Bearer"
  string access_token = 2;
  int64 expires_in = 3;       // seconds
  string refresh_token = 4;
}
```

`Login` carries optional `two_factor_code` and `two_factor_recovery_code`, as Identity's `LoginRequest` does. The Identity API's cookie modes (`useCookies`, `useSessionCookies`) have no gRPC equivalent and are not modelled.

Each new file has a single service named after the file, following the one-service-per-file convention.

## Deliverable 2 — `src/Shared/Senswave.Infrastructure.Grpc`

A new `net10.0` class library, sibling to `Senswave.Infrastructure.Web`, added to `Senswave.sln` with `Debug;Release;Tests` configurations. Module `Api` projects depend on it for gRPC plumbing.

### `ErrorHandlingInterceptor`

A server interceptor translating the `Result` failure model into gRPC status codes. The mapping parallels `ErrorTypeExtensions.GetStatusCode`:

| `ErrorType` | HTTP | gRPC `StatusCode` |
|---|---|---|
| `Validation` | 400 | `InvalidArgument` |
| `NotFound` | 404 | `NotFound` |
| `Conflict` | 409 | `AlreadyExists` |
| `Failure` | 400 | `FailedPrecondition` |
| `ServerFail` | 500 | `Internal` |

`Failure` maps to `FailedPrecondition` because the code uses it for business-rule refusals, such as a user without the `Manage` privilege attempting a write, not for malformed input.

The interceptor:

1. Wraps `UnaryServerHandler` and `ServerStreamingServerHandler`.
2. Catches `SenswaveRpcException`, a thin exception carrying `Result` errors defined in this project. It converts the exception to an `RpcException`: `Status.Detail` is the error title, and the trailers carry the serialized `Error[]` (as `common.v1.Error`, binary `-bin` trailer) plus the current trace id. These mirror the `errors` and `traceId` fields of `ErrorProblemDetails`.
3. Catches every other exception itself. Exceptions thrown in a gRPC service never reach ASP.NET Core's `UseExceptionHandler` / `GlobalExceptionHandlerMiddleware`, because the gRPC framework catches them first and returns `Unknown` without logging them. The interceptor is therefore the gRPC equivalent of `GlobalExceptionHandlerMiddleware`. It logs the exception with the same log line, marks the activity as errored, and throws `RpcException(StatusCode.Internal, "Server Error")` with the trace id in the trailers. The exception message is **not** sent to the client; this deliberately differs from REST, which returns `exception.Message`. `RpcException`s thrown deliberately (for example by the consent interceptor or the live-updates checks) pass through untouched.

Enable `GrpcServiceOptions.EnableDetailedErrors` only in Development, mirroring `UseDeveloperExceptionPage`.

`ResultGrpcExtensions.ThrowIfFailure(this Result result)` lets service methods read like the endpoints:

```csharp
var result = await mediator.Send(command);
result.ThrowIfFailure();
return result.ToCreateDeviceResponse();
```

### Request context: reuse `UserRequestContext`, no `GrpcRequestContext`

*Revised.* The original design added a `GrpcRequestContext`. Under the config switch both surfaces share one DI container, so a second `IRequestContext` registration would conflict with the REST one. It is also unnecessary: ASP.NET Core gRPC runs inside the HTTP pipeline, so `IHttpContextAccessor.HttpContext` is populated for gRPC calls. The existing `UserRequestContext` (`Senswave.Infrastructure.Web/Contexts`) therefore returns the correct user id under every transport.

This spec adds no new request context. Spec A's EndTests prove the claim through a success-path assertion that the authenticated user is recorded as owner (the gRPC port of `OwnerOfDeviceIsSetCorrectly`).

### `GrpcInfrastructureExtensions`

`AddGrpcInfrastructure(this IServiceCollection services)` calls `services.AddGrpc(o => o.Interceptors.Add<ErrorHandlingInterceptor>())` and registers the interceptor's dependencies. Spec A calls it from `Startup.ConfigureServices` in gRPC modes only. It accepts an optional `Action<GrpcServiceOptions>` so the host can append further interceptors, such as Users' `LegalConsentInterceptor`. This spec adds the method without calling it, so the registration is dead code until Spec A lands. Note that in the PR description.

### Telemetry

Server-side gRPC spans already arrive through `OpenTelemetry.Instrumentation.AspNetCore`. Add `OpenTelemetry.Instrumentation.GrpcNetClient` to the tracing builder in `src/Presentation/Senswave.Presentation.Api/Diagnostics/DiagnosticsExtensions.cs`, so client-side calls (which the gRPC EndTests make) produce spans.

Add an `ActivitySource` in `Senswave.Infrastructure.Grpc` and start an activity in `ErrorHandlingInterceptor` around the handler. Tag the mapped `ErrorType` and gRPC status when a failure is converted, and register the source name with the tracing builder.

## Deliverable 3 — package registration

Add to `Directory.Packages.props` under the `<!-- gRPC -->` block (approved 2026-09-16):

| Package | Used by | State |
|---|---|---|
| `Grpc.AspNetCore` | server hosting | present |
| `Grpc.Tools` | codegen in `Senswave.Contracts` | present |
| `Google.Protobuf` | generated types | present |
| `Grpc.Net.Client` | EndTest clients | present |
| `Grpc.AspNetCore.Web` | Spec B | **to add** |
| `Grpc.Net.Client.Web` | Spec B EndTest clients | **to add** |
| `OpenTelemetry.Instrumentation.GrpcNetClient` | client spans | **to add** |

Pin versions explicitly, choosing versions compatible with the existing `Grpc.*` 2.83 line and the existing OpenTelemetry packages. Do not upgrade any unrelated package.

## Testing

This spec ships no behavior. Verification is:

1. `dotnet build Senswave.sln` succeeds, including generated protobuf sources.
2. The existing test suite passes unchanged.
3. `test/Shared/Senswave.ArchitectureTests` still passes. If an existing rule asserts the shape of `src/Shared` projects, extend it to the two new projects rather than exempting them.

Add one guard test, in `Senswave.ArchitectureTests` or the nearest existing home: every `ErrorType` value other than `None` has a gRPC status mapping. Adding a new `ErrorType` later then fails the build instead of throwing at runtime.

## Known gaps carried forward

- **Rate limiting does not apply to gRPC.** `UserRateLimiter` and `AnonymousRateLimiter` attach through `RequireRateLimiting`, which gRPC method routing does not use, so every method is unthrottled in gRPC modes. Accepted, auth included (2026-10-10); rate limiting can be disabled in any mode anyway.
- **No JSON transcoding.** In gRPC modes, clients that speak neither gRPC nor gRPC-Web have no path to module operations. They can use REST mode instead.

## Acceptance criteria

- `src/Shared/Senswave.Contracts` builds and generates server and client types for all 76 `IEndpoint` operations with a gRPC equivalent, the 10 Identity API equivalents, and `LiveUpdatesService`.
- `LiveUpdatesService` uses server streaming only; no client-streaming or bidirectional rpc exists. *(met)*
- `src/Shared/Senswave.Infrastructure.Grpc` builds and exposes `ErrorHandlingInterceptor`, `SenswaveRpcException`, `ResultGrpcExtensions`, and `AddGrpcInfrastructure`.
- `Directory.Packages.props` carries the seven packages with pinned versions and no unrelated changes.
- gRPC client instrumentation and the interceptor's `ActivitySource` are registered with tracing.
- `dotnet build Senswave.sln` is green.
- `dotnet test` is green, with no test modified other than additions.
- Every `ErrorType` value except `None` has an asserted gRPC status mapping.
