# Spec D — Comparison Profile (REST baseline)

**Branch:** one short-lived branch per step, each merged to `main` by its own PR after owner review — see Spec A, "Delivery"
**Depends on:** Spec A (transport switch). Spec B for the `GrpcWeb` run.
**Status:** design approved 2026-09-16; revised 2026-10-10. Replaces the never-merged `main/rest` branch with a configuration profile and a test filter.

## Purpose

Make REST, gRPC, and gRPC-Web comparable on equal terms, from one commit.

The original design kept a separate, never-merged `main/rest` branch that deleted rate limiting and deleted failure-path EndTests, so REST would match the gRPC branches. With the transport switch (Spec A), all three transports live in one build, so the same two equalizations become **configuration and test filtering** instead of deletions:

| Difference to equalize | Old approach | New approach |
|---|---|---|
| gRPC methods are unthrottled | delete rate limiting on `main/rest` | `RateLimiter:Enabled=false` in comparison runs |
| gRPC suites are success-path only | delete REST failure tests on `main/rest` | tag REST failure tests, filter them out in comparison runs |
| REST must stay frozen while gRPC evolves | separate branch, rebased | same commit; only `Transport:Mode` differs |

Nothing is deleted. `main` keeps rate limiting on by default and keeps its full test suite.

## Deliverable 1 — rate limiting as a switch

`RateLimitersOptions.Enabled` (`RateLimiter:Enabled` in `appsettings.json`) already exists, and `Startup.Configure` already calls `UseRateLimiter()` only when it is true. With the middleware absent, the `RequireRateLimiting(...)` metadata on endpoint groups is inert. So **no code change is needed to disable rate limiting.** Comparison runs set `RateLimiter__Enabled=false`.

Verify, rather than assume, that with `Enabled=false`:

- Startup does not throw. `AddRateLimiters` still registers policies, and `RequireRateLimiting` with no middleware must be a no-op.
- The `RateLimiter/` EndTests are not run in comparison runs. They assert throttling and would fail. They live in the limit environment, which sets its own options, so this is covered by the filter below.

Rate limiting may be disabled in any environment (decided 2026-10-10); there is no production guard.

### What stays

`Senswave.LiveUpdates.Api` has its own `IRateLimiterService`, configured through `LiveUpdatesOptions.RateLimiter`. It is module business logic, not the ASP.NET Core rate limiter. It is active in `Rest` mode only, because the gRPC live-updates path does not use it (Spec A). Remove nothing.

## Deliverable 2 — tag failure-path REST tests

Apply the classification rule the gRPC suites are written to, but **tag instead of delete**:

> A test is a failure-path test if what it is named for and proving is a refusal: its subject assertion is a non-success status code. Arrange-step successes do not count.

Tag each such test method with:

```csharp
[Trait("Category", "FailurePath")]
```

Worked example, `test/Modules/Devices/Senswave.Devices.EndTests/Base/Devices/CreateDeviceTest.cs`:

| Test | Assertion | Tag |
|---|---|---|
| `ShouldBeSecuredEndpoint` | `Unauthorized` | `FailurePath` |
| `OwnerCanCreateDevice` | `Created` | — |
| `OwnerOfDeviceIsSetCorrectly` | `Created` + DB state | — |
| `CannotCreateDeviceWithDuplicateName` | `Created` then `Conflict` | `FailurePath` (the success is arrange, not subject) |
| `MaliciousCannotCreateDevice` | `BadRequest` | `FailurePath` |
| `FriendCanCreateDevice` | `Created` | — |
| `FriendCanNotCreateDevice` | `BadRequest` | `FailurePath` |

Where a `[Theory]` mixes positive and negative privilege cases (for example via `NotManageDevicePrivileges` and `DisplayDevicePrivileges` on `BaseFeatureTest`), a trait cannot apply per data row. Split the theory into a success theory and a refusal theory, tag the refusal one, and leave assertions unchanged. This is the only structural edit permitted to existing tests in this spec.

Apply across:

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

The `RateLimiter/` tests in `Senswave.Presentation.Api.EndTests` are tagged `[Trait("Category", "RateLimiting")]` at class level, since they test a feature comparison runs disable.

Unit and integration tests are not tagged; comparison runs exclude them by project.

## Deliverable 3 — comparison runbook

Add `docs/comparison.md` describing how to produce comparable runs. The essentials:

```bash
# Test-suite comparison (same commit). Each transport has its own EndTest projects:
#   REST:     Senswave.<Module>.EndTests          (filter out failure paths and rate limiter tests)
#   gRPC:     Senswave.<Module>.EndTestsGrpc      (success-only by construction, no filter)
#   gRPC-Web: Senswave.<Module>.EndTestsGrpcWeb   (success-only by construction, no filter)
dotnet test test/Modules/Devices/Senswave.Devices.EndTests --filter "Category!=FailurePath&Category!=RateLimiting"
dotnet test test/Modules/Devices/Senswave.Devices.EndTestsGrpc
dotnet test test/Modules/Devices/Senswave.Devices.EndTestsGrpcWeb
```

```bash
# Running instance for load / latency comparison
Transport__Mode=Rest     RateLimiter__Enabled=false
Transport__Mode=Grpc     RateLimiter__Enabled=false
Transport__Mode=GrpcWeb  RateLimiter__Enabled=false
```

The runbook also records:

- The commit SHA every run used. All runs in one comparison must share it.
- That traces are separable by the `senswave.transport` resource attribute (Spec A).
- That coverage is collected per suite with `dotnet-coverage collect` and the same filter, then reported side by side.
- The known behavioral differences between modes that a reader of results must know: live-update error semantics (Spec A), auth running over the selected transport in every mode (gRPC login issues the same tokens as REST), Google login existing only in `Rest` mode, and gRPC modes being unthrottled.

## Testing and verification

1. `dotnet build Senswave.sln`
2. `dotnet test` unfiltered: green, and the same test count as before tagging, plus any splits of mixed theories.
3. Each comparison filter above runs green.
4. Run the API with `RateLimiter__Enabled=false` and confirm it starts and serves.
5. Coverage: unfiltered (should match `main`) and per comparison filter. Report all of them. If REST-filtered coverage differs notably from gRPC-filtered coverage, that difference is a finding worth writing down.

Architecture tests are unaffected.

## Implementation notes (2026-10-11)

- **Rate limiting switch verified on a running host** (Postgres and RabbitMQ in containers, `Transport__Mode=Rest`): with `RateLimiter__Enabled=false` the host starts, and 30 anonymous `POST /api/v1/auth/login` calls in a row all return 401, none 429. With the default (`true`), the same burst returns 429 from the 11th call (anonymous `TokenLimit` 10).
- **Tagging:** 290 REST EndTests carry `Category=FailurePath`. The two ASP.NET Core rate-limiter classes carry `Category=RateLimiting` at class level. A script classified each test by its last HTTP status assertion, then about 60 edge cases were reviewed by hand:
  - "delete, then GET returns 404" is a success (11 tests)
  - "refuse, then GET shows it still exists" is a refusal
  - the `PresenceGates*` tests (refused offline, allowed online, refused again) are refusals
  - `ConfirmEmailV2`: the 302 to the failure page is a refusal, the 302 to the success page is not
  - SignalR refusals are tagged: the hub's 401, an intruder's `Initialize`, and the LiveUpdates module rate limiter
  - tests named against their assertion are classified by the assertion: `TileActionAllowedWithDefaultPresence` and `MaliciousUserCanGetAutomations` assert refusals; `FailsWhenValueProvided` asserts 204
  - tests that assert a side effect did not happen (automation not triggered, event not published) have no refusal status and stay untagged
- **No theory splits were needed.** Every status assertion in the REST suites compares against a literal code, and no theory branches on its data row, so no `[Theory]` mixes success and refusal rows.
- **CLAUDE.md** documents the tagging rule under "Conventions to preserve".

## Known gaps

- **Tagging is manual and can drift.** A new REST failure test without the trait silently enters comparison runs. Mitigate by documenting the rule in CLAUDE.md under conventions, so new tests follow it.
- **The suites are not equal in size.** The gRPC and gRPC-Web projects are deliberately minimal smoke suites (Spec A, Spec B), while the filtered REST suite keeps every success-path test. Compare runtime behavior (latency, throughput, payload size) on running instances; treat test-suite comparison as "does each transport work", not as like-for-like coverage. Report counts alongside results.

## Acceptance criteria

- No production code or test is deleted.
- Comparison runs disable ASP.NET Core rate limiting by configuration alone, in any environment.
- The LiveUpdates module's own rate limiter is untouched and active in `Rest` mode.
- Every REST failure-path EndTest carries `Category=FailurePath`; rate limiter tests carry `Category=RateLimiting`.
- `docs/comparison.md` exists with the filters, environment settings, and reporting rules.
- `dotnet build Senswave.sln` and unfiltered `dotnet test` are green; each comparison filter is green.
- Coverage is reported unfiltered and per comparison filter.
