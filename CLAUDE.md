# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Senswave is a self-hosted/cloud backend for managing DIY smart-home devices over MQTT: users organize homes/rooms, register devices with typed operations, run automations, and stream live state to dashboards via SignalR. Built with .NET 10 Minimal APIs as a **modular monolith**.

## Commands

```bash
dotnet build Senswave.sln          # always verify the whole solution builds
dotnet test                        # run all tests
dotnet test path/to/Project.csproj # run a single test project
dotnet test --filter "FullyQualifiedName~CreateHomeTest"   # run a single test/class
```

- After changing a module, run at least that module's tests (`test/Modules/<Module>/...`), not just the whole suite.
- Coverage: `dotnet-coverage collect "dotnet test" -f cobertura -o coverage.cobertura.xml`, then `reportgenerator -reports:coverage.cobertura.xml -targetdir:coverage-report -reporttypes:Html`. Target is 70%.
- Test projects use a `Tests` build configuration in addition to `Debug`/`Release`.

### Local environment setup

```bash
dotnet dev-certs https -t                                              # trust dev HTTPS cert
dotnet user-secrets init                                               # per-project, for secrets
dotnet user-secrets set "Modules:Users:Auth:Google:ClientSecret" "<value>"
```

Running everything via Docker is documented in [`docker/README.md`](docker/README.md) — copy `docker/.env.template` to `.env`, generate a pfx cert into `docker/https`, then `docker-compose up -d`.

## Architecture

### Modular monolith layout

- `src/Shared` — cross-cutting concerns used by every module:
  - `Senswave.Abstractions` — pure contracts/interfaces with no implementation: `ICommand`/`IQuery` (Mediator pattern), `Result`/`Error`/`ErrorType`, entity base types, `IRequestContext`, module interface (`ISenswaveModule`).
  - `Senswave.Infrastructure` — Mediator pipeline behaviors, persistence base, MassTransit messaging setup, module loading (`ModuleLoader`), OpenTelemetry diagnostics.
  - `Senswave.Infrastructure.Web` — ASP.NET-specific plumbing: minimal-API endpoint discovery/mapping, request context, exception handling, cryptography.
  - `Senswave.Integration` — DTOs (`*Request`/`*Response`) for cross-module communication over MassTransit request/response, organized by target module (`Homes/`, `Devices/`, `DataSource/`, `Automations/`, `User/`) plus `DataTransfer/` events for pub/sub between modules (e.g. live-update events).
- `src/Modules/<Name>` — one folder per bounded context: `Automations`, `DataSource`, `Devices`, `Homes`, `LiveUpdate`, `Users`. Modules never reference each other's Domain/Application/Infrastructure directly — they talk through `Senswave.Integration` contracts over MassTransit.
- `src/Presentation` — runnable hosts: `Senswave.Presentation.Api` (the HTTP/SignalR API), `Senswave.Presentation.DataSource.Worker` (background MQTT broker worker), `Senswave.Presentation.Seed` (data seeding tool).

### Module internals (per module, 4 projects)

- **Domain** — entities, service/repository interfaces, and business logic for the module's core concepts.
- **Application** — the actual business logic, organized as `<Entity>/Features/<FeatureName>/` folders (vertical slices). Each feature typically has a `Command`/`Query`, a `Handler` (Mediator), a FluentValidation `Validator`, and an `Errors` static class. Cross-module service calls (e.g. checking home access) go through interfaces backed by MassTransit requests to other modules' Integration contracts.
- **Infrastructure** — EF Core `DbContext`/entity configurations, repository implementations, MassTransit consumers for events published by other modules.
- **Api** — minimal-API endpoints, one per feature folder (`<FeatureName>Endpoint.cs`, `<FeatureName>Request.cs`/`Response.cs`, `<FeatureName>Extensions.cs` for DTO↔Command/Query mapping). Endpoints call `IMediator.Send`. Each module has a `<Module>Module : ISenswaveModule` entry point that registers its DI and endpoints.

`ModuleLoader` (in `Senswave.Infrastructure`) discovers all `Senswave*.dll` assemblies at startup and activates every `ISenswaveModule` found — modules are plugged into `Startup` this way rather than being referenced explicitly.

### Request flow

`Endpoint` → builds `Command`/`Query` DTO → `IMediator.Send` → `Handler` (Application) → `Domain`/`Infrastructure` (repositories, EF Core) → returns a `Result`/`Result<T>` → `Extensions` maps it to an HTTP response DTO. Errors flow as `Result` failures with an `ErrorType` (`Validation`, `NotFound`, `Conflict`, `Failure`, `ServerFail`), mapped centrally to HTTP status codes rather than thrown as exceptions in normal control flow.

### Cross-module and async communication

MassTransit is the backbone for both:
- **Request/response between modules** (e.g. Homes asking Devices "does this device exist?") using the request/response DTOs under `Senswave.Integration`.
- **Fire-and-forget events** (e.g. a device state change triggering an automation, or a live-update event reaching `LiveUpdate`/SignalR) using the event types under `Senswave.Integration/DataTransfer`.

Senswave can run MassTransit against RabbitMQ or an in-memory transport, configurable per environment. PostgreSQL is the only database; there is currently no external cache. High availability for main processing paths is a stated future goal, so new code should keep that in mind, and OpenTelemetry instrumentation is expected on custom (non-framework) code paths.

### Testing structure

Tests mirror `src/Modules`/`src/Shared`/`src/Presentation` under `test/`, split into three kinds per module: `*.UnitTests` (isolated, minimal setup), `*.IntegrationTests` (Testcontainers-backed, real Postgres/RabbitMQ), `*.EndTests` (full API tests through `WebApplicationFactory`). `test/Shared/Senswave.ArchitectureTests` uses `NetArchTest` to enforce the module layering rules above (e.g. Application may only depend on Domain, Commands/Queries must be named `*Command`/`*Query`) — architecture violations fail these tests rather than code review. `test/Shared/Senswave.TestInfrastructure` holds shared fixtures/base test classes used across module test projects.

## Conventions to preserve

- **REST DTO contract is frozen**: `*Request`/`*Response` classes in `Api` projects are a public contract — never rename or remove existing fields, only add new ones. When an underlying Application command's field is renamed, map old→new in the feature's `*Extensions` class rather than changing the DTO.
- Don't add new third-party libraries or bump dependency versions without explicit approval; don't perform incidental/unrelated library upgrades while making other changes (`Directory.Packages.props` is centrally managed).
- Follow `.editorconfig` for style (4-space indent, CRLF line endings).
- PR titles follow `fix: <info>` / `feat: <info>` / `chore: <info>`; don't merge your own PRs, and flag test failures rather than pushing through them.

## In-flight design work

`docs/specs/` contains approved-but-unimplemented design specs for migrating the 5 feature modules' REST surface to gRPC/gRPC-Web (`main/grpc`, `main/grpc-web` branches) with a REST baseline comparison branch (`main/rest`, never merged). As of now none of this is implemented — no `Senswave.Contracts` or `Senswave.Infrastructure.Grpc` projects exist yet, and the API is fully REST + SignalR. Consult these specs before starting any gRPC-related work in this repo, since they define binding proto/naming conventions and a strict success-paths-only test-trimming rule for that effort specifically (it does not apply to the current REST test suite).
