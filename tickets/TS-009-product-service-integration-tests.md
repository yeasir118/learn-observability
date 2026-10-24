---
key: TS-009
title: Product service integration tests
type: Task
status: In Review
priority: High
points: 3
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, dotnet, testing, testcontainers, ci]
depends_on: [TS-008]
created: 2026-10-18
---

# TS-009: Product service integration tests

## Summary
Add an integration test project for `product-service` that runs the real HTTP pipeline against a real Postgres in a throwaway container, and run those tests in CI.

## Background
- Decision D17: xUnit, `WebApplicationFactory`, Testcontainers.
- TS-008 already added `TinyShop.ProductService.UnitTests` (27 fast tests: domain rules, service, exception translation, Problem Details rules). This ticket adds the **integration** layer: the real HTTP pipeline, the real EF Core queries and **your real migration**, against a real Postgres.
- **Why integration tests rather than mocks:** product-service is almost entirely configuration, database access and error mapping. Those are exactly the things a mocked unit test can't check. Testcontainers starts a real `postgres:18-alpine` container for each test run, so the tests check the real queries, migrations and error handling.
- This is also Docker practice: the tests talk to the Docker engine from code, both on your machine and on GitHub's runners.

## Scope
**In scope**
- `tinyshop/services/product-service/tests/TinyShop.ProductService.IntegrationTests/`, added to `TinyShop.ProductService.slnx`
- Tests for the contract in contracts.md §5.1 and §5.4
- CI running the new tests (see AC7: no new job needed)
- A `ci-passed` gatekeeper job in CI, so the ruleset requires one check that never changes

**Out of scope**
- Performance or load tests
- Tests for other services

## Acceptance criteria
- [ ] AC1: The test project is part of the solution and references `product-service`.
- [ ] AC2: One Postgres container (`postgres:18-alpine`, the same image as Compose) is shared by every test in a run (an xUnit fixture). The fixture applies the service's **real EF Core migrations** once, so the tests also prove that `InitialCreate` works. Each test starts from known data.
- [ ] AC3: Tests cover at least:
  - `GET /products` returns 200 with the five seeded products, in order, as `{ "items": [...] }`
  - `GET /products/{id}` returns 200 for an existing product
  - returns 404 `product_not_found` for a missing one
  - returns 400 `validation_failed` for a non-numeric ID
  - every error response has content type `application/problem+json` and a `code`
  - `/health/ready` returns 200 while the database is up
- [ ] AC4: One test proves the 503 path: when the database is unreachable, `GET /products` returns 503 `service_unavailable` and `/health/live` still returns 200.
- [ ] AC5: Tests check the JSON text that's actually sent, including field names and casing. They must not just deserialize into the service's own C# types, which would pass even if the casing were wrong.
- [ ] AC6: `dotnet test` passes locally, using your Docker Desktop.
- [ ] AC7: **CI:** the existing `dotnet (product-service)` job runs the new tests with no workflow change, because `dotnet test --solution TinyShop.ProductService.slnx` runs every test project in the solution. Check the job log to confirm that the integration tests ran, and how long they took.
- [ ] AC8: **CI gatekeeper:** a `ci-passed` job depends on every other job, and the `master` ruleset requires only `ci-passed` (see the [CI policy](README.md#continuous-integration-policy)). Prove it: a pull request with a failing unit test shows `ci-passed` red and can't be merged.

## Technical notes
- **`WebApplicationFactory<Program>`** (package `Microsoft.AspNetCore.Mvc.Testing`) hosts the app in memory. With top-level statements, `Program` is generated as an internal class. The Api project already has `InternalsVisibleTo` for the unit tests; add one for the integration tests, or add `public partial class Program;` at the end of `Program.cs`. Know why either works.
- **Central package management:** new packages (`Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`) need a `PackageVersion` in `tinyshop/Directory.Packages.props`. The `.csproj` references them without a version.
- **Overriding configuration in tests:** the tests must replace `ConnectionStrings:ProductDb` with the container's connection string. Careful: `AddInfrastructure` reads the connection string **while `Program.cs` runs** (it fails fast if it's missing), not later. Overrides that are only applied when the host is built can arrive too late. Try `UseSetting(...)` and `ConfigureAppConfiguration(...)` inside `ConfigureWebHost`, observe which one the app actually sees, and write down why.
- **Testcontainers** (`Testcontainers.PostgreSql`) picks a random free host port for each container. Never hard-code ports in tests.
- **For AC4,** use a second factory whose connection string points at a closed port, rather than stopping the shared container halfway through a run. Use `127.0.0.1`, not `localhost`: on Windows, `localhost` tries IPv6 first, and each refused attempt costs about 2 seconds (seen during TS-008 testing).
- **xUnit v3 fixtures:** `IClassFixture<T>` shares one instance per test class, `ICollectionFixture<T>` per named collection, and v3's `AssemblyFixture` per test assembly. Fixtures implementing `IAsyncLifetime` can start and stop the container asynchronously. Choose the narrowest one that gives one container per run.
- **Applying migrations in the fixture:** create a `ProductDbContext` from the factory's services and call `Database.MigrateAsync()`. The tests only read data, so the seeded rows are the known data. If later tests write data, they'll need a reset between tests (for example with the Respawn library).
- GitHub's Ubuntu runners have Docker installed, so Testcontainers works there with no extra setup.

## Learning objectives
- What integration tests check that unit tests can't, and the cost of each.
- Test fixtures, test isolation, and why tests must not depend on the order they run in.
- Using Docker from code, and the same tests passing on your machine and in CI.

## Questions to answer
1. Why does Testcontainers map the container to a random host port instead of 5432?
2. What would AC5 catch that deserializing into the service's own types would miss?
3. How long does the test run take, and how much of that is starting the container? How would you make it faster if you had to?

## Definition of done
- [ ] All acceptance criteria met
- [ ] Questions answered in `notes/m1.md`
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
- 2026-10-18: Paths updated for the per-service layout (`tinyshop/services/<service>/{src,tests}`)
- 2026-10-19: Refined after TS-008: unit tests already exist, real migrations in the fixture, configuration-override pitfall, xUnit v3 fixtures, CI needs no new job, added AC8 (ci-passed gatekeeper)
- 2026-10-19: In progress. Integration test project implemented (25 tests); ConfigureAppConfiguration confirmed too late for AddInfrastructure, UseSetting used
- 2026-10-25: Branch also carries the start of TS-010 (Dockerfile, .dockerignore, .env.example for manual runs) and, per the CI policy, an `image (product-service)` CI job that builds it
