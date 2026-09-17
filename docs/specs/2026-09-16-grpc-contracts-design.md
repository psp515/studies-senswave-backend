# Spec 0 — gRPC Contracts and Plumbing

**Branch:** `main/grpc` (phase 1 — this spec lands first, then Spec A continues on the same branch)
**Depends on:** nothing
**Status:** design approved 2026-09-16

## Purpose

Introduce the protobuf contracts and the shared gRPC infrastructure that Spec A (gRPC services) and Spec B (gRPC-Web) both build on. This spec adds no user-visible behavior: after it lands, the solution still serves exactly the REST surface it serves today. Its success criterion is that `dotnet build Senswave.sln` and the full existing test suite stay green while the generated protobuf types and the shared interceptors become available to every module.

Splitting this out keeps the contract discussion separate from the service-implementation work. A reviewer of this branch is reviewing API shape, not handler wiring.

## Scope

In scope:

- A new shared project holding every `.proto` file and the generated C# types.
- Protobuf message and service definitions covering the 73 REST endpoints being converted, across five modules.
- The protobuf contract replacing the SignalR live-updates hub. The replacement is designed and executed in Spec A; only the contract is here.
- A new shared project holding gRPC cross-cutting infrastructure: error mapping, request context, telemetry registration.
- Central package registration for the gRPC dependency set.

Out of scope (deliberately):

- Implementing any gRPC service. That is Spec A.
- Removing or changing any existing REST endpoint. That is Spec A.
- gRPC-Web transport. That is Spec B.
- Rate limiting for gRPC methods. Known gap, documented below.
- Everything about the SignalR replacement other than the proto file: the streaming service, the subscription registry that replaces SignalR groups, deleting the hub, and removing the SignalR packages. All of that is designed and executed in Spec A.

## Endpoint inventory

The proto surface must cover these, derived by enumerating `IEndpoint` implementations:

| Module | `IEndpoint` today | Converted | Stays REST |
|---|---|---|---|
| `Senswave.Devices.Api` | 28 | 28 | 0 |
| `Senswave.Homes.Api` | 18 | 18 | 0 |
| `Senswave.DataSources.Api` | 14 | 14 | 0 |
| `Senswave.Automations.Api` | 10 | 10 | 0 |
| `Senswave.Users.Api` | 8 | 3 | 5 |
| **Total** | **78** | **73** | **5** |

The five Users endpoints that stay REST are:

- `Auth/ConfirmEmailV2/ConfirmEmailV2Endpoint.cs`
- `Auth/Google/GoogleAuthEndpoint.cs`
- `Auth/GoogleV2/GoogleJwtAuthEndpoint.cs`
- `Legal/GetPrivacyPolicy/GetPrivacyPolicyEndpoint.cs` (`IPublicEndpoint`)
- `Legal/GetTermsAndConditions/GetTermsAndConditionsEndpoint.cs` (`IPublicEndpoint`)

The three converted Users endpoints are `Users/CreateConsents`, `Users/DeleteAccount`, and `Users/GetUser`.

Separately, `UsersModuleExtensions.UseAuthEndpoints` maps the ASP.NET Identity API via `MapIdentityApi<User>()` under `api/v1/auth`. Those endpoints are not `IEndpoint` implementations, are not counted above, and stay REST. They are framework-generated and have no gRPC equivalent.

`Senswave.LiveUpdates.Api` implements no `IEndpoint`, so it appears nowhere in this table. It exposes a SignalR hub instead, which Spec A replaces with the `LiveUpdatesService` streaming contract defined here.

## Deliverable 1 — `src/Shared/Senswave.Contracts`

A new `net10.0` class library, added to `Senswave.sln`, targeting `Debug;Release;Tests` configurations to match sibling projects.

Layout:

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
    users/v1/users.proto
    liveupdates/v1/liveupdates.proto
```

The Devices module is split across several proto files because it carries 28 endpoints under five distinct tags (`Devices`, `Dashboards`, `Widgets`, `Operations`, `Sharing`, per the constants on `DevicesModule`). One proto file per tag keeps each file small enough to read in one sitting. Other modules get a single file unless they exceed roughly 15 rpcs, in which case split them the same way.

The csproj registers every proto with `GrpcServices="Both"`, so a single compilation produces both the server base classes that Spec A derives from and the typed clients that the EndTests and Spec B consume. This avoids a second generation pass and guarantees client and server never drift.

```xml
<ItemGroup>
  <Protobuf Include="Protos/**/*.proto" GrpcServices="Both" ProtoRoot="Protos" />
</ItemGroup>
```

### Proto conventions

These conventions are binding; the implementation plan should treat a violation as a review blocker.

- **Package naming:** `senswave.<module>.v1` — for example `senswave.devices.v1`. The version lives in the package, which is how this surface replaces `Asp.Versioning`. The REST surface that survives keeps `Asp.Versioning` unchanged.
- **C# namespace:** set `option csharp_namespace = "Senswave.Contracts.Devices.V1";` on every file so generated types do not collide with the hand-written module types.
- **Service naming:** one service per proto file, named after the tag — `DevicesService`, `DashboardsService`, `HomesService`, and so on.
- **Rpc naming:** the existing feature folder name, unchanged. `CreateDevice`, `DisplayDashboards`, `SetWidgetOnDashboard`. This keeps a one-to-one trace from proto to the existing `IEndpoint` class and to the MediatR command it dispatches.
- **Message naming:** `<Rpc>Request` and `<Rpc>Response`. Every rpc declares both, even when one side is empty, so fields can be added later without a signature change.
- **Field derivation:** fields come from the existing `*Request` / `*Response` DTOs in each module's `Api` project. `AGENTS.md` forbids renaming or removing fields on the REST contract; the proto surface is a **new** contract, so it is free to use idiomatic protobuf naming (`lower_snake_case` fields). Where a name changes, the mapping is explicit in the `*Extensions` class that Spec A writes — the same pattern the REST DTOs already use.
- **Identifiers:** `Guid` maps to `string` carrying the canonical `D` format. Do not invent a custom UUID message.
- **Timestamps:** `google.protobuf.Timestamp`.
- **Optional values:** use `optional` on proto3 scalar fields where the REST DTO is nullable, so the distinction between "absent" and "default" survives.
- **Enums:** mirror the domain enums, with the zero value suffixed `_UNSPECIFIED`.

### `common/v1/common.proto`

Holds types shared across modules so they are defined once:

- `IdResponse` — a single `string id`, matching the existing `IdResponse` used throughout the EndTests.
- `Error` and `ErrorType` — mirroring `Senswave.Abstractions.Resulting.Error` and `ErrorType`, used in the error trailer described below.
- `Empty` is **not** redefined; use `google.protobuf.Empty`.

### `liveupdates/v1/liveupdates.proto`

The one streaming contract in the set, replacing the SignalR hub:

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

Two properties of this contract are binding on the rest of the effort, so they are settled here rather than in Spec A.

**Server streaming only.** gRPC-Web supports unary and server-streaming calls, and nothing else — client streaming and bidirectional streaming do not work in any browser. Spec B exists for browser reach, so no rpc anywhere in this contract set may use client or bidirectional streaming. This is a transport limit, not a preference.

The hub's shape already fits. `LiveUpdatesHub.Initialize(homeReferenceId)` is called once per home and re-invoked "each time someone switches home in app", per its own documentation. A single server-streaming call carrying the home id says the same thing: switching home means cancelling the stream and opening a new one.

**The `oneof` replaces `Update(string updateType, JsonNode data)`.** Today the update kind is a magic string — `"widgetsActionUpdate"`, `"deviceTileActionUpdate"`, `"devicePresenceUpdate"`, `"dataSourceStateUpdate"` — and the payload is an untyped `JsonObject` built by hand in `DevicesUpdateService` and `DataSourcesUpdateService`. Field shapes above come directly from that existing construction: `UpdateWidgets` sends `deviceId` plus an `items` array of widget id strings; `UpdateDeviceTile` and `UpdateDevicePresence` send `deviceId` alone; `UpdateDataSourceState` sends `dataSourceId` and `state`.

## Deliverable 2 — `src/Shared/Senswave.Infrastructure.Grpc`

A new `net10.0` class library, sibling to the existing `Senswave.Infrastructure.Web`. It exists so that Spec A's module projects depend on gRPC plumbing without depending on the web host, matching how `Senswave.Infrastructure.Web` is structured today.

### `ErrorHandlingInterceptor`

A server interceptor that translates the existing `Result` failure model into gRPC status codes. Today `ResultExtensions.ToResultsDetails` builds an `ErrorProblemDetails` from `ErrorType` via `ErrorTypeExtensions.GetStatusCode`. The gRPC equivalent maps the same enum to `StatusCode`:

| `ErrorType` | HTTP today | gRPC `StatusCode` |
|---|---|---|
| `Validation` | 400 | `InvalidArgument` |
| `NotFound` | 404 | `NotFound` |
| `Conflict` | 409 | `AlreadyExists` |
| `Failure` | 400 | `FailedPrecondition` |
| `ServerFail` | 500 | `Internal` |

`Failure` maps to `FailedPrecondition` rather than `InvalidArgument` because the existing code uses it for business-rule refusals — for example a user without the `Manage` privilege attempting a write — not for malformed input. Keeping the two distinct preserves information the HTTP surface loses by collapsing both to 400.

The interceptor:

1. Wraps `UnaryServerHandler`.
2. Catches `SenswaveRpcException` (a thin exception carrying `Result` errors, defined in this project) and converts it to an `RpcException` whose `Status.Detail` is the error title and whose trailers carry the serialized `Error[]` plus the current trace id, mirroring the `errors` and `traceId` fields of `ErrorProblemDetails`.
3. Lets any other exception fall through to the existing `GlobalExceptionHandlerMiddleware` behavior — do not swallow unknown exceptions here.

Provide `ResultGrpcExtensions.ThrowIfFailure(this Result result)` so service methods read the same way the endpoints read today:

```csharp
var result = await mediator.Send(command);
result.ThrowIfFailure();
return result.ToCreateDeviceResponse();
```

### `GrpcRequestContext`

An `IRequestContext` implementation reading the authenticated user from `ServerCallContext.GetHttpContext().User`, mirroring the existing `UserRequestContext` in `Senswave.Infrastructure.Web/Contexts`. Register it so that module handlers, which already depend on `IRequestContext`, work unchanged under gRPC. Read `UserRequestContext` first and reuse its claim-reading logic rather than reimplementing it — if the logic is non-trivial, lift the shared part into `Senswave.Abstractions` instead of duplicating it.

### `GrpcInfrastructureExtensions`

`AddGrpcInfrastructure(this IServiceCollection services)` registering the interceptor and the request context. Spec A calls it from `Startup.ConfigureServices`. This spec adds the method but does not call it, so the registration is dead code on this branch — that is intentional and should be noted in the PR description.

### Telemetry

`AGENTS.md` asks for OpenTelemetry in custom code. Server-side gRPC spans already arrive through the existing `OpenTelemetry.Instrumentation.AspNetCore` registration, because ASP.NET Core gRPC is ASP.NET Core routing. Add `OpenTelemetry.Instrumentation.GrpcNetClient` to the tracing builder in `src/Presentation/Senswave.Presentation.Api/Diagnostics/DiagnosticsExtensions.cs` so client-side calls — which the EndTests in Spec A make — produce spans too.

Additionally, add an `ActivitySource` in `Senswave.Infrastructure.Grpc` and start an activity in `ErrorHandlingInterceptor` around the handler, tagging the mapped `ErrorType` when a failure is converted. This is the one piece of gRPC behavior that is genuinely custom code rather than framework plumbing, so it is where custom instrumentation earns its place.

## Deliverable 3 — package registration

Add to `Directory.Packages.props` under a new `<!-- gRPC -->` comment block. Approved on 2026-09-16; `AGENTS.md` otherwise forbids new libraries.

| Package | Used by |
|---|---|
| `Grpc.AspNetCore` | Spec A server hosting |
| `Grpc.Tools` | codegen in `Senswave.Contracts` |
| `Google.Protobuf` | generated types |
| `Grpc.Net.Client` | EndTest clients |
| `Grpc.AspNetCore.Web` | Spec B |
| `Grpc.Net.Client.Web` | Spec B EndTest clients |
| `OpenTelemetry.Instrumentation.GrpcNetClient` | client spans |

Pin versions explicitly, as every other entry in that file does. Do not upgrade any unrelated package while editing this file — `AGENTS.md` forbids incidental upgrades.

## Testing

This spec ships no behavior, so it ships no new behavioral tests. Verification is:

1. `dotnet build Senswave.sln` succeeds, including generated protobuf sources.
2. The existing test suite passes unchanged: `dotnet test` across all modules.
3. `test/Shared/Senswave.ArchitectureTests` still passes. If any existing rule asserts something about the shape of `src/Shared` projects, extend it to cover the two new projects rather than exempting them.

Add one unit test project concern to `Senswave.ArchitectureTests` (or the nearest existing home): assert that every `ErrorType` enum value has a gRPC status mapping, so adding a new `ErrorType` later fails the build rather than throwing `InvalidEnumArgumentException` at runtime. The existing `ErrorTypeExtensions` has the same hazard; matching its structure is fine, but the new mapping should be guarded.

## Known gaps carried forward

- **Rate limiting does not apply to gRPC.** `UserRateLimiter` and `AnonymousRateLimiter` attach to endpoints through `RequireRateLimiting`, which gRPC method routing does not use. Once Spec A moves module endpoints to gRPC, those methods are unthrottled. The REST endpoints that survive — auth, public, diagnostics — keep `AnonymousRateLimiter`. Accepted deliberately on 2026-09-16.
- **No JSON transcoding.** Clients that speak neither gRPC nor gRPC-Web have no path to the converted endpoints after Spec A.

## Acceptance criteria

- `src/Shared/Senswave.Contracts` builds and generates both server and client types for all 73 converted endpoints, plus the `LiveUpdatesService` streaming contract.
- `LiveUpdatesService` uses server streaming only, with no client-streaming or bidirectional rpc anywhere in the contract set.
- `src/Shared/Senswave.Infrastructure.Grpc` builds and exposes `ErrorHandlingInterceptor`, `GrpcRequestContext`, `ResultGrpcExtensions`, and `AddGrpcInfrastructure`.
- `Directory.Packages.props` carries the seven packages with pinned versions and no unrelated changes.
- `dotnet build Senswave.sln` is green.
- `dotnet test` is green with no test modified other than additions.
- Every `ErrorType` value has an asserted gRPC status mapping.
