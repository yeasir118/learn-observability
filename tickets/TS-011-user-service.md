---
key: TS-011
title: User service
type: Story
status: Done
priority: High
points: 5
assignee: claude
reporter: claude
epic: TS-001
labels: [m1, dotnet, ef-core, testing]
depends_on: [TS-010]
created: 2026-10-18
---

# TS-011: User service

## Summary
As a shopper, I can enter my user ID and see my name, so that I know I'm ordering as the right person.

Technically: the `user-service` .NET solution, following the product-service pattern (clean architecture, controllers, versioning, Problem Details, health endpoints, EF Core with migrations, unit and integration tests). It runs locally against any PostgreSQL.

## Background
- Contract: [contracts.md §3 (User) and §5.2](../tinyshop/docs/contracts.md#52-user-service). Local port `5102` (architecture.md §8).
- **Rescoped on 2026-10-25 at the assignee's request:** this ticket is the .NET solution only, implemented by Claude. The container and infrastructure work (Dockerfile, Compose, `user-db`, migration job, proxy, network) moved to [TS-021](TS-021-containerize-user-service.md).

## Scope
**In scope**
- `tinyshop/services/user-service/`: `TinyShop.UserService.slnx`, the four `src/` projects and two test projects
- `InitialCreate` migration seeding users 123 Alice, 124 Bob, 125 Carol, 126 Dave, 127 Erin
- CI: user-service in the `dotnet` job matrix (build, format, unit and integration tests)

**Out of scope**
- Everything container- and infrastructure-related (TS-021)
- Extracting shared code into a library (see the duplication list below, input for that decision)

## Acceptance criteria
- [x] AC1: `GET /users/{id}` matches contracts.md §5.2, including every error: 200, 404 `user_not_found` (also for 0 and negative IDs), 400 `validation_failed` (non-numeric, too large), 503 `service_unavailable`, plus the framework errors (404 `not_found`, 405 with `Allow`, 400 `unsupported_api_version`).
- [x] AC2: Health endpoints follow contracts.md §5.4: `/health/live` ignores the database, `/health/ready` checks it.
- [x] AC3: Unit tests (24) and integration tests (37, real PostgreSQL via Testcontainers, the shared role script with `user_migrator`/`user_app`) pass, and CI runs them.
- [x] AC4: Works locally against a plain PostgreSQL: `dotnet ef database update --connection "<user_migrator>"` and `dotnet run` with a `user_app` connection string.
- [x] AC5: The duplication with product-service is listed below.

## What was copied from product-service (input for the "share it?" discussion)
| Copied | Lines (approx.) | Differs only in |
|---|---|---|
| `Api/Errors/` (ErrorCatalog, ProblemDetailsCustomizer, DataStoreUnavailableExceptionHandler) | 190 | one error code (`user_not_found` vs `product_not_found`) |
| `Api/Extensions/` (service registration, pipeline, versioning, Swagger, health endpoints) | 120 | nothing |
| `Api/OpenApi/ConfigureSwaggerOptions.cs` | 30 | title and description |
| `Application/Common/Exceptions/DataStoreUnavailableException.cs` | 15 | nothing |
| `Infrastructure/Persistence/DatabaseExceptionTranslator.cs` | 40 | one message |
| `Infrastructure/DependencyInjection.cs` | 45 | names (connection string, health check) |
| Integration-test support: factory, Postgres fixture, `ResponseAssertions`, unavailable-database fixture | 230 | names, database, role prefix |
| Role, migration, routing and health tests | 300 | table and path names |
| `.csproj` files, `appsettings*.json`, `launchSettings.json` | 120 | names, port |

About 1,000 lines are near-identical. Candidates for a shared library (for example `TinyShop.ServiceDefaults`): the Problem Details setup and error catalog base, the database exception translation, health-endpoint mapping, versioning and Swagger setup, and a test-support package. The service-boundary rule (no project references between services) would need an explicit exception for such a shared library, or the library published as a NuGet package. To be decided before order-service (TS-013), the third copy. **Outcome:** not decided in time (TS-013 made the third copy); decided on 2026-10-27 as D25 and implemented in [TS-024](TS-024-shared-service-defaults.md). Of this list, the first five rows and the integration-test support (assertions, Postgres fixture, unavailable-database helper) are now shared; `DependencyInjection.cs`, the test factories, the tests themselves and the project and settings files stay per service, because they differ in names and behaviour or test each service's own wiring.

**One difference from product-service:** user-service has a design-time DbContext factory (`Api/DesignTime/UserDbContextDesignTimeFactory`, reading `--connection`, then the environment variable, then the user secret) and a single `UseUserDatabase` options method. Without it, `dotnet ef ... --connection` failed unless the app's own connection string was also configured, because dotnet ef starts the app's host first. Product-service could get the same (it currently relies on the user secret being set, and on a placeholder in its Dockerfile).

## Definition of done
- [x] All acceptance criteria met
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
- 2026-10-25: Noted that D21-D24 apply
- 2026-10-25: Rescoped to the .NET solution (container work moved to TS-021) and implemented by Claude. Verified: build with warnings as errors, format check, 61 tests (24 unit, 37 integration), a mutation of the not-found code caught by 4 tests, and a run against a throwaway PostgreSQL with the shared role script: migrated as user_migrator, served as user_app, every contract case checked, 503 while the database was down and recovery afterwards
- 2026-10-25: Fixed two issues found by the assignee: the design-time factory ignored user secrets (moved to the Api project, which owns the secrets, and made it read them; precedence verified for all four sources), and the UserSecretsId had been copied from product-service, so both services shared one secrets store (user-service now has its own)
- 2026-10-25: Done, merged into master (PR #13)
