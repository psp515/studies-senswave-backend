# Spec A — gRPC Services

**Branch:** `main/grpc` (phase 2 — continues on the branch after Spec 0 lands)
**Depends on:** Spec 0 (gRPC contracts and plumbing)
**Status:** design approved 2026-09-16

## Purpose

Replace the REST surface of the five feature modules with gRPC services backed by the protobuf contracts from Spec 0. After this spec, module operations are reached over gRPC; only authentication, the public legal documents, diagnostics, and health remain REST.

The handlers themselves do not change. Every module already routes through MediatR — `CreateDeviceEndpoint` builds a `CreateDeviceCommand` and sends it. This spec swaps the transport and the mapping layer around that call, and nothing below it.

## Scope

In scope:

- One gRPC service implementation per proto service, in the owning module's `Api` project.
- Proto-to-command and result-to-proto mapping, following the existing `*Extensions` convention.
- Deletion of the `IEndpoint` implementations and REST DTOs for the 73 converted endpoints.
- Host wiring in `Senswave.Presentation.Api`.
- Rewriting the module EndTests against gRPC clients, success paths only.
- Replacing the SignalR live-updates hub with a `LiveUpdatesService` streaming service, against the contract Spec 0 defines.

Out of scope:

- The five Users endpoints that stay REST — `ConfirmEmailV2Endpoint`, `GoogleAuthEndpoint`, `GoogleJwtAuthEndpoint`, `GetPrivacyPolicyEndpoint`, `GetTermsAndConditionsEndpoint` — plus the `MapIdentityApi<User>()` surface under `api/v1/auth`, `DiagnosticModule`, health checks, and `PublicModule`. See Spec 0 for the full inventory.
- Unit tests and integration tests. Neither touches transport, so neither changes. The one exception is any LiveUpdates unit test that constructs `IHubContext`; see "Live updates" below.
- Rate limiting for gRPC methods. See "Known gaps".
- gRPC-Web. That is Spec B.

## Architecture

### Service implementations

Each module's `Api` project gains a `Grpc/` folder. One class per proto service:

```
src/Modules/Devices/Senswave.Devices.Api/Grpc/
  DevicesGrpcService.cs
  DashboardsGrpcService.cs
  WidgetsGrpcService.cs
  OperationsGrpcService.cs
  SharingGrpcService.cs
```

A service derives from the generated base and carries the same authorization attribute the endpoints carry today:

```csharp
[Authorize(AuthenticationSchemes = "Identity.Bearer")]
internal sealed class DevicesGrpcService(IMediator mediator, IRequestContext context)
    : DevicesService.DevicesServiceBase
{
    public override async Task<DeviceCreatedResponse> CreateDevice(
        CreateDeviceRequest request, ServerCallContext callContext)
    {
        var command = request.ToCommand(context.UserId);
        var result = await mediator.Send(command);
        result.ThrowIfFailure();
        return result.ToDeviceCreatedResponse();
    }
}
```

`[Authorize]` on the class matches today's per-method attribute placement on the endpoints. Where an endpoint today has no `[Authorize]` but is nonetheless reached through the authenticated group in `Startup`, the gRPC service still needs the attribute — gRPC has no equivalent of the `MapGroup` inheritance the REST surface relies on. Audit each endpoint's effective authorization before writing its service method; do not assume the attribute's presence in source tells the whole story.

Grouping rule: one service class per proto service, and proto services follow the module's existing tag constants (`DevicesModule.DevicesTag`, `DashboardsTag`, and so on). If a service class grows past roughly 400 lines, split the proto service rather than letting the class sprawl.

### Mapping

Keep the existing convention. Today each feature folder holds `CreateDeviceEndpoint.cs`, `CreateDeviceRequest.cs`, `DeviceCreatedResponse.cs`, and `CreateDeviceExtensions.cs`. After conversion the request and response types are generated, so the folder holds only the extensions:

```
src/Modules/Devices/Senswave.Devices.Api/Devices/CreateDevice/
  CreateDeviceExtensions.cs
```

`CreateDeviceExtensions` keeps both directions, as it does now:

```csharp
internal static CreateDeviceCommand ToCommand(this CreateDeviceRequest dto, Guid userId) => new()
{
    HomeId = Guid.Parse(dto.HomeId),
    RoomId = ...,
    Icon = dto.Icon,
    Name = dto.Name,
    UserId = userId,
};

internal static DeviceCreatedResponse ToDeviceCreatedResponse(this Result<Device> result) => new()
{
    Id = result.Data.Id.ToString(),
};
```

Guid and timestamp conversion lives here and nowhere else. Do not scatter `Guid.Parse` through service methods.

Delete the REST `*Request` and `*Response` classes for converted endpoints. The `AGENTS.md` rule that REST request DTOs are a frozen public contract applies to DTOs that remain; a deleted endpoint has no contract to preserve. Call this out in the PR description, since it reads as a violation at a glance.

`Senswave.Users.Api` is the one module where both surfaces coexist. Its `Auth/` and `Legal/` folders keep their endpoints and DTOs untouched; only `Users/CreateConsents`, `Users/DeleteAccount`, and `Users/GetUser` convert. Do not delete the module's `IEndpoint` registration or its Swagger document.

### Live updates — replacing SignalR

`Senswave.LiveUpdates.Api` exposes a SignalR hub at `signalr/liveupdates/live`. This spec replaces it with the `LiveUpdatesService` server-streaming rpc whose contract Spec 0 defines, so live updates travel the same transport as everything else.

Server streaming is not a stylistic choice. gRPC-Web supports unary and server-streaming calls only, so a design where the client sends more than one message per call would be unreachable from a browser and would break Spec B. Spec 0 states this constraint; this spec must not violate it.

#### Concept mapping

| SignalR today | gRPC |
|---|---|
| Hub connection, then `Initialize(homeReferenceId)` | a single `Subscribe` call carrying the home id |
| Switching home: call `Initialize` again | cancel the stream, open a new `Subscribe` |
| `Initialized()` callback | first `LiveUpdate` on the stream, `initialized` case |
| `FailedToInitialize(reason)` callback | the call fails with an `RpcException` — see below |
| `Update(updateType, data)` callback | subsequent `LiveUpdate` messages, typed by `oneof` |
| `Groups.AddToGroupAsync(connectionId, group)` | registry subscribes the stream to the same group names |
| `IHubContext.Clients.Group(g).Update(...)` | registry fans the update out to that group's streams |
| `Context.ConnectionId` | a per-stream subscription id generated on `Subscribe` |
| `Context.UserIdentifier` | `ServerCallContext.GetHttpContext().User`, via `GrpcRequestContext` |
| `Context.ConnectionAborted` | `ServerCallContext.CancellationToken` |

`FailedToInitialize` becomes call failure rather than a stream message, because a subscription that cannot be established should not produce a stream at all:

| Hub behavior | gRPC status |
|---|---|
| rate limiter refuses | `ResourceExhausted` |
| `homeReferenceId` is not a valid Guid | `InvalidArgument` |
| home not found, or user not owner and not in `AllowedUsers` | `PermissionDenied` |

The hub currently returns silently on an unparseable Guid and swallows exceptions in a `catch` that only logs. The gRPC version surfaces these as statuses instead. That is a deliberate behavior change and an improvement — record it in the PR description.

#### Subscription registry

SignalR's group mechanism needs replacing, because `IHubContext` disappears with the hub. Add a singleton to `Senswave.LiveUpdates.Api`:

```csharp
public interface ILiveUpdatesSubscriptionRegistry
{
    ILiveUpdatesSubscription Subscribe(IReadOnlyCollection<string> groupNames);
    Task PublishAsync(string groupName, LiveUpdate update, CancellationToken cancellationToken);
}
```

A subscription owns a bounded `Channel<LiveUpdate>`. The service method drains it into `IServerStreamWriter<LiveUpdate>` until the call is cancelled, then disposes the subscription, which removes it from every group.

Group names stay exactly as they are, from `GuidExtensions`: `senswave-devices-{id}`, `senswave-datasources-{id}`, `senswave-homes-{id}`. Keeping the naming makes the registry a drop-in for the group mechanism and keeps the diff readable.

Use a bounded channel with a drop-oldest policy, not an unbounded one. An unbounded channel lets a slow or stalled client grow memory without limit, which SignalR avoided through its own buffering. Read the bound from `LiveUpdatesOptions` so it is configurable.

#### The work

1. **`LiveUpdatesGrpcService`** in `Senswave.LiveUpdates.Api/Grpc/`, deriving from the generated `LiveUpdatesService.LiveUpdatesServiceBase`, carrying `[Authorize(AuthenticationSchemes = "Identity.Bearer")]` exactly as `LiveUpdatesHub` does. `Subscribe` runs the checks the hub's `Initialize` runs today, in the same order — rate limiter, Guid parse, `HomeRequest` access check, `DevicesRequest` group resolution — then writes `Initialized`, then drains the subscription channel until `ServerCallContext.CancellationToken` fires. Keep the existing log statements; they are the module's operational surface.

2. **`ILiveUpdatesSubscriptionRegistry`** and its implementation, registered as a singleton.

3. **Rewire the update services.** `DevicesUpdateService` and `DataSourcesUpdateService` swap `IHubContext<LiveUpdatesHub, ILiveUpdatesHub>` for the registry and build typed `LiveUpdate` messages instead of `JsonObject`. Their interfaces and method signatures do not change, so the four MassTransit consumers — `DeviceTileActionEventConsumer`, `WidgetActionEventConsumer`, `DataSourceStateConsumer`, `DevicePresenceEventConsumer` — are untouched.

4. **Retag telemetry.** `ILiveUpdatesActivityProvider` activities are named `"SignalR /update"` and tagged `signalr.group`, `signalr.request.type`, `signalr.request.device_id`. Rename to `"LiveUpdates /update"` and `rpc.*`.

5. **Rekey the rate limiter.** `RateLimitingService` keys its token buckets by `Context.ConnectionId`; key by the per-stream subscription id instead. The service itself stays — it is module business logic, not ASP.NET Core rate limiting, and Spec D keeps it too.

6. **Delete SignalR:**
   - `Hubs/ILiveUpdatesHub.cs` and `Hubs/LiveUpdatesHub.cs`
   - `LiveUpdatesExtensions.UseLiveUpdates` and its call in `Startup.Configure`
   - `services.AddSignalR()` in `LiveUpdatesModule.Register`
   - The `SignalRSwaggerGen` attributes, the `AddSignalRSwaggerGen` call in `Startup`, and the LiveUpdates Swagger document
   - Package references, once confirmed unreferenced: `SignalRSwaggerGen`, `Microsoft.AspNetCore.SignalR.Client`, `AspNetCore.HealthChecks.SignalR`. Three csproj files reference SignalR today: `Senswave.LiveUpdates.Api`, `Senswave.Api`, and `Senswave.LiveUpdates.EndTests`.

   `AspNetCore.HealthChecks.SignalR` needs a decision rather than a straight deletion. Check what it probes; if a health check pings the hub, replace it with a gRPC equivalent or drop it consciously, rather than removing it silently along with the package.

`LiveUpdatesModule` therefore maps a gRPC service like every other module.

#### HA behavior is unchanged, and still limited

`MessagingExtensions` calls `config.ConfigureEndpoints(context)`, giving each consumer one receive endpoint. Across multiple API instances that is a competing-consumer setup: an integration event reaches one instance, and only the clients connected to that instance see the update. SignalR has the same problem today, and no backplane is configured — `AGENTS.md` records that no external cache is in use.

The registry is in-process, so it inherits this exactly. The conversion neither fixes nor worsens the gap. `AGENTS.md` names HA for main processing parts as a future goal; that work needs a fan-out exchange or a shared backplane whether the transport is SignalR or gRPC. Do not attempt it here.

### Module registration

`ISenswaveModule.Register` currently calls `services.AddMinimalApiEndpoints(assembly)`. Add a parallel mechanism for gRPC. gRPC services are mapped, not resolved from a list, so the registration cannot mirror `IEndpoint` exactly.

Add to `Senswave.Abstractions.Modules.ISenswaveModule`:

```csharp
void MapGrpcServices(IEndpointRouteBuilder endpoints);
```

Each module implements it with explicit `endpoints.MapGrpcService<DevicesGrpcService>()` calls. Explicit beats reflection here: the generated base types make an assembly scan awkward, the list is short, and a missing service then fails at compile time rather than silently not being served.

Every module implements it with a real body, `LiveUpdatesModule` included — it maps `LiveUpdatesGrpcService`.

### Host wiring

In `src/Presentation/Senswave.Presentation.Api/Startup.cs`:

- `ConfigureServices`: add `services.AddGrpc(options => options.Interceptors.Add<ErrorHandlingInterceptor>());` and `services.AddGrpcInfrastructure();`.
- `Configure`: inside the existing `app.UseEndpoints(...)` callback, call `module.MapGrpcServices(endpoints)` for each module, after the existing REST group mapping.
- The REST group mapping stays, but `defaultGroutBuilder.MapEndpoints(minimalApiEndpoints)` will now map far fewer endpoints, since most `IEndpoint` implementations are gone. Leave the call in place; auth, public, and diagnostics still need it.
- Kestrel must serve HTTP/2. In development over HTTPS this already works; confirm `appsettings` and the Docker setup do not pin HTTP/1.1.

Swagger generation in `ConfigureServices` currently declares a document per module. Remove the `SwaggerDoc` registrations, `UseSwaggerUI` entries, and `DocInclusionPredicate` dictionary entries for the four fully converted modules: Automations, DataSources, Devices, Homes. Also remove the LiveUpdates document and the `AddSignalRSwaggerGen` scanning call, since the hub is gone. Keep the Users document — that module still serves auth and legal endpoints — along with `DiagnosticModule` and `PublicModule`.

The module `*Tag` and `GroupName` constants are referenced by both Swagger and the endpoints. Once a module's Swagger document is gone and its endpoints are deleted, its unused tag constants should be deleted too rather than left dangling.

## Testing

### What changes

Only EndTests. Unit and integration tests do not touch transport and must pass unmodified — treat any need to edit them as a signal that something leaked out of the transport layer.

### Success paths only

This is a deliberate narrowing agreed on 2026-09-16. Converted EndTests assert success behavior and drop failure assertions. Concretely, using `test/Modules/Devices/Senswave.Devices.EndTests/Base/Devices/CreateDeviceTest.cs` as the worked example:

| Existing test | Fate |
|---|---|
| `ShouldBeSecuredEndpoint` (asserts 401) | delete |
| `OwnerCanCreateDevice` (asserts 201) | convert |
| `OwnerOfDeviceIsSetCorrectly` (asserts 201 + DB state) | convert |
| `CannotCreateDeviceWithDuplicateName` (asserts 409) | delete |
| `MaliciousCannotCreateDevice` (asserts 400) | delete |
| `FriendCanCreateDevice` (asserts 201) | convert |
| `FriendCanNotCreateDevice` (asserts 400) | delete |

The rule: a test survives if its assertion is about a successful call or about state after a successful call. A test whose only assertion is a non-success status code is deleted, not ported.

This will drop coverage. `AGENTS.md` sets a 70% target. Measure after conversion with `dotnet-coverage collect "dotnet test" -f cobertura -o coverage.cobertura.xml` and report the number. If a module falls below 70%, raise it by testing success behavior more thoroughly — additional assertions on returned payloads and persisted state — not by reinstating failure tests.

### Live-updates tests

`test/Modules/LiveUpdates/Senswave.LiveUpdates.EndTests` currently drives the hub through `Microsoft.AspNetCore.SignalR.Client`. Rewrite it against the generated streaming client.

The success-paths-only rule applies with one practical wrinkle worth stating, because it is easy to get wrong: a streaming test's success assertion is that the expected `LiveUpdate` messages arrive on the stream. Such a test needs a timeout, or a failure becomes a hang rather than a red test. Give every stream read a bounded wait and fail the test when it elapses. A hung suite is worse than a failing one.

Keep at minimum: subscribe with a valid home, assert `Initialized` arrives; then publish each of the four update kinds through the corresponding update service and assert the matching typed `LiveUpdate` arrives on a stream subscribed to that group. That is four success assertions covering the whole `oneof`.

Delete the tests whose only assertion is a refusal — unauthorized connection, inaccessible home, rate-limit rejection — consistent with every other module.

Any LiveUpdates unit test that mocks `IHubContext` must be repointed at `ILiveUpdatesSubscriptionRegistry`. This is the sole permitted unit-test change in this spec, and it is mechanical.

### Test infrastructure

`test/Shared/Senswave.TestInfrastructure/TestEnvironments/Base/BaseFeatureTest.cs` currently exposes `CreateUnauthorizedClient`, `CreateAdmin`, `AuthorizeClientAsUser`, `AuthorizeClientAsAdmin`. Add gRPC equivalents alongside them:

```csharp
protected GrpcChannel CreateChannel() =>
    GrpcChannel.ForAddress(Factory.Server.BaseAddress, new GrpcChannelOptions
    {
        HttpHandler = Factory.Server.CreateHandler()
    });

protected async Task<T> CreateAuthorizedClientAsUser<T>() where T : ClientBase<T>;
protected async Task<T> CreateAuthorizedClientAsAdmin<T>() where T : ClientBase<T>;
```

The authorized variants obtain a token through the same flow `AuthorizeClientAsUser` uses today and attach it as `Authorization: Bearer <token>` call metadata, via a `CallCredentials` or an interceptor so every call on the client carries it.

Keep the HTTP helpers. Auth, public, and diagnostics tests still need them, and the arrange steps in converted tests (`PostHome`, `PostBroker`, `PutBrokerForHome`) may stay on HTTP or move to gRPC — decide once, per module, and be consistent. Preferring gRPC for arrange steps is better: it exercises the new surface more and avoids a mixed-transport test reading as accidental.

`Paths.cs` in `TestEnvironments/Common` holds REST route constants. Entries for converted endpoints become unused; delete them as each module converts.

### Verification

Per `AGENTS.md`:

1. `dotnet build Senswave.sln`
2. `dotnet test` for each module touched — all five converted modules plus `Senswave.Presentation.Api.EndTests` and `Senswave.Modules.EndTests`
3. `test/Shared/Senswave.ArchitectureTests` — rules asserting `IEndpoint` conventions will need updating, since most implementations are gone. Update the rules to assert the gRPC service conventions instead: services live in a `Grpc` folder, derive from a generated base, and carry `[Authorize]`.
4. Coverage report per the `AGENTS.md` commands, reported in the PR.

## Suggested execution order

Convert one module end to end before starting the next, so the pattern is reviewed once on a small surface before it is applied 78 times.

1. `Senswave.Users.Api` — 3 rpcs, the smallest converted surface, and the one module where REST and gRPC must coexist. Converting it first forces the coexistence question before it can be discovered late.
2. `Senswave.Automations.Api` — 10 endpoints, few cross-module dependencies.
3. `Senswave.DataSources.Api` — 14 endpoints.
4. `Senswave.Homes.Api` — 18 endpoints.
5. `Senswave.Devices.Api` — 28 endpoints, and the module other tests lean on most.
6. `Senswave.LiveUpdates.Api` — the SignalR replacement. Last, because it is the only streaming work and shares nothing with the unary conversions. It also depends on Devices being converted, since its tests publish device updates.

Host wiring and test infrastructure changes land with the first module, then are reused.

## Known gaps

- **gRPC methods are unthrottled.** `RequireRateLimiting` does not apply to gRPC routing. Surviving REST endpoints keep `AnonymousRateLimiter`. `UserRateLimiter` becomes near-dead once module endpoints move; leave it registered, since `main/rest` (Spec D) is the branch where rate limiting is deliberately removed and this branch should not pre-empt that comparison.
- **Coverage will drop** from removing failure-path tests. Expected and accepted.
- **No JSON transcoding.** Non-gRPC clients lose access to converted endpoints.

## Acceptance criteria

- All 73 converted endpoints are served as gRPC methods; their `IEndpoint` classes and REST DTOs are deleted.
- The 5 Users endpoints that stay REST, and the `MapIdentityApi<User>()` surface, are untouched and still reachable.
- Live updates are served by `LiveUpdatesService.Subscribe`; the SignalR hub, its registration, its Swagger integration, and its packages are gone; the four MassTransit consumers are unchanged.
- No stream test can hang: every stream read is bounded by a timeout.
- Auth, public, diagnostics, health, and SignalR surfaces are unchanged and still reachable.
- Every gRPC service enforces authorization equivalent to the endpoint it replaced.
- `dotnet build Senswave.sln` is green.
- `dotnet test` is green across all modules; no unit or integration test was modified.
- Architecture tests assert the gRPC service conventions.
- Coverage is reported; any module below 70% has a stated plan or a fix.
