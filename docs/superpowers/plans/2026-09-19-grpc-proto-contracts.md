# gRPC Proto Contracts (Senswave.Contracts) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up `src/Shared/Senswave.Contracts`, a new class library holding every `.proto` file that mirrors the 73 REST endpoints being converted to gRPC (Devices, Dashboards, Widgets, Operations, Sharing, Homes, Rooms, Automations, DataSources, 3 of Users' endpoints) plus the `LiveUpdatesService` streaming contract that replaces the SignalR hub — with zero behavior change to the running application.

**Architecture:** One `.proto` file per REST module tag (split further when a module exceeds ~15 rpcs), all compiled with `GrpcServices="Both"` so a single `dotnet build` produces both server base classes and typed clients from the same source. Every message field is derived directly from the existing `*Request`/`*Response`/nested-Dto C# classes already shipping in each module's `Api` project — this plan does not invent new shapes, it transliterates existing ones into protobuf idiom (`lower_snake_case`, `Guid`→`string`, `DateTime`→`google.protobuf.Timestamp`, nullable→`optional`, untyped `JsonObject`/`JsonValue`→`google.protobuf.Struct`/`google.protobuf.Value`). No gRPC service implementation, no REST endpoint deletion, and no `Senswave.Infrastructure.Grpc` plumbing (interceptor, request context) happen in this plan — those are Spec A's job and belong in a separate plan, per this plan's own Scope Check below.

**Tech Stack:** .NET 10, `Grpc.Tools` codegen, `Google.Protobuf`, `Grpc.AspNetCore` (server base types), `Grpc.Net.Client` (client stubs) — all centrally versioned via `Directory.Packages.props`, matching this repo's existing package-management convention.

**Spec:** [`docs/specs/2026-09-16-grpc-contracts-design.md`](../../specs/2026-09-16-grpc-contracts-design.md) (Spec 0 — gRPC Contracts and Plumbing). This plan implements Spec 0's **Deliverable 1** (`src/Shared/Senswave.Contracts` and all `.proto` files) plus the slice of **Deliverable 3** (package registration) needed to compile it. Spec 0's Deliverable 2 (`src/Shared/Senswave.Infrastructure.Grpc` — `ErrorHandlingInterceptor`, `GrpcRequestContext`, `ResultGrpcExtensions`, `AddGrpcInfrastructure`, the `ErrorType`→`StatusCode` mapping test) is a separate, independently-testable subsystem (it has no `.proto` dependency to write, only C# plumbing) and is intentionally **out of scope** — write a follow-up plan for it once this one lands.

## Scope Check

Spec 0 bundles three deliverables (contracts, gRPC infrastructure, package registration) that are independently buildable and reviewable. This plan covers only the proto-contracts deliverable the user asked for (Deliverable 1) plus the minimal package registration it needs to compile (a subset of Deliverable 3 — `Grpc.AspNetCore`, `Grpc.Net.Client`, `Google.Protobuf`, `Grpc.Tools`; the three packages that exist solely for Spec A telemetry and Spec B gRPC-Web — `OpenTelemetry.Instrumentation.GrpcNetClient`, `Grpc.AspNetCore.Web`, `Grpc.Net.Client.Web` — are deliberately left out here and added when those specs' plans execute, so this plan doesn't carry package additions nothing in it uses). Deliverable 2 is a separate plan.

## Global Constraints

- **Package naming:** `senswave.<module>.v1` (e.g. `senswave.devices.v1`).
- **C# namespace:** `option csharp_namespace = "Senswave.Contracts.<Module>.V1";` on every file — must not collide with hand-written module namespaces.
- **Service naming:** one service per proto file, named after the REST tag it replaces (e.g. `DevicesService`, `DashboardsService`).
- **Rpc naming:** the existing feature-folder name, unchanged (e.g. `CreateDevice`, `DisplayDashboards`) — this keeps a 1:1 trace back to the `IEndpoint` class and its MediatR command.
- **Message naming:** `<Rpc>Request` and `<Rpc>Response`, both declared even when one side is empty — **except** the shared `senswave.common.v1.IdResponse`, which is reused directly as the return type for every rpc whose REST response is exactly `{ Guid Id }`, because Spec 0 explicitly names `IdResponse` as a type "defined once" in `common.proto` (matching the `IdResponse` class the EndTests already deserialize `Created` responses into). A `<Rpc>Response` with genuinely different fields (e.g. `CreateSharingResponse` in Homes) still gets its own message.
- **Field derivation:** fields come only from the existing `*Request`/`*Response`/nested-Dto classes (or, where the REST DTO omits a value bound from the route/query string instead, that route/query value becomes an explicit message field, since gRPC has no URL to carry it). Field names become `lower_snake_case`.
- **Identifiers:** `Guid` → `string` (canonical `D` format, enforced by the mapping layer in Spec A — not this plan).
- **Timestamps:** `DateTime` → `google.protobuf.Timestamp` (import `google/protobuf/timestamp.proto`).
- **Untyped JSON:** `JsonObject`/`JsonObject?` → `google.protobuf.Struct`; `JsonValue`/`JsonNode?` → `google.protobuf.Value` (both from `google/protobuf/struct.proto`).
- **Optionality:** a nullable REST scalar (`Guid?`, `int?`, `double?`, `bool?`) becomes an `optional` proto3 scalar field. A non-nullable scalar with a query-string default (e.g. `int page = 1`) is **not** optional — it is a plain required-presence scalar, since the "nullability" there is an ASP.NET binding default, not a real absent/present distinction. Message-typed fields (`Struct`, `Value`, `Timestamp`, any nested Dto message) never take the `optional` keyword — proto3 message fields already carry presence natively.
- **Enums:** mirror the domain enum's named values; the zero value is always suffixed `_UNSPECIFIED` regardless of what the C# enum calls its zero member. A C# sentinel like `Invalid = -1` that only exists for validation failure (never a valid wire value) is omitted from the proto enum — validation happens before a value ever reaches the wire.
- **Message name collisions:** protobuf message names must be unique per `package`, not just per file. Where two files in the same module package would otherwise declare the same nested-Dto shape twice (e.g. Devices' `operations.proto` and `widgets.proto` both have an "operation summary" shape; Homes' `homes.proto` and `rooms.proto` both have a "room" shape), rename one so they don't collide — this plan calls out every such rename explicitly per task.
- **Codegen:** `<Protobuf Include="Protos/**/*.proto" GrpcServices="Both" ProtoRoot="Protos" />` — one compilation produces both server base classes and typed clients, so they can never drift.
- **No incidental changes:** do not touch any REST `IEndpoint`, `*Request`/`*Response` DTO, or `Directory.Packages.props` entry unrelated to the four gRPC packages this plan adds. `AGENTS.md` forbids new libraries and incidental upgrades without approval; the four packages below were approved for this effort on 2026-09-16 per Spec 0.
- **Verification is build-only:** this plan ships no runtime behavior (nothing calls these services yet), so there are no unit tests to write. Every task's "test" step is `dotnet build` succeeding and the expected generated types existing in `obj/`, matching Spec 0's own Testing section ("This spec ships no behavior, so it ships no new behavioral tests").

---

## File Structure

```
Directory.Packages.props                                    (modified — new <!-- gRPC --> block)
Senswave.sln                                                 (modified — new project + solution folder)
src/Shared/Senswave.Contracts/
  Senswave.Contracts.csproj                                  (new)
  Protos/
    common/v1/common.proto                                   (new)
    automations/v1/automations.proto                          (new)
    datasources/v1/datasources.proto                          (new)
    devices/v1/devices.proto                                  (new)
    devices/v1/dashboards.proto                                (new)
    devices/v1/widgets.proto                                   (new)
    devices/v1/operations.proto                                (new)
    devices/v1/sharing.proto                                   (new)
    homes/v1/homes.proto                                       (new)
    homes/v1/rooms.proto                                       (new)
    homes/v1/sharing.proto                                     (new)
    users/v1/users.proto                                       (new)
    liveupdates/v1/liveupdates.proto                           (new)
```

Each `.proto` file is self-contained: one file, one task, one commit. `common.proto` lands first because seven of the other files import `IdResponse` from it. Everything else can be written in any order after that.

---

### Task 1: Register gRPC packages in `Directory.Packages.props`

**Files:**
- Modify: `Directory.Packages.props`

**Interfaces:**
- Produces: four centrally-pinned package versions (`Grpc.AspNetCore`, `Grpc.Tools`, `Google.Protobuf`, `Grpc.Net.Client`) that Task 2's csproj references by name with no `Version` attribute, matching every other `PackageReference` in this repo.

- [ ] **Step 1: Add the new package versions**

Open `Directory.Packages.props`. Insert a new block immediately before the existing `<!-- Tests -->` comment (i.e. right after the `<!-- Others -->` block that ends with `<PackageVersion Include="StackExchange.Redis" Version="2.12.8" />`):

```xml
		<!-- gRPC -->
		<PackageVersion Include="Grpc.AspNetCore" Version="2.83.0" />
		<PackageVersion Include="Grpc.Tools" Version="2.84.0" />
		<PackageVersion Include="Google.Protobuf" Version="3.36.1" />
		<PackageVersion Include="Grpc.Net.Client" Version="2.83.0" />
```

Do not touch any other line in the file — no incidental version bumps.

- [ ] **Step 2: Verify the file is still well-formed XML**

Run: `dotnet build Senswave.sln --no-restore -t:Restore` — this forces MSBuild to parse `Directory.Packages.props` without yet needing the new project to exist.
Expected: restore succeeds (no XML parse error, no "duplicate PackageVersion" error).

- [ ] **Step 3: Commit**

```bash
git add Directory.Packages.props
git commit -m "chore: register gRPC package versions for Senswave.Contracts

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 2: Scaffold `Senswave.Contracts` and `common.proto`

**Files:**
- Create: `src/Shared/Senswave.Contracts/Senswave.Contracts.csproj`
- Create: `src/Shared/Senswave.Contracts/Protos/common/v1/common.proto`
- Modify: `Senswave.sln`

**Interfaces:**
- Consumes: the four package versions from Task 1.
- Produces: the `Senswave.Contracts` project itself (target of every later task's `<Protobuf Include>` glob), and `senswave.common.v1.IdResponse` / `Error` / `ErrorType` — the `IdResponse` message is consumed by Tasks 5, 6, 7, 8, 10, 11, 3, 4 (every module with an id-only "Created" response).

- [ ] **Step 1: Create the csproj**

Create `src/Shared/Senswave.Contracts/Senswave.Contracts.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<TargetFramework>net10.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
		<Configurations>Debug;Release;Tests</Configurations>
	</PropertyGroup>

	<ItemGroup>
		<PackageReference Include="Grpc.AspNetCore" />
		<PackageReference Include="Grpc.Net.Client" />
		<PackageReference Include="Google.Protobuf" />
		<PackageReference Include="Grpc.Tools">
			<PrivateAssets>all</PrivateAssets>
			<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
		</PackageReference>
	</ItemGroup>

	<ItemGroup>
		<Protobuf Include="Protos/**/*.proto" GrpcServices="Both" ProtoRoot="Protos" />
	</ItemGroup>

</Project>
```

`Grpc.AspNetCore` supplies the `Grpc.Core.Api`/`Grpc.AspNetCore.Server` types the generated `*ServiceBase` classes derive from; `Grpc.Net.Client` supplies the types the generated `*Client` classes derive from. Referencing both in this shared library (rather than splitting server-only vs. client-only) is what makes `GrpcServices="Both"` compile in one pass, per Spec 0's stated goal ("a single compilation produces both... this avoids a second generation pass and guarantees client and server never drift").

- [ ] **Step 2: Write `common.proto`**

Create `src/Shared/Senswave.Contracts/Protos/common/v1/common.proto`:

```proto
syntax = "proto3";

package senswave.common.v1;

option csharp_namespace = "Senswave.Contracts.Common.V1";

message IdResponse {
  string id = 1;
}

enum ErrorType {
  ERROR_TYPE_UNSPECIFIED = 0;
  ERROR_TYPE_FAILURE = 1;
  ERROR_TYPE_VALIDATION = 2;
  ERROR_TYPE_NOT_FOUND = 3;
  ERROR_TYPE_CONFLICT = 4;
  ERROR_TYPE_SERVER_FAIL = 5;
}

message Error {
  string code = 1;
  ErrorType type = 2;
  string description = 3;
}
```

`ErrorType` mirrors `Senswave.Abstractions.Resulting.ErrorType` (`None, Failure, Validation, NotFound, Conflict, ServerFail`), with `None` renamed to the required `_UNSPECIFIED` zero value. `Error` mirrors `Senswave.Abstractions.Resulting.Error` (`Code`, `Type`, `Description`). Neither is consumed by any rpc signature in this plan — they exist for Spec A/Deliverable 2's error trailer — but Spec 0 places them in `common.proto`, so they are declared now alongside `IdResponse` while the file is being created, rather than reopening this file in a later plan.

- [ ] **Step 3: Add the project to the solution**

Run:
```bash
dotnet sln Senswave.sln add src/Shared/Senswave.Contracts/Senswave.Contracts.csproj --solution-folder Shared
```

- [ ] **Step 4: Build and verify codegen**

Run: `dotnet build src/Shared/Senswave.Contracts/Senswave.Contracts.csproj`
Expected: `Build succeeded`. Then confirm the generated files exist:

```bash
find src/Shared/Senswave.Contracts/obj -iname "Common*.cs"
```
Expected output includes two files: one ending `Common.cs` (message types: `IdResponse`, `Error`, `ErrorType`) — there is no service in this file, so no `*Grpc.cs` is generated for it.

- [ ] **Step 5: Commit**

```bash
git add Senswave.sln src/Shared/Senswave.Contracts
git commit -m "feat: scaffold Senswave.Contracts with common/v1/common.proto

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 3: `automations/v1/automations.proto`

**Files:**
- Create: `src/Shared/Senswave.Contracts/Protos/automations/v1/automations.proto`

**Interfaces:**
- Consumes: `senswave.common.v1.IdResponse` (Task 2).
- Produces: `AutomationsService` with 10 rpcs, for Spec A's `AutomationsGrpcService` to implement later.

Source endpoints (`src/Modules/Automations/Senswave.Automations.Api`, tag `AutomationsModule.AutomationsTag`): `CreateAutomation`, `UpdateAutomation`, `DeleteAutomation`, `GetAutomation`, `DisplayAutomations`, `AutomationState`, `PutConditionToAutomation`, `PutResultToAutomation`, `DeleteCondition`, `DeleteResult`. `AutomationConditionType` and `AutomationConditionConnector` are REST-layer `string`-typed enums (validated against `AutomationConditionType`/`AutomationConditionConnector` C# enums with an `Invalid = -1` sentinel that is dropped per the Global Constraints enum rule) become real proto enums here. `ConditionConfiguration` is an untyped `JsonObject`; `ValueToSend` is an untyped `JsonValue`.

- [ ] **Step 1: Write the proto file**

Create `src/Shared/Senswave.Contracts/Protos/automations/v1/automations.proto`:

```proto
syntax = "proto3";

package senswave.automations.v1;

option csharp_namespace = "Senswave.Contracts.Automations.V1";

import "google/protobuf/struct.proto";
import "common/v1/common.proto";

service AutomationsService {
  rpc CreateAutomation(CreateAutomationRequest) returns (senswave.common.v1.IdResponse);
  rpc UpdateAutomation(UpdateAutomationRequest) returns (UpdateAutomationResponse);
  rpc DeleteAutomation(DeleteAutomationRequest) returns (DeleteAutomationResponse);
  rpc GetAutomation(GetAutomationRequest) returns (GetAutomationResponse);
  rpc DisplayAutomations(DisplayAutomationsRequest) returns (DisplayAutomationsResponse);
  rpc AutomationState(AutomationStateRequest) returns (AutomationStateResponse);
  rpc PutConditionToAutomation(PutConditionToAutomationRequest) returns (PutConditionToAutomationResponse);
  rpc PutResultToAutomation(PutResultToAutomationRequest) returns (PutResultToAutomationResponse);
  rpc DeleteCondition(DeleteConditionRequest) returns (DeleteConditionResponse);
  rpc DeleteResult(DeleteResultRequest) returns (DeleteResultResponse);
}

enum ConditionConnector {
  CONDITION_CONNECTOR_UNSPECIFIED = 0;
  CONDITION_CONNECTOR_AND = 1;
  CONDITION_CONNECTOR_OR = 2;
}

enum ConditionType {
  CONDITION_TYPE_UNSPECIFIED = 0;
  CONDITION_TYPE_BOOLEAN = 1;
  CONDITION_TYPE_NUMBER = 2;
  CONDITION_TYPE_TEXT = 3;
}

message ConditionDto {
  string operation_id = 1;
  ConditionType condition_type = 2;
  google.protobuf.Struct condition_configuration = 3;
}

message ResultDto {
  string operation_id = 1;
  google.protobuf.Value value_to_send = 2;
}

message CreateAutomationRequest {
  string home_id = 1;
  string name = 2;
  string icon = 3;
  ConditionConnector condition_connector = 4;
  repeated ConditionDto conditions = 5;
  repeated ResultDto results = 6;
}

message UpdateAutomationRequest {
  string automation_id = 1;
  string name = 2;
  string icon = 3;
  ConditionConnector condition_connector = 4;
  repeated ConditionDto conditions = 5;
  repeated ResultDto results = 6;
}

message UpdateAutomationResponse {}

message DeleteAutomationRequest {
  string automation_id = 1;
}

message DeleteAutomationResponse {}

message ConditionModel {
  string id = 1;
  string operation_id = 2;
  string operation_name = 3;
  ConditionType condition_type = 4;
  google.protobuf.Struct condition_configuration = 5;
}

message ResultModel {
  string id = 1;
  string operation_id = 2;
  string operation_name = 3;
  google.protobuf.Value value_to_send = 4;
}

message AutomationModel {
  string id = 1;
  string home_id = 2;
  string name = 3;
  string icon = 4;
  ConditionConnector condition_connector = 5;
  bool is_enabled = 6;
  repeated ConditionModel conditions = 7;
  repeated ResultModel results = 8;
}

message GetAutomationRequest {
  string automation_id = 1;
}

message GetAutomationResponse {
  AutomationModel automation = 1;
}

message DisplayAutomationsRequest {
  string home_id = 1;
}

message DisplayAutomationsResponse {
  repeated AutomationModel items = 1;
}

message AutomationStateRequest {
  string automation_id = 1;
  bool is_enabled = 2;
}

message AutomationStateResponse {}

message PutConditionToAutomationRequest {
  string automation_id = 1;
  string operation_id = 2;
  ConditionType condition_type = 3;
  google.protobuf.Struct condition_configuration = 4;
}

message PutConditionToAutomationResponse {}

message PutResultToAutomationRequest {
  string automation_id = 1;
  string operation_id = 2;
  string value = 3;
}

message PutResultToAutomationResponse {}

message DeleteConditionRequest {
  string condition_id = 1;
}

message DeleteConditionResponse {}

message DeleteResultRequest {
  string result_id = 1;
}

message DeleteResultResponse {}
```

Note on `PutResultToAutomationRequest.value`: the REST `PutResultRequest.Value` is a plain `string` that the handler wraps into a `JsonValue` before sending to the command (unlike `ResultDto.ValueToSend`, which is already a `JsonValue` at the API boundary) — so this field stays `string`, not `google.protobuf.Value`, to match the actual REST contract being mirrored.

- [ ] **Step 2: Build and verify codegen**

Run: `dotnet build src/Shared/Senswave.Contracts/Senswave.Contracts.csproj`
Expected: `Build succeeded`.

```bash
grep -rl "class AutomationsServiceBase" src/Shared/Senswave.Contracts/obj
grep -rl "class AutomationsServiceClient" src/Shared/Senswave.Contracts/obj
```
Expected: both commands print a matching generated `.cs` path.

- [ ] **Step 3: Commit**

```bash
git add src/Shared/Senswave.Contracts/Protos/automations
git commit -m "feat: add automations/v1 proto contract

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 4: `datasources/v1/datasources.proto`

**Files:**
- Create: `src/Shared/Senswave.Contracts/Protos/datasources/v1/datasources.proto`

**Interfaces:**
- Consumes: `senswave.common.v1.IdResponse` (Task 2).
- Produces: `DataSourcesService` with 14 rpcs.

Source endpoints (`src/Modules/DataSource/Senswave.DataSources.Api`): `CreateBroker`, `CreateSubscribtion`, `DeleteBroker`, `DeleteSubscribtion`, `GetBroker`, `GetBrokers`, `GetSubscribtions`, `UpdateBroker`, `GetClientState`, `RestartClient`, `StartClient`, `StopClient`, `GetSession`, `GetSessions`. (`Subscribtion` is the existing spelling in the codebase — the rpc-naming rule keeps feature-folder names unchanged, typo included.)

- [ ] **Step 1: Write the proto file**

Create `src/Shared/Senswave.Contracts/Protos/datasources/v1/datasources.proto`:

```proto
syntax = "proto3";

package senswave.datasources.v1;

option csharp_namespace = "Senswave.Contracts.DataSources.V1";

import "google/protobuf/timestamp.proto";
import "common/v1/common.proto";

service DataSourcesService {
  rpc CreateBroker(CreateBrokerRequest) returns (senswave.common.v1.IdResponse);
  rpc CreateSubscribtion(CreateSubscribtionRequest) returns (senswave.common.v1.IdResponse);
  rpc DeleteBroker(DeleteBrokerRequest) returns (DeleteBrokerResponse);
  rpc DeleteSubscribtion(DeleteSubscribtionRequest) returns (DeleteSubscribtionResponse);
  rpc GetBroker(GetBrokerRequest) returns (GetBrokerResponse);
  rpc GetBrokers(GetBrokersRequest) returns (GetBrokersResponse);
  rpc GetSubscribtions(GetSubscribtionsRequest) returns (GetSubscribtionsResponse);
  rpc UpdateBroker(UpdateBrokerRequest) returns (UpdateBrokerResponse);
  rpc GetClientState(GetClientStateRequest) returns (GetClientStateResponse);
  rpc RestartClient(RestartClientRequest) returns (RestartClientResponse);
  rpc StartClient(StartClientRequest) returns (StartClientResponse);
  rpc StopClient(StopClientRequest) returns (StopClientResponse);
  rpc GetSession(GetSessionRequest) returns (GetSessionResponse);
  rpc GetSessions(GetSessionsRequest) returns (GetSessionsResponse);
}

message CreateBrokerRequest {
  string name = 1;
  string url = 2;
  string client_name = 3;
  int32 port = 4;
  string protocol_version = 5;
  bool use_tls = 6;
  string username = 7;
  string password = 8;
}

message CreateSubscribtionRequest {
  string broker_id = 1;
  string topic = 2;
}

message DeleteBrokerRequest {
  string broker_id = 1;
}

message DeleteBrokerResponse {}

message DeleteSubscribtionRequest {
  string broker_id = 1;
  string subscription_id = 2;
}

message DeleteSubscribtionResponse {}

message GetBrokerRequest {
  string broker_id = 1;
}

message GetBrokerResponse {
  string id = 1;
  string name = 2;
  string url = 3;
  string protocol_version = 4;
  string client_name = 5;
  int32 port = 6;
  bool use_tls = 7;
  google.protobuf.Timestamp created_at = 8;
  google.protobuf.Timestamp updated_at = 9;
}

message BrokerDto {
  string id = 1;
  string name = 2;
  string server = 3;
  google.protobuf.Timestamp created_at = 4;
  google.protobuf.Timestamp updated_at = 5;
}

message GetBrokersRequest {
  int32 page = 1;
  int32 size = 2;
}

message GetBrokersResponse {
  repeated BrokerDto items = 1;
}

message SubscribtionDto {
  string id = 1;
  string topic = 2;
  google.protobuf.Timestamp created_at = 3;
  google.protobuf.Timestamp updated_at = 4;
}

message GetSubscribtionsRequest {
  string broker_id = 1;
  int32 page = 2;
  int32 size = 3;
}

message GetSubscribtionsResponse {
  repeated SubscribtionDto items = 1;
}

message UpdateBrokerRequest {
  string broker_id = 1;
  string name = 2;
  string url = 3;
  string client_name = 4;
  optional int32 port = 5;
  string protocol_version = 6;
  optional bool use_tls = 7;
  string username = 8;
  string password = 9;
}

message UpdateBrokerResponse {}

message GetClientStateRequest {
  string broker_id = 1;
}

message GetClientStateResponse {
  string connection_status = 1;
  string latest_session_id = 2;
}

message RestartClientRequest {
  string broker_id = 1;
}

message RestartClientResponse {}

message StartClientRequest {
  string broker_id = 1;
  string username = 2;
  string password = 3;
}

message StartClientResponse {}

message StopClientRequest {
  string broker_id = 1;
}

message StopClientResponse {}

message LogDto {
  string id = 1;
  string event_type = 2;
  string data = 3;
  google.protobuf.Timestamp created_at_utc = 4;
}

message GetSessionRequest {
  string broker_id = 1;
  string session_id = 2;
}

message GetSessionResponse {
  string id = 1;
  repeated LogDto logs = 2;
  google.protobuf.Timestamp created_at_utc = 3;
  google.protobuf.Timestamp updated_at_utc = 4;
}

message SessionDto {
  string id = 1;
  google.protobuf.Timestamp updated_at_utc = 2;
  google.protobuf.Timestamp created_at_utc = 3;
  bool finished = 4;
}

message GetSessionsRequest {
  string broker_id = 1;
  int32 page = 2;
  int32 size = 3;
}

message GetSessionsResponse {
  repeated SessionDto items = 1;
}
```

- [ ] **Step 2: Build and verify codegen**

Run: `dotnet build src/Shared/Senswave.Contracts/Senswave.Contracts.csproj`
Expected: `Build succeeded`.

```bash
grep -rl "class DataSourcesServiceBase" src/Shared/Senswave.Contracts/obj
grep -rl "class DataSourcesServiceClient" src/Shared/Senswave.Contracts/obj
```
Expected: both print a matching path.

- [ ] **Step 3: Commit**

```bash
git add src/Shared/Senswave.Contracts/Protos/datasources
git commit -m "feat: add datasources/v1 proto contract

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 5: `devices/v1/devices.proto`

**Files:**
- Create: `src/Shared/Senswave.Contracts/Protos/devices/v1/devices.proto`

**Interfaces:**
- Consumes: `senswave.common.v1.IdResponse` (Task 2).
- Produces: `DevicesService` with 7 rpcs, and message names `DeviceTileDto`, `DevicePresenceDto`, `DisplayDeviceDto`, `GetDeviceTileDto`, `GetDevicePresenceDto` that must stay unique within `senswave.devices.v1` across this and Tasks 6–9.

Source endpoints (`src/Modules/Devices/Senswave.Devices.Api`, tag `DevicesModule.DevicesTag`): `CreateDevice`, `DeleteDevice`, `DeviceTileAction`, `DisplayDevice`, `DisplayDevices`, `GetDevice`, `UpdateDevice`. Two distinct C# DTO pairs exist for a "device tile"/"device presence" shape — one under the `DisplayDevice`/`DisplayDevices`/`DeviceTileAction` namespace, a differently-shaped one under `GetDevice` — so this file declares both under different names (`DeviceTileDto`/`DevicePresenceDto` vs. `GetDeviceTileDto`/`GetDevicePresenceDto`) rather than merging them, since they are genuinely different shapes in the source code.

- [ ] **Step 1: Write the proto file**

Create `src/Shared/Senswave.Contracts/Protos/devices/v1/devices.proto`:

```proto
syntax = "proto3";

package senswave.devices.v1;

option csharp_namespace = "Senswave.Contracts.Devices.V1";

import "google/protobuf/timestamp.proto";
import "google/protobuf/struct.proto";
import "common/v1/common.proto";

service DevicesService {
  rpc CreateDevice(CreateDeviceRequest) returns (senswave.common.v1.IdResponse);
  rpc DeleteDevice(DeleteDeviceRequest) returns (DeleteDeviceResponse);
  rpc DeviceTileAction(DeviceTileActionRequest) returns (DeviceTileActionResponse);
  rpc DisplayDevice(DisplayDeviceRequest) returns (DisplayDeviceResponse);
  rpc DisplayDevices(DisplayDevicesRequest) returns (DisplayDevicesResponse);
  rpc GetDevice(GetDeviceRequest) returns (GetDeviceResponse);
  rpc UpdateDevice(UpdateDeviceRequest) returns (UpdateDeviceResponse);
}

message CreateDeviceRequest {
  string home_id = 1;
  optional string room_id = 2;
  string name = 3;
  string icon = 4;
}

message DeleteDeviceRequest {
  string device_id = 1;
}

message DeleteDeviceResponse {}

message DeviceTileDto {
  string type = 1;
  google.protobuf.Value value = 2;
  google.protobuf.Struct configuration = 3;
}

message DevicePresenceDto {
  string type = 1;
  optional bool value = 2;
  google.protobuf.Timestamp last_seen_at_utc = 3;
}

message DisplayDeviceDto {
  string id = 1;
  optional string room_id = 2;
  string name = 3;
  string icon = 4;
  DeviceTileDto tile = 5;
  DevicePresenceDto presence = 6;
  google.protobuf.Timestamp created_at_utc = 7;
  google.protobuf.Timestamp updated_at_utc = 8;
}

message DeviceTileActionRequest {
  string device_id = 1;
  google.protobuf.Value value = 2;
}

message DeviceTileActionResponse {
  DisplayDeviceDto device = 1;
}

message DisplayDeviceRequest {
  string device_id = 1;
}

message DisplayDeviceResponse {
  DisplayDeviceDto device = 1;
}

message DisplayDevicesRequest {
  string home_id = 1;
  optional int32 page = 2;
  optional int32 size = 3;
}

message DisplayDevicesResponse {
  repeated DisplayDeviceDto items = 1;
}

message GetDeviceTileDto {
  string type = 1;
  optional string operation_id = 2;
  optional string displayable_operation_id = 3;
  google.protobuf.Struct configuration = 4;
}

message GetDevicePresenceDto {
  string type = 1;
  optional string operation_id = 2;
}

message GetDeviceRequest {
  string device_id = 1;
}

message GetDeviceResponse {
  string id = 1;
  string name = 2;
  string icon = 3;
  optional string room_id = 4;
  GetDeviceTileDto tile = 5;
  GetDevicePresenceDto presence = 6;
}

message UpdateDeviceRequest {
  string device_id = 1;
  optional string room_id = 2;
  string name = 3;
  string icon = 4;
  optional string operation_id = 5;
  optional string displayable_operation_id = 6;
  google.protobuf.Struct configuration = 7;
  string type = 8;
  string presence_operation_id = 9;
  string presence_type = 10;
}

message UpdateDeviceResponse {}
```

- [ ] **Step 2: Build and verify codegen**

Run: `dotnet build src/Shared/Senswave.Contracts/Senswave.Contracts.csproj`
Expected: `Build succeeded`.

```bash
grep -rl "class DevicesServiceBase" src/Shared/Senswave.Contracts/obj
grep -rl "class DevicesServiceClient" src/Shared/Senswave.Contracts/obj
```
Expected: both print a matching path.

- [ ] **Step 3: Commit**

```bash
git add src/Shared/Senswave.Contracts/Protos/devices/v1/devices.proto
git commit -m "feat: add devices/v1 devices.proto contract

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 6: `devices/v1/dashboards.proto`

**Files:**
- Create: `src/Shared/Senswave.Contracts/Protos/devices/v1/dashboards.proto`

**Interfaces:**
- Consumes: `senswave.common.v1.IdResponse` (Task 2). Shares package `senswave.devices.v1` with Task 5 — message names here (`DashboardDto`) must not collide with Task 5, 7, 8, or 9.
- Produces: `DashboardsService` with 8 rpcs.

Source endpoints (`src/Modules/Devices/Senswave.Devices.Api`, tag `DevicesModule.DashboardsTag`): `CreateDashboard`, `DeleteDashboard`, `DeleteDashboardWidget`, `DisplayDashboard`, `DisplayDashboards`, `GetDashboard`, `SetWidgetOnDashboard`, `UpdateDashboard`.

- [ ] **Step 1: Write the proto file**

Create `src/Shared/Senswave.Contracts/Protos/devices/v1/dashboards.proto`:

```proto
syntax = "proto3";

package senswave.devices.v1;

option csharp_namespace = "Senswave.Contracts.Devices.V1";

import "google/protobuf/struct.proto";
import "common/v1/common.proto";

service DashboardsService {
  rpc CreateDashboard(CreateDashboardRequest) returns (senswave.common.v1.IdResponse);
  rpc DeleteDashboard(DeleteDashboardRequest) returns (DeleteDashboardResponse);
  rpc DeleteDashboardWidget(DeleteDashboardWidgetRequest) returns (DeleteDashboardWidgetResponse);
  rpc DisplayDashboard(DisplayDashboardRequest) returns (DisplayDashboardResponse);
  rpc DisplayDashboards(DisplayDashboardsRequest) returns (DisplayDashboardsResponse);
  rpc GetDashboard(GetDashboardRequest) returns (GetDashboardResponse);
  rpc SetWidgetOnDashboard(SetWidgetOnDashboardRequest) returns (SetWidgetOnDashboardResponse);
  rpc UpdateDashboard(UpdateDashboardRequest) returns (UpdateDashboardResponse);
}

message CreateDashboardRequest {
  string device_id = 1;
  string name = 2;
  string icon = 3;
  google.protobuf.Struct configuration = 4;
}

message DeleteDashboardRequest {
  string dashboard_id = 1;
}

message DeleteDashboardResponse {}

message DeleteDashboardWidgetRequest {
  string dashboard_id = 1;
  string widget_id = 2;
}

message DeleteDashboardWidgetResponse {}

message DisplayDashboardRequest {
  string dashboard_id = 1;
}

message DisplayDashboardResponse {
  string type = 1;
  google.protobuf.Struct configuration = 2;
}

message DashboardDto {
  string id = 1;
  string name = 2;
  string icon = 3;
  string type = 4;
}

message DisplayDashboardsRequest {
  string device_id = 1;
}

message DisplayDashboardsResponse {
  repeated DashboardDto items = 1;
}

message GetDashboardRequest {
  string dashboard_id = 1;
}

message GetDashboardResponse {
  string id = 1;
  string name = 2;
  string icon = 3;
  string type = 4;
  google.protobuf.Struct configuration = 5;
}

message SetWidgetOnDashboardRequest {
  string dashboard_id = 1;
  string widget_id = 2;
  int32 row = 3;
  int32 row_span = 4;
  int32 column = 5;
  int32 column_span = 6;
}

message SetWidgetOnDashboardResponse {}

message UpdateDashboardRequest {
  string dashboard_id = 1;
  string name = 2;
  string icon = 3;
}

message UpdateDashboardResponse {}
```

- [ ] **Step 2: Build and verify codegen**

Run: `dotnet build src/Shared/Senswave.Contracts/Senswave.Contracts.csproj`
Expected: `Build succeeded` (this also re-validates Task 5 still compiles now that both files share a package).

```bash
grep -rl "class DashboardsServiceBase" src/Shared/Senswave.Contracts/obj
grep -rl "class DashboardsServiceClient" src/Shared/Senswave.Contracts/obj
```
Expected: both print a matching path.

- [ ] **Step 3: Commit**

```bash
git add src/Shared/Senswave.Contracts/Protos/devices/v1/dashboards.proto
git commit -m "feat: add devices/v1 dashboards.proto contract

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 7: `devices/v1/widgets.proto`

**Files:**
- Create: `src/Shared/Senswave.Contracts/Protos/devices/v1/widgets.proto`

**Interfaces:**
- Consumes: `senswave.common.v1.IdResponse` (Task 2). Shares package `senswave.devices.v1`.
- Produces: `WidgetsService` with 6 rpcs.

Source endpoints (`src/Modules/Devices/Senswave.Devices.Api`, tag `DevicesModule.WidgetsTag`): `Action`, `CreateWidget`, `DeleteWidget`, `DisplayWidgets`, `GetWidget`, `State`.

**Collision note:** the REST `DisplayWidgets` response nests an `OperationDto { Id, Name, Type }` shape that is a different C# class from, but structurally identical to, `operations.proto`'s `OperationDto` (Task 8) — declaring both as `OperationDto` in the shared `senswave.devices.v1` package would collide. This file names its copy `WidgetOperationDto` instead.

- [ ] **Step 1: Write the proto file**

Create `src/Shared/Senswave.Contracts/Protos/devices/v1/widgets.proto`:

```proto
syntax = "proto3";

package senswave.devices.v1;

option csharp_namespace = "Senswave.Contracts.Devices.V1";

import "google/protobuf/struct.proto";
import "common/v1/common.proto";

service WidgetsService {
  rpc Action(ActionRequest) returns (ActionResponse);
  rpc CreateWidget(CreateWidgetRequest) returns (senswave.common.v1.IdResponse);
  rpc DeleteWidget(DeleteWidgetRequest) returns (DeleteWidgetResponse);
  rpc DisplayWidgets(DisplayWidgetsRequest) returns (DisplayWidgetsResponse);
  rpc GetWidget(GetWidgetRequest) returns (GetWidgetResponse);
  rpc State(StateRequest) returns (StateResponse);
}

message ActionRequest {
  string widget_id = 1;
  google.protobuf.Value value = 2;
}

message ActionResponse {}

message CreateWidgetRequest {
  string operation_id = 1;
  string name = 2;
  string type = 3;
  google.protobuf.Struct configuration = 4;
}

message DeleteWidgetRequest {
  string widget_id = 1;
}

message DeleteWidgetResponse {}

message WidgetOperationDto {
  string id = 1;
  string name = 2;
  string type = 3;
}

message WidgetDto {
  string id = 1;
  string name = 2;
  string type = 3;
  bool enabled = 4;
}

message DisplayGroupDto {
  WidgetOperationDto operation = 1;
  repeated WidgetDto widgets = 2;
}

message DisplayWidgetsRequest {
  string device_id = 1;
}

message DisplayWidgetsResponse {
  repeated DisplayGroupDto items = 1;
}

message GetWidgetRequest {
  string widget_id = 1;
}

message GetWidgetResponse {
  string id = 1;
  string device_id = 2;
  string operation_id = 3;
  string name = 4;
  string type = 5;
  bool enabled = 6;
  google.protobuf.Struct configuration = 7;
}

message StateRequest {
  string widget_id = 1;
  bool enabled = 2;
}

message StateResponse {}
```

Note on `Action`: the REST `WidgetActionResponse` class (`List<DisplayWidgetDto> Items`) exists in the C# source but is dead code — the actual endpoint handler returns `Results.NoContent()`. Per the field-derivation rule ("fields come from the existing DTOs"), this plan mirrors what the endpoint actually does, not the unused class, so `ActionResponse` is empty.

- [ ] **Step 2: Build and verify codegen**

Run: `dotnet build src/Shared/Senswave.Contracts/Senswave.Contracts.csproj`
Expected: `Build succeeded` — in particular, no "message already defined" error for `OperationDto`, confirming the `WidgetOperationDto` rename avoided the collision with Task 8.

```bash
grep -rl "class WidgetsServiceBase" src/Shared/Senswave.Contracts/obj
grep -rl "class WidgetsServiceClient" src/Shared/Senswave.Contracts/obj
```
Expected: both print a matching path.

- [ ] **Step 3: Commit**

```bash
git add src/Shared/Senswave.Contracts/Protos/devices/v1/widgets.proto
git commit -m "feat: add devices/v1 widgets.proto contract

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 8: `devices/v1/operations.proto`

**Files:**
- Create: `src/Shared/Senswave.Contracts/Protos/devices/v1/operations.proto`

**Interfaces:**
- Consumes: `senswave.common.v1.IdResponse` (Task 2). Shares package `senswave.devices.v1`.
- Produces: `OperationsService` with 4 rpcs, and the canonical `OperationDto` name in this package (Task 7 uses `WidgetOperationDto` to avoid colliding with this one).

Source endpoints (`src/Modules/Devices/Senswave.Devices.Api`, tag `DevicesModule.OperationsTag`): `CreateOperation`, `DeleteOperation`, `DisplayOperations`, `GetOperation`.

- [ ] **Step 1: Write the proto file**

Create `src/Shared/Senswave.Contracts/Protos/devices/v1/operations.proto`:

```proto
syntax = "proto3";

package senswave.devices.v1;

option csharp_namespace = "Senswave.Contracts.Devices.V1";

import "google/protobuf/struct.proto";
import "common/v1/common.proto";

service OperationsService {
  rpc CreateOperation(CreateOperationRequest) returns (senswave.common.v1.IdResponse);
  rpc DeleteOperation(DeleteOperationRequest) returns (DeleteOperationResponse);
  rpc DisplayOperations(DisplayOperationsRequest) returns (DisplayOperationsResponse);
  rpc GetOperation(GetOperationRequest) returns (GetOperationResponse);
}

message CreateOperationRequest {
  string device_id = 1;
  string name = 2;
  string type = 3;
  google.protobuf.Struct configuration = 4;
  string topic = 5;
}

message DeleteOperationRequest {
  string operation_id = 1;
}

message DeleteOperationResponse {}

message OperationDto {
  string id = 1;
  string name = 2;
  string type = 3;
}

message DisplayOperationsRequest {
  string device_id = 1;
  int32 page = 2;
  int32 size = 3;
}

message DisplayOperationsResponse {
  repeated OperationDto items = 1;
}

message GetOperationRequest {
  string operation_id = 1;
}

message GetOperationResponse {
  string id = 1;
  string topic = 2;
  string name = 3;
  string type = 4;
  google.protobuf.Struct configuration = 5;
}
```

`DisplayOperationsRequest.page`/`.size` are plain (non-`optional`) `int32`, unlike `DisplayDevicesRequest` in Task 5 — the REST query params here bind as `int page = 1, int size = 10` (non-nullable with an ASP.NET default), not `int?`, so per the Global Constraints optionality rule this is a required-presence scalar, not an `optional` one.

- [ ] **Step 2: Build and verify codegen**

Run: `dotnet build src/Shared/Senswave.Contracts/Senswave.Contracts.csproj`
Expected: `Build succeeded` — confirms `OperationDto` here and `WidgetOperationDto` in Task 7 coexist without collision.

```bash
grep -rl "class OperationsServiceBase" src/Shared/Senswave.Contracts/obj
grep -rl "class OperationsServiceClient" src/Shared/Senswave.Contracts/obj
```
Expected: both print a matching path.

- [ ] **Step 3: Commit**

```bash
git add src/Shared/Senswave.Contracts/Protos/devices/v1/operations.proto
git commit -m "feat: add devices/v1 operations.proto contract

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 9: `devices/v1/sharing.proto`

**Files:**
- Create: `src/Shared/Senswave.Contracts/Protos/devices/v1/sharing.proto`

**Interfaces:**
- Shares package `senswave.devices.v1`. No `IdResponse` usage — none of its rpcs return a bare id.
- Produces: `SharingService` with 3 rpcs (device sharing). Distinct from Homes' `SharingService` in Task 12, which lives in package `senswave.homes.v1` and therefore does not collide despite the identical service name — both are named after their module's "Sharing" tag per the service-naming rule.

Source endpoints (`src/Modules/Devices/Senswave.Devices.Api`, tag `DevicesModule.SharingTag`): `DeleteSharing`, `GetSharings`, `SetSharing`.

- [ ] **Step 1: Write the proto file**

Create `src/Shared/Senswave.Contracts/Protos/devices/v1/sharing.proto`:

```proto
syntax = "proto3";

package senswave.devices.v1;

option csharp_namespace = "Senswave.Contracts.Devices.V1";

service SharingService {
  rpc DeleteSharing(DeleteSharingRequest) returns (DeleteSharingResponse);
  rpc GetSharings(GetSharingsRequest) returns (GetSharingsResponse);
  rpc SetSharing(SetSharingRequest) returns (SetSharingResponse);
}

message DeleteSharingRequest {
  string device_sharing_id = 1;
}

message DeleteSharingResponse {}

message SharingDto {
  optional string sharing_id = 1;
  string friend_email = 2;
  string sharing_type = 3;
}

message GetSharingsRequest {
  string device_id = 1;
}

message GetSharingsResponse {
  repeated SharingDto items = 1;
}

message SetSharingRequest {
  string device_id = 1;
  string sharing_type = 2;
  string friend_email = 3;
}

message SetSharingResponse {}
```

`SharingDto.sharing_id` is `optional` here because the REST `SharingDto` in this module is a record with `Guid? SharingId` — unlike Homes' `SharingDto` (Task 12), whose `SharingId` is a non-nullable `Guid`.

- [ ] **Step 2: Build and verify codegen — full module check**

Run: `dotnet build src/Shared/Senswave.Contracts/Senswave.Contracts.csproj`
Expected: `Build succeeded`. This is the fifth and final file in `senswave.devices.v1`, so also confirm the whole package resolved with no cross-file name collisions:

```bash
grep -rl "class SharingServiceBase" src/Shared/Senswave.Contracts/obj/**/Devices
```
Expected: prints a matching path (the devices-package `SharingServiceBase`, distinct from the homes-package one Task 12 will add later).

- [ ] **Step 3: Commit**

```bash
git add src/Shared/Senswave.Contracts/Protos/devices/v1/sharing.proto
git commit -m "feat: add devices/v1 sharing.proto contract

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 10: `homes/v1/homes.proto`

**Files:**
- Create: `src/Shared/Senswave.Contracts/Protos/homes/v1/homes.proto`

**Interfaces:**
- Consumes: `senswave.common.v1.IdResponse` (Task 2).
- Produces: `HomesService` with 8 rpcs, and message names `LocationDto`, `DataSourceDto`, `HomeRoomDto`, `HomeDto` that must stay unique within `senswave.homes.v1` across this and Tasks 11–12.

Source endpoints (`src/Modules/Homes/Senswave.Homes.Api`, tag `HomesModule.HomesTag`): `CreateHome`, `DeleteHome`, `DeleteHomeDataSource`, `GetCurrentHome`, `GetHome`, `GetHomes`, `SetHomeDataSource`, `UpdateHome`.

**Collision note:** `GetHomeResponse` nests a `RoomDto { Id, Name }` shape that is a different C# class from, but structurally identical to, `rooms.proto`'s `RoomDto` (Task 11). This file names its copy `HomeRoomDto` to avoid the collision.

- [ ] **Step 1: Write the proto file**

Create `src/Shared/Senswave.Contracts/Protos/homes/v1/homes.proto`:

```proto
syntax = "proto3";

package senswave.homes.v1;

option csharp_namespace = "Senswave.Contracts.Homes.V1";

import "common/v1/common.proto";

service HomesService {
  rpc CreateHome(CreateHomeRequest) returns (senswave.common.v1.IdResponse);
  rpc DeleteHome(DeleteHomeRequest) returns (DeleteHomeResponse);
  rpc DeleteHomeDataSource(DeleteHomeDataSourceRequest) returns (DeleteHomeDataSourceResponse);
  rpc GetCurrentHome(GetCurrentHomeRequest) returns (GetCurrentHomeResponse);
  rpc GetHome(GetHomeRequest) returns (GetHomeResponse);
  rpc GetHomes(GetHomesRequest) returns (GetHomesResponse);
  rpc SetHomeDataSource(SetHomeDataSourceRequest) returns (SetHomeDataSourceResponse);
  rpc UpdateHome(UpdateHomeRequest) returns (UpdateHomeResponse);
}

message CreateHomeRequest {
  optional string data_source_id = 1;
  string name = 2;
  string icon = 3;
  optional double latitude = 4;
  optional double longitude = 5;
}

message DeleteHomeRequest {
  string home_id = 1;
}

message DeleteHomeResponse {}

message DeleteHomeDataSourceRequest {
  string home_id = 1;
}

message DeleteHomeDataSourceResponse {}

message GetCurrentHomeRequest {
  optional double latitude = 1;
  optional double longitude = 2;
}

message GetCurrentHomeResponse {
  string id = 1;
}

message LocationDto {
  double longitude = 1;
  double latitude = 2;
}

message DataSourceDto {
  optional string id = 1;
  string state = 2;
  string name = 3;
}

message HomeRoomDto {
  string id = 1;
  string name = 2;
}

message GetHomeRequest {
  string home_id = 1;
}

message GetHomeResponse {
  string id = 1;
  string name = 2;
  string icon = 3;
  bool is_owner = 4;
  DataSourceDto data_source = 5;
  LocationDto location = 6;
  repeated HomeRoomDto rooms = 7;
}

message HomeDto {
  string id = 1;
  optional string data_source_id = 2;
  string name = 3;
  string icon = 4;
  bool is_owner = 5;
  LocationDto location = 6;
}

message GetHomesRequest {
  int32 page = 1;
  int32 size = 2;
}

message GetHomesResponse {
  repeated HomeDto items = 1;
}

message SetHomeDataSourceRequest {
  string home_id = 1;
  string broker_id = 2;
}

message SetHomeDataSourceResponse {}

message UpdateHomeRequest {
  string home_id = 1;
  optional string data_source_id = 2;
  string name = 3;
  string icon = 4;
  optional double latitude = 5;
  optional double longitude = 6;
}

message UpdateHomeResponse {}
```

- [ ] **Step 2: Build and verify codegen**

Run: `dotnet build src/Shared/Senswave.Contracts/Senswave.Contracts.csproj`
Expected: `Build succeeded`.

```bash
grep -rl "class HomesServiceBase" src/Shared/Senswave.Contracts/obj
grep -rl "class HomesServiceClient" src/Shared/Senswave.Contracts/obj
```
Expected: both print a matching path.

- [ ] **Step 3: Commit**

```bash
git add src/Shared/Senswave.Contracts/Protos/homes/v1/homes.proto
git commit -m "feat: add homes/v1 homes.proto contract

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 11: `homes/v1/rooms.proto`

**Files:**
- Create: `src/Shared/Senswave.Contracts/Protos/homes/v1/rooms.proto`

**Interfaces:**
- Consumes: `senswave.common.v1.IdResponse` (Task 2). Shares package `senswave.homes.v1` with Task 10 and Task 12.
- Produces: `RoomsService` with 5 rpcs, and the canonical `RoomDto` name in this package (Task 10 uses `HomeRoomDto` for its own distinct nested shape to avoid colliding with this one).

Source endpoints (`src/Modules/Homes/Senswave.Homes.Api`, tag `HomesModule.RoomsTag`): `CreateRoom`, `DeleteRoom`, `DisplayRooms`, `GetRoom`, `UpdateRoom`.

- [ ] **Step 1: Write the proto file**

Create `src/Shared/Senswave.Contracts/Protos/homes/v1/rooms.proto`:

```proto
syntax = "proto3";

package senswave.homes.v1;

option csharp_namespace = "Senswave.Contracts.Homes.V1";

import "common/v1/common.proto";

service RoomsService {
  rpc CreateRoom(CreateRoomRequest) returns (senswave.common.v1.IdResponse);
  rpc DeleteRoom(DeleteRoomRequest) returns (DeleteRoomResponse);
  rpc DisplayRooms(DisplayRoomsRequest) returns (DisplayRoomsResponse);
  rpc GetRoom(GetRoomRequest) returns (GetRoomResponse);
  rpc UpdateRoom(UpdateRoomRequest) returns (UpdateRoomResponse);
}

message CreateRoomRequest {
  string home_id = 1;
  string name = 2;
}

message DeleteRoomRequest {
  string home_id = 1;
  string room_id = 2;
}

message DeleteRoomResponse {}

message RoomDto {
  string id = 1;
  string name = 2;
}

message DisplayRoomsRequest {
  string home_id = 1;
}

message DisplayRoomsResponse {
  repeated RoomDto items = 1;
}

message GetRoomRequest {
  string home_id = 1;
  string room_id = 2;
}

message GetRoomResponse {
  string id = 1;
  string name = 2;
}

message UpdateRoomRequest {
  string home_id = 1;
  string room_id = 2;
  string name = 3;
}

message UpdateRoomResponse {}
```

`UpdateRoomRequest` carries `home_id` even though the current REST handler's method signature only binds `roomId` from the route (the `homeId` route segment is present in the URL template but unused by the handler) — a gRPC message has no URL to carry route segments implicitly, so every route parameter the REST route exposes becomes an explicit field here regardless of whether today's handler happens to read it.

- [ ] **Step 2: Build and verify codegen**

Run: `dotnet build src/Shared/Senswave.Contracts/Senswave.Contracts.csproj`
Expected: `Build succeeded` — confirms `RoomDto` here and `HomeRoomDto` in Task 10 coexist without collision.

```bash
grep -rl "class RoomsServiceBase" src/Shared/Senswave.Contracts/obj
grep -rl "class RoomsServiceClient" src/Shared/Senswave.Contracts/obj
```
Expected: both print a matching path.

- [ ] **Step 3: Commit**

```bash
git add src/Shared/Senswave.Contracts/Protos/homes/v1/rooms.proto
git commit -m "feat: add homes/v1 rooms.proto contract

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 12: `homes/v1/sharing.proto`

**Files:**
- Create: `src/Shared/Senswave.Contracts/Protos/homes/v1/sharing.proto`

**Interfaces:**
- Shares package `senswave.homes.v1`. No `IdResponse` usage — `CreateSharingResponse` carries more than just an id, so it keeps its own dedicated message per the Global Constraints message-naming exception.
- Produces: `SharingService` with 5 rpcs (home sharing). Distinct from Devices' `SharingService` (Task 9), which lives in package `senswave.devices.v1`.

Source endpoints (`src/Modules/Homes/Senswave.Homes.Api`, tag `HomesModule.SharingTag`): `AcceptSharing`, `CreateSharing`, `DeleteSharing`, `GetSharings`, `LeaveSharing`.

- [ ] **Step 1: Write the proto file**

Create `src/Shared/Senswave.Contracts/Protos/homes/v1/sharing.proto`:

```proto
syntax = "proto3";

package senswave.homes.v1;

option csharp_namespace = "Senswave.Contracts.Homes.V1";

import "google/protobuf/timestamp.proto";

service SharingService {
  rpc AcceptSharing(AcceptSharingRequest) returns (AcceptSharingResponse);
  rpc CreateSharing(CreateSharingRequest) returns (CreateSharingResponse);
  rpc DeleteSharing(DeleteSharingRequest) returns (DeleteSharingResponse);
  rpc GetSharings(GetSharingsRequest) returns (GetSharingsResponse);
  rpc LeaveSharing(LeaveSharingRequest) returns (LeaveSharingResponse);
}

message AcceptSharingRequest {
  string password = 1;
}

message AcceptSharingResponse {}

message CreateSharingRequest {
  string home_id = 1;
  string friend_email = 2;
  string sharing_type = 3;
}

message CreateSharingResponse {
  string invitation_id = 1;
  string password = 2;
  google.protobuf.Timestamp expires_at_utc = 3;
  google.protobuf.Timestamp created_utc = 4;
}

message DeleteSharingRequest {
  string home_sharing_id = 1;
}

message DeleteSharingResponse {}

message SharingDto {
  string sharing_id = 1;
  string friend_email = 2;
  string sharing_type = 3;
}

message GetSharingsRequest {
  string home_id = 1;
}

message GetSharingsResponse {
  repeated SharingDto items = 1;
}

message LeaveSharingRequest {
  string home_id = 1;
}

message LeaveSharingResponse {}
```

`SharingDto.sharing_id` is a plain (non-`optional`) `string` here, unlike Devices' `SharingDto` (Task 9) — the REST `SharingDto` in this module has a non-nullable `Guid SharingId`.

- [ ] **Step 2: Build and verify codegen — full module check**

Run: `dotnet build src/Shared/Senswave.Contracts/Senswave.Contracts.csproj`
Expected: `Build succeeded`. This is the third and final file in `senswave.homes.v1`, so also confirm no cross-file collisions:

```bash
grep -rl "class SharingServiceBase" src/Shared/Senswave.Contracts/obj/**/Homes
```
Expected: prints a matching path (the homes-package `SharingServiceBase`, distinct from the devices-package one from Task 9).

- [ ] **Step 3: Commit**

```bash
git add src/Shared/Senswave.Contracts/Protos/homes/v1/sharing.proto
git commit -m "feat: add homes/v1 sharing.proto contract

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 13: `users/v1/users.proto`

**Files:**
- Create: `src/Shared/Senswave.Contracts/Protos/users/v1/users.proto`

**Interfaces:**
- Produces: `UsersService` with 3 rpcs, covering only the endpoints Spec 0 marks as converted.

Source endpoints (`src/Modules/Users/Senswave.Users.Api`): `CreateConsents`, `DeleteAccount`, `GetUser`. Per Spec 0's endpoint inventory, these are the **only** 3 of Users' 8 endpoints that convert — everything under `Auth/` and `Legal/` (`ConfirmEmailV2Endpoint`, `GoogleAuthEndpoint`, `GoogleJwtAuthEndpoint`, `GetPrivacyPolicyEndpoint`, `GetTermsAndConditionsEndpoint`) plus the ASP.NET Identity API surface under `api/v1/auth` stay REST and are **not** represented here. All 3 read the caller's user id from `IRequestContext.UserId` (the authenticated principal), not from any request field — Spec A's `GrpcRequestContext` resolves this the same way `ServerCallContext.GetHttpContext().User` does for every other converted rpc, so no user-id field belongs on these request messages.

- [ ] **Step 1: Write the proto file**

Create `src/Shared/Senswave.Contracts/Protos/users/v1/users.proto`:

```proto
syntax = "proto3";

package senswave.users.v1;

option csharp_namespace = "Senswave.Contracts.Users.V1";

service UsersService {
  rpc CreateConsents(CreateConsentsRequest) returns (CreateConsentsResponse);
  rpc DeleteAccount(DeleteAccountRequest) returns (DeleteAccountResponse);
  rpc GetUser(GetUserRequest) returns (GetUserResponse);
}

message CreateConsentsRequest {}

message CreateConsentsResponse {}

message DeleteAccountRequest {}

message DeleteAccountResponse {}

message GetUserRequest {}

message GetUserResponse {
  string id = 1;
  string email = 2;
  string theme = 3;
  string language = 4;
  bool has_active_consent = 5;
}
```

- [ ] **Step 2: Build and verify codegen**

Run: `dotnet build src/Shared/Senswave.Contracts/Senswave.Contracts.csproj`
Expected: `Build succeeded`.

```bash
grep -rl "class UsersServiceBase" src/Shared/Senswave.Contracts/obj
grep -rl "class UsersServiceClient" src/Shared/Senswave.Contracts/obj
```
Expected: both print a matching path.

- [ ] **Step 3: Commit**

```bash
git add src/Shared/Senswave.Contracts/Protos/users/v1/users.proto
git commit -m "feat: add users/v1 proto contract

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 14: `liveupdates/v1/liveupdates.proto`

**Files:**
- Create: `src/Shared/Senswave.Contracts/Protos/liveupdates/v1/liveupdates.proto`

**Interfaces:**
- Produces: `LiveUpdatesService` with one server-streaming rpc, the contract that replaces the `LiveUpdatesHub` SignalR hub in Spec A. This message content is dictated verbatim by Spec 0 itself (Spec 0 §"`liveupdates/v1/liveupdates.proto`"), not derived from a REST DTO like the other 12 files — there is no REST equivalent, since live updates are the SignalR hub being replaced.

- [ ] **Step 1: Write the proto file**

Create `src/Shared/Senswave.Contracts/Protos/liveupdates/v1/liveupdates.proto`:

```proto
syntax = "proto3";

package senswave.liveupdates.v1;

option csharp_namespace = "Senswave.Contracts.LiveUpdates.V1";

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

This is the one rpc in the whole contract set that uses `stream` — a hard requirement from Spec 0: gRPC-Web (Spec B) supports unary and server-streaming only, so no rpc anywhere in `Senswave.Contracts` may use client-streaming or bidirectional streaming. There is exactly one `stream` keyword in this file and none in any of Tasks 3–13; the final verification task below re-checks this invariant across the whole project.

- [ ] **Step 2: Build and verify codegen**

Run: `dotnet build src/Shared/Senswave.Contracts/Senswave.Contracts.csproj`
Expected: `Build succeeded`.

```bash
grep -rl "class LiveUpdatesServiceBase" src/Shared/Senswave.Contracts/obj
grep -rl "class LiveUpdatesServiceClient" src/Shared/Senswave.Contracts/obj
```
Expected: both print a matching path. Additionally confirm the generated client exposes a streaming call, not a unary one:

```bash
grep -rl "AsyncServerStreamingCall<LiveUpdate>" src/Shared/Senswave.Contracts/obj
```
Expected: prints a matching path (proves codegen emitted a server-streaming client method, not a unary one).

- [ ] **Step 3: Commit**

```bash
git add src/Shared/Senswave.Contracts/Protos/liveupdates
git commit -m "feat: add liveupdates/v1 streaming proto contract

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 15: Full-solution verification

**Files:** none (verification only).

**Interfaces:**
- Consumes: every proto file from Tasks 2–14.
- Produces: nothing new — this task exists to prove the addition of `Senswave.Contracts` didn't disturb anything else in the solution, matching Spec 0's acceptance criteria.

- [ ] **Step 1: Confirm no client/bidirectional streaming crept in**

```bash
grep -rn "rpc .*stream .*returns (stream\|rpc .*(stream " src/Shared/Senswave.Contracts/Protos
```
Expected: the only match is `liveupdates.proto`'s `rpc Subscribe(SubscribeRequest) returns (stream LiveUpdate);` — a server-streaming rpc (`stream` appears only on the response side). If any `stream` keyword appears on a request side anywhere, that is a Spec 0 violation and must be fixed before continuing (Spec 0 explicitly forbids client-streaming and bidirectional rpcs across the entire contract set).

- [ ] **Step 2: Build the whole solution**

Run: `dotnet build Senswave.sln`
Expected: `Build succeeded`, with no other project's build output changed (no other `.csproj` in the solution references `Senswave.Contracts` yet, so this is purely additive).

- [ ] **Step 3: Run the full existing test suite**

Run: `dotnet test`
Expected: every existing test still passes — this plan added a new library with no consumers, so no existing behavior should change. If any test fails, the failure is unrelated to this plan's changes (nothing here touches runtime code) and should be investigated separately before proceeding, not patched over.

- [ ] **Step 4: Confirm `Senswave.ArchitectureTests` still passes**

Run: `dotnet test test/Shared/Senswave.ArchitectureTests/Senswave.ArchitectureTests.csproj`
Expected: all pass unchanged — this plan adds no `Command`/`Query`/module-layering violation for `NetArchTest` to catch, since `Senswave.Contracts` is a standalone shared library outside the module-layering rules those tests assert on `src/Modules/**`.

- [ ] **Step 5: Tally generated services against the spec's endpoint inventory**

```bash
grep -rhoE "class [A-Za-z]+ServiceBase" src/Shared/Senswave.Contracts/obj | sort -u
```
Expected output — exactly 10 lines, one per service, matching Spec 0's table:

```
class AutomationsServiceBase
class DashboardsServiceBase
class DataSourcesServiceBase
class DevicesServiceBase
class HomesServiceBase
class LiveUpdatesServiceBase
class OperationsServiceBase
class RoomsServiceBase
class SharingServiceBase
class UsersServiceBase
class WidgetsServiceBase
```

(Note: `SharingServiceBase` appears twice in the raw `obj` tree — once generated under the `Devices` namespace, once under `Homes` — but `sort -u` collapses them to one line since the class *name* is identical even though the fully-qualified type differs; that is expected and matches the by-design service-name reuse documented in Tasks 9 and 12.)

- [ ] **Step 6: Commit the plan's completion marker (none needed)**

No commit here — Task 15 is verification-only and makes no file changes. If Steps 1–5 all pass, the plan is complete.

---

## Self-Review

**Spec coverage:** All three parts of Spec 0's endpoint inventory table are covered — Devices (28 → Tasks 5–9), Homes (18 → Tasks 10–12), DataSources (14 → Task 4), Automations (10 → Task 3), Users (3 of 8 → Task 13) = 73, plus the `LiveUpdatesService` streaming contract (Task 14) = the full inventory. `common/v1/common.proto`'s `IdResponse`/`Error`/`ErrorType` (Task 2) and the four-package registration (Task 1) are both explicit Spec 0 deliverables. Deliverable 2 (`Senswave.Infrastructure.Grpc`) and the remaining three Deliverable 3 packages are explicitly and intentionally out of scope per the Scope Check, for a follow-up plan.

**Placeholder scan:** every task's proto file is complete, real content — no `TODO`, no "similar to Task N", no elided message bodies. Every field in every message traces back to a specific C# class an exploration pass actually read.

**Type consistency:** `senswave.common.v1.IdResponse` is referenced identically (fully qualified) in Tasks 3, 4, 5, 6, 7, 8, 10, 11. Every intra-package collision found during design (Devices' `OperationDto` vs. `WidgetOperationDto`; Homes' `RoomDto` vs. `HomeRoomDto`) is resolved consistently in both the file that keeps the plain name and the file that takes the renamed one, and each rename is called out in both tasks' prose so a reviewer approving one task in isolation still sees the reason. Service name reuse across different packages (`SharingService` in both `senswave.devices.v1` and `senswave.homes.v1`) is called out in both Task 9 and Task 12 rather than assumed obvious.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-09-19-grpc-proto-contracts.md`. Two execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
