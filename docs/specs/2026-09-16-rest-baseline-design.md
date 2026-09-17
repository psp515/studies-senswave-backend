# Spec D — REST Baseline

**Branch:** `main/rest`, branched from `main`, **never merged**
**Depends on:** nothing
**Status:** design approved 2026-09-16

## Purpose

Provide a REST control branch that can be compared fairly against `main/grpc` and `main/grpc-web`. The gRPC branches lose rate limiting on their converted surface as an unavoidable consequence of the transport change, and they trim their EndTests to success paths only. For a comparison to mean anything, the REST branch must carry the same two properties.

So this branch keeps all 78 REST endpoints exactly as they are, removes rate limiting entirely, and trims its EndTests by the same rule the gRPC branches use.

This branch is a measurement artifact. It stays open indefinitely and is never merged to `main`. Nothing on `main` changes because of it.

## Scope

In scope:

- Complete removal of rate limiting from the application and from the test infrastructure.
- Trimming EndTests to success paths only, by the same rule as Spec A.

Out of scope:

- Any change to endpoint behavior, routing, DTOs, or handlers. All 78 endpoints stay exactly as they are.
- The SignalR live-updates hub. It stays on this branch — it is the baseline that `main/grpc` replaces with a streaming rpc. Do not touch `Senswave.LiveUpdates.Api` beyond the EndTest trimming below.
- Anything gRPC-related. This branch never sees a `.proto` file.
- Merging. See "Branch lifecycle".

## Deliverable 1 — remove rate limiting

### Application code

Delete `src/Presentation/Senswave.Presentation.Api/RateLimiters/` in full:

```
RateLimiters/
  RateLimiterExtensions.cs
  RateLimitersOptions.cs
  Anonymous/AnonymousRateLimiter.cs
  Anonymous/AnonymousRateLimiterOptions.cs
  User/UserRateLimiter.cs
  User/UserRateLimiterOptions.cs
```

In `src/Presentation/Senswave.Presentation.Api/Startup.cs`:

- Remove `services.AddRateLimiters(_configuration);` from `ConfigureServices`.
- Remove the `IOptions<RateLimitersOptions> rateLimitingOptions` parameter from `Configure` and the conditional `app.UseRateLimiter();` it guards.
- Remove every `RequireRateLimiting(...)` call in the `UseEndpoints` callback. Three groups use it: the default versioned group with `UserRateLimiter.PolicyName`, and the two public groups with `AnonymousRateLimiter.PolicyName`. The groups themselves stay — they carry versioning and routing, not just throttling.
- Remove the `AnonymousRateLimiter.PolicyName` argument from `endpoints.UseAuthEndpoints(...)`. That method's signature takes a policy name; simplify it in `Senswave.Users.Api/UsersModuleExtensions.cs` to take none.
- Remove the now-unused `using Senswave.Api.RateLimiters...` directives.

### Configuration

Remove the rate limiter configuration sections from `appsettings.json` and every environment-specific variant. Search for the option class names to find them, since the section keys are bound by name.

### Test infrastructure

`test/Shared/Senswave.TestInfrastructure/TestEnvironments/Base/BaseTestEnvironment.cs` imports `Senswave.Api.RateLimiters.Anonymous` and `Senswave.Api.RateLimiters.User` and configures limiter options for the test host. Remove those imports and the option overrides.

Delete the dedicated limit test environment, which exists only to exercise throttling:

```
test/Shared/Senswave.TestInfrastructure/TestEnvironments/Limit/
  LimitFeatureTest.cs
  LimitTestCollection.cs
  LimitTestEnvironment.cs
```

Before deleting, grep for `LimitFeatureTest` and `LimitTestEnvironment` across `test/`. If any test outside the rate limiter suite derives from them, that test needs rehoming onto `BaseFeatureTest` rather than the environment being kept.

### Rate limiter tests

Delete:

```
test/Presentation/Senswave.Api.EndTests/Senswave.Presentation.Api.EndTests/RateLimiter/
  AnnonymousRateLimiterTests.cs
  UserRateLimiterTests.cs
```

### What stays

`Senswave.LiveUpdates.Api` has its own rate limiter — `IRateLimiterService` / `RateLimitingService`, configured through `LiveUpdatesOptions.RateLimiter` and keyed by SignalR connection id. **Keep it.** It is module business logic, not the ASP.NET Core rate limiter, and `main/grpc` keeps it too. Removing it here would make the branches diverge on something neither branch's design intends to change.

The distinction to apply: remove what `AddRateLimiters` / `UseRateLimiter` / `RequireRateLimiting` provides. Leave anything a module implements itself.

### Packages

After the removal, check whether any package in `Directory.Packages.props` becomes unreferenced. Rate limiting in ASP.NET Core is in-framework, so most likely nothing drops out. Do not remove a package without confirming it has no remaining reference — `AGENTS.md` forbids incidental dependency changes.

## Deliverable 2 — trim EndTests to success paths

Apply the identical rule Spec A applies, so the two branches' suites are comparable:

> A test survives if its assertion is about a successful call, or about state observed after a successful call. A test whose only assertion is a non-success status code is deleted, not rewritten.

Worked example, `test/Modules/Devices/Senswave.Devices.EndTests/Base/Devices/CreateDeviceTest.cs`:

| Test | Assertion | Fate |
|---|---|---|
| `ShouldBeSecuredEndpoint` | `Unauthorized` | delete |
| `OwnerCanCreateDevice` | `Created` | keep |
| `OwnerOfDeviceIsSetCorrectly` | `Created` + DB state | keep |
| `CannotCreateDeviceWithDuplicateName` | `Created` then `Conflict` | delete |
| `MaliciousCannotCreateDevice` | `BadRequest` | delete |
| `FriendCanCreateDevice` | `Created` | keep |
| `FriendCanNotCreateDevice` | `BadRequest` | delete |

`CannotCreateDeviceWithDuplicateName` is deleted even though it contains a success assertion, because the success is arrange, not the subject. Judge by what the test is named for and what it is proving.

Where a `[Theory]` is parameterized over privileges — `NotManageDevicePrivileges`, `DisplayDevicePrivileges` and similar helpers on `BaseFeatureTest` — and the negative privileges exist only to assert refusal, remove those cases and keep the positive ones. If a data source becomes unused, delete it.

Apply this across every EndTest project:

```
test/Modules/Automations/Senswave.Automations.EndTests
test/Modules/DataSources/Senswave.DataSources.EndTests
test/Modules/Devices/Senswave.Devices.EndTests
test/Modules/Homes/Senswave.Homes.EndTests
test/Modules/LiveUpdates/Senswave.LiveUpdates.EndTests
test/Modules/Users/Senswave.Users.EndTests
test/Modules/Senswave.Modules.EndTests
test/Presentation/Senswave.Api.EndTests
```

Unit tests and integration tests are **not** trimmed, matching Spec A, which leaves them untouched.

## Testing and verification

Per `AGENTS.md`:

1. `dotnet build Senswave.sln`
2. `dotnet test` across all modules
3. Coverage: `dotnet-coverage collect "dotnet test" -f cobertura -o coverage.cobertura.xml` then `reportgenerator -reports:coverage.cobertura.xml -targetdir:coverage-report -reporttypes:Html`

Coverage will drop, for the same reason it drops on `main/grpc`. Report the number. Do not reinstate failure tests to recover it — that would break the comparison this branch exists to enable. If the drop is worse here than on `main/grpc`, that difference is itself a finding worth writing down.

Architecture tests in `test/Shared/Senswave.ArchitectureTests` should pass unchanged, since no endpoint convention changes on this branch. If one fails, it was asserting something about rate limiting, and it should be deleted along with the feature.

## Branch lifecycle

`main/rest` is never merged into `main`. It is cut from `main` at the point the comparison begins and is left open. If `main` moves and the comparison needs refreshing, rebase this branch rather than merging it back.

`main` therefore keeps rate limiting. So does `main/grpc`, on the REST endpoints that survive there. That asymmetry is deliberate: `main/grpc` measures what happens when a gRPC surface has no throttling while REST auth endpoints keep it, and `main/rest` measures a REST surface with none at all.

## Known gaps

- **The API on this branch is unthrottled**, including anonymous auth endpoints. This branch must never be deployed to staging or production. Say so in the PR description and in the branch description.
- **Coverage drops** from removing failure-path tests. Expected and accepted.

## Acceptance criteria

- No reference to ASP.NET Core rate limiting remains anywhere in `src/` or `test/`: no `RateLimiters` folder, no `UseRateLimiter`, no `RequireRateLimiting`, no limiter options in configuration, no limit test environment, no limiter tests.
- The LiveUpdates module's own rate limiter is still present and still registered.
- The SignalR hub is unchanged.
- All 78 REST endpoints are unchanged in routing, DTOs, and behavior.
- EndTests are trimmed by the stated rule; unit and integration tests are untouched.
- `dotnet build Senswave.sln` is green.
- `dotnet test` is green.
- Coverage is measured and reported, with no failure tests reinstated to prop it up.
- The branch carries a clear "do not deploy" note.
