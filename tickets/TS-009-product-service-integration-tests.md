---
key: TS-009
title: Product service integration tests
type: Task
status: To Do
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
- **Why integration tests rather than mocks:** product-service is almost entirely configuration, database access and error mapping. Those are exactly the things a mocked unit test can't check. Testcontainers starts a real `postgres:18-alpine` container for each test run, so the tests check the real queries, migrations and error handling.
- This is also Docker practice: the tests talk to the Docker engine from code, both on your machine and on GitHub's runners.

## Scope
**In scope**
- `tinyshop/services/product-service/tests/TinyShop.ProductService.IntegrationTests/`, added to `TinyShop.ProductService.slnx`
- Tests for the contract in contracts.md §5.1 and §5.4
- A CI job that runs the tests

**Out of scope**
- Performance or load tests
- Tests for other services

## Acceptance criteria
- [ ] AC1: The test project is part of the solution and references `product-service`.
- [ ] AC2: One Postgres container is shared by every test in a run (an xUnit fixture), and its migrations are applied once. Each test starts from known data.
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
- [ ] AC7: **CI:** a job runs `dotnet test` on `ubuntu-latest` and passes. Required checks are updated so this job must pass before merging.

## Technical notes
- **`WebApplicationFactory<Program>`** hosts the app in memory. With top-level statements, `Program` is generated as an internal class, so you need a small `public partial class Program;` (or `InternalsVisibleTo`) so the test project can see it.
- **Overriding configuration in tests:** `WithWebHostBuilder` together with `ConfigureAppConfiguration`, or `UseSetting`, lets the tests replace `ConnectionStrings:ProductDb` with the container's connection string.
- **Testcontainers** (`Testcontainers.PostgreSql`) picks a random free host port for each container. Never hard-code ports in tests.
- **For AC4,** consider pointing the app at a closed port rather than stopping a shared container halfway through a run.
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
