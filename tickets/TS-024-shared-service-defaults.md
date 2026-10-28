---
key: TS-024
title: Extract shared service plumbing into TinyShop.ServiceDefaults
type: Task
status: To Do
priority: High
points: 5
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, dotnet, refactoring, architecture, ci, testing]
depends_on: [TS-013]
created: 2026-10-27
---

# TS-024: Extract shared service plumbing into TinyShop.ServiceDefaults

## Summary
Move the technical plumbing that product-, user- and order-service each carry a copy of into two shared projects, `TinyShop.ServiceDefaults` and `TinyShop.Testing`, referenced by every .NET service. Business logic stays in each service. Done before the gateway (TS-014), which would otherwise be the fourth copy, and before M2 adds OpenTelemetry to every service.

## Background
- [TS-011](TS-011-user-service.md#what-was-copied-from-product-service-input-for-the-share-it-discussion) listed about 1,000 near-identical lines and deferred the "share it?" question to the third copy. TS-013 made that third copy.
- **Decision D25** (recorded in the epic): technical plumbing is shared through projects under `tinyshop/shared/`, referenced as project references; business logic, DTOs, contracts and service-specific error codes are never shared. This narrows D16's "no project may reference another service's projects": a service may reference `shared/`, still never another service.
- The industry pattern is the **microservice chassis** (Chris Richardson): one library for cross-cutting concerns. In .NET it's usually a `ServiceDefaults` project with `AddServiceDefaults()`/`UseServiceDefaults()` (as in Microsoft's eShop reference app). Here it's written by hand: D2 rules out .NET Aspire, and the name is only borrowed.
- **Why a project reference, not a NuGet package:** one repository, one team, no package registry. The trade-off: a change to `shared/` affects every service at once, so every service's CI must run on it (the `dotnet` matrix already runs every service on every push). Question 3 asks what an internal NuGet package would change.

## Scope
**In scope**
- `tinyshop/shared/TinyShop.ServiceDefaults/` (class library, `FrameworkReference Microsoft.AspNetCore.App`):
  - Problem Details: the customizer, the generic codes (`validation_failed`, `not_found`, `method_not_allowed`, `unsupported_media_type`, `unsupported_api_version`, `service_unavailable`, `internal_error`, and the status-to-code mapping), and a way for each service to register **its own** codes and titles (`user_not_found`, `payment_timeout`, …)
  - API versioning and Swagger setup; health endpoint mapping (`/health/live`, `/health/ready` with the `ready` tag); the exception handler pipeline
  - Entry points: `services.AddServiceDefaults(...)` and `app.UseServiceDefaults()`
- Database exception translation (`DataStoreUnavailableException`, the transient-Npgsql check): in ServiceDefaults or a small `TinyShop.ServiceDefaults.Data` project, so services without a database (payment-service, the gateway) don't pull in Npgsql. Decide and record which
- `tinyshop/shared/TinyShop.Testing/`: `ResponseAssertions`, a Postgres fixture base (container with the role script, migrator/app connection strings), the closed-port helper
- Migrate product-, user- and order-service to them; delete the copies. payment-service (gRPC, no Problem Details) adopts only what fits, if anything
- `check-service-boundaries.sh`: allow references into `tinyshop/shared/`; still reject references into another service, and reject `shared/` referencing any service
- Every Dockerfile copies `shared/` (`.csproj` files before restore, sources after); the build context is already `tinyshop/`
- Docs: architecture.md (repository layout, the boundary rule), epic D16/D25; TS-011's duplication list points here

**Out of scope**
- OpenTelemetry (M2; this ticket gives it a single home)
- A NuGet package or package feed
- Each service's `DependencyInjection`, controllers, domain, persistence and dependency clients
- Behaviour changes: every existing test passes unchanged, apart from moved test-support code

## Acceptance criteria
- [ ] AC1: product-, user- and order-service use `TinyShop.ServiceDefaults`; the copied files listed in TS-011 are gone from them. Every existing unit and integration test passes, unchanged except for the moved support code.
- [ ] AC2: Every error response is byte-for-byte the same shape as before (type, title, status, detail, instance, code, traceId; service-specific codes included). The integration tests prove it.
- [ ] AC3: `shared/` contains no domain types, DTOs, `DbContext`s, service-specific error codes or clients for other services. The rule is written in a README in `tinyshop/shared/`.
- [ ] AC4: The boundary check passes, and fails on purpose when a service references another service or `shared/` references a service (shown once, then reverted).
- [ ] AC5: All images build (CI `image` job) and the Compose stack comes up healthy.
- [ ] AC6: D25 is in the epic, D16 points to it, and architecture.md describes `shared/`.

## Technical notes
- Start with the smallest slice end to end (for example health endpoint mapping) across all three services, then grow it. Each step should leave every test green.
- The service-specific part of the error catalog is the one design decision with some depth: options include each service passing a dictionary of its codes to `AddServiceDefaults`, or registering an `IErrorCodeProvider`. Keep the controllers' `Problem(type: ErrorCatalog.TypeUri(...))` style working.
- Shared projects inherit `tinyshop/Directory.Build.props` and central package versions automatically.
- The test-support project references xUnit and Testcontainers; only test projects may reference it.
- Watch for CS0118: avoid namespace segments that collide with type names (see TS-012).

## Learning objectives
- Why microservices tolerate duplication, and where the line between "plumbing" and "business logic" sits
- The microservice chassis pattern, and how a shared library couples deployments even without a network call
- Project reference versus package: versioning, release cadence, and what CI has to rebuild

## Questions to answer
Write the answers in `notes/`.
1. Name two things that must never go into `shared/`, and the concrete failure each would cause later.
2. A change to `TinyShop.ServiceDefaults` breaks only order-service. With project references, what happens to the other services' next deployment? What would happen with a versioned package?
3. Sketch how this would work as an internal NuGet package instead: where it's published, how a service picks up a new version, and what Dependabot and CI would do differently.
4. When would you go back to duplicating instead of sharing?

## Definition of done
- [ ] All acceptance criteria met
- [ ] Questions answered in `notes/`
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-27: Created, from the "share it?" question raised in TS-011 and left open in TS-013. D25 decided
- 2026-10-27: Phase 0 (baseline, no code changes), at commit `bf49ac9`. All four solutions green: 336 tests (product 63, user 61, payment 33, order 179), format and boundary check clean. 57 characterization snapshots recorded (every HTTP service's success, error, health, dependency-outage and database-outage responses, plus the three Swagger documents; trace IDs, timestamps and transaction IDs blanked); captured twice, identical. Duplication inventory: ProblemDetailsCustomizer, WebApplicationExtensions, Program, DataStoreUnavailableException, ResponseAssertions and HealthTests are identical across services; ErrorCatalog differs only in service-specific codes; ConfigureSwaggerOptions in title and description; DatabaseExceptionTranslator in one message; order-service's ServiceCollectionExtensions adds JSON and validation options the other two don't have
- 2026-10-27: Phase 1 (foundation and tracer bullet). Added `tinyshop/shared/TinyShop.ServiceDefaults` (class library, `FrameworkReference Microsoft.AspNetCore.App`) and `shared/README.md` (what may and may not go in). Moved the health endpoint mapping into it (`MapDefaultHealthEndpoints`) for product-, user- and order-service. Readiness convention changed from "checks tagged `ready`" to "every registered check" (liveness: only checks tagged `live`, none), as in Microsoft's ServiceDefaults template, so Infrastructure needs no tag and never references the web-host library; behaviour unchanged. Boundary check: services may reference their own folder and `shared/`, `shared/` only itself; shown failing on a service→service and a shared→service reference. Dockerfiles copy `shared/`. architecture.md layout and rules updated. Verified: 336 tests green, format, boundary check, ShellCheck v0.10.0; all 7 images rebuilt and the 57 snapshots identical to Phase 0
- 2026-10-28: Phase 2 (Problem Details). Moved into `TinyShop.ServiceDefaults/Errors`: the customizer, the generic codes (`ErrorCodes`), the type-URI rule (`ProblemType.For`/`CodeOf`) and an `ErrorCatalog` (titles and status-to-code map). **Design decision:** each service passes a registration delegate, `services.AddDefaultProblemDetails(ProductErrorCodes.Register)`, and its `<Service>ErrorCodes` class adds its codes with `catalog.Add(code, title, forStatus?)` (order-service maps 502/504 to `dependency_*`). Chosen over an `IErrorCodeProvider` in DI: the catalog is fixed at startup and needs no runtime lookup; adding an existing code or an already-mapped status throws at startup. Controllers keep the "pick a code by setting the type URI" style: `Problem(type: ProblemType.For(...))`. New `shared/tests/TinyShop.ServiceDefaults.UnitTests` (18 tests) replaces the three copies of `ProblemDetailsCustomizerTests`; each service keeps a small `<Service>ErrorCodesTests` for its own codes. `shared/TinyShop.Shared.slnx` added; CI `dotnet` matrix gains a `shared` entry (entries now carry their directory). Verified: all 5 solutions Release `-warnaserror` and format clean; tests shared 18, product 55, user 53, payment 33, order 179; boundary check, actionlint; all images rebuilt (shared DLL in each, no test code); the 57 snapshots identical to Phase 0. Found, not caused by this phase: payment-service's `ProcessPayment_WhenDeadlinePasses_ReturnsDeadlineExceeded` is flaky (about 1 run in 10 returns `Internal` instead of `DeadlineExceeded`); needs its own ticket
