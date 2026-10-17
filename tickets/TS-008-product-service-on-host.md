---
key: TS-008
title: Product service on the host with EF Core
type: Story
status: To Do
priority: High
points: 5
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, dotnet, ef-core, api]
depends_on: [TS-007]
created: 2026-10-18
---

# TS-008: Product service on the host with EF Core

## Summary
As a shopper, I can list products and look one up by ID, so that I can choose what to buy.

Technically: create the .NET solution and `product-service` as an ASP.NET Core minimal API. It runs on the host with `dotnet run` and connects to `product-db` through the debug proxy, using EF Core for the schema, seed data and queries.

## Background
- The endpoints, response shapes and error codes are in [contracts.md §3 and §5.1](../tinyshop/docs/contracts.md#51-product-service). Health endpoints are in [§5.4](../tinyshop/docs/contracts.md#54-health-endpoints).
- Host ports and connection strings for local development are in [architecture.md §8](../tinyshop/docs/architecture.md#8-local-development): the service listens on `localhost:5101` and reaches the database at `localhost:5433`.
- Phase 1 rule: plain, unstructured console logging only. No structured logging, OpenTelemetry or correlation IDs yet.

## Scope
**In scope**
- `tinyshop/TinyShop.slnx` and `tinyshop/src/product-service/`
- EF Core model, migrations and seed data for products
- `GET /products`, `GET /products/{id}`, `GET /health/live`, `GET /health/ready`
- Problem Details error handling with `code`, as contracts.md specifies
- A CI job that builds the solution

**Out of scope**
- Tests (TS-009)
- Dockerfile and running in a container (TS-010)
- Fault injection (TS-016)

## Acceptance criteria
- [ ] AC1: The solution is created at `tinyshop/` and the project at `tinyshop/src/product-service/`, targeting .NET 10. The project builds with no warnings.
- [ ] AC2: `dotnet-ef` is installed as a **local tool** (`.config/dotnet-tools.json`), so `dotnet tool restore` gives anyone the same version.
- [ ] AC3: The `Product` entity matches contracts.md §3: `Id` is a `bigint` identity column, `Name` is at most 100 characters and required, and `PriceCents` is a `bigint` that must be greater than 0 (enforced by a database check constraint). Table and column names follow one convention, and your notes say which and why.
- [ ] AC4: An EF Core migration creates the schema, and seeds five products (Laptop, Keyboard, Mouse, Monitor, Headphones, or your own choice).
- [ ] AC5: You apply the migration with `dotnet ef database update` from the host, through the proxy. You did **not** do it by having the service migrate the database automatically at startup.
- [ ] AC6: The connection string is configured as `ConnectionStrings:ProductDb`. For local runs it comes from `dotnet user-secrets`. No password appears in any committed file.
- [ ] AC7: `dotnet run` listens on `http://localhost:5101` (set in `Properties/launchSettings.json`), and `GET /products` returns `{ "items": [...] }` in the order given by contracts.md.
- [ ] AC8: `GET /products/{id}` returns 200 with a product, 404 `product_not_found` for an ID that doesn't exist, and 400 `validation_failed` for `GET /products/abc`.
- [ ] AC9: **Every** error, including ones the framework generates, is Problem Details with a `code`. With the database stopped (`docker compose stop product-db`), `GET /products` returns 503 `service_unavailable`, not a 500 with a stack trace.
- [ ] AC10: `/health/live` returns 200 even when the database is stopped. `/health/ready` returns 503 when the database is stopped and 200 when it's running.
- [ ] AC11: JSON uses `camelCase` and includes `null` fields (contracts.md §1).
- [ ] AC12: **CI:** a new job runs `dotnet restore`, `dotnet build --no-restore` with warnings treated as errors, and `dotnet format --verify-no-changes` on the solution.

## Technical notes
- The .NET 10 SDK creates solutions in the new XML format (`.slnx`) by default.
- **Minimal APIs:** `MapGroup("/products")` keeps the endpoints together. Look at `TypedResults` for return types the compiler can check.
- **Problem Details:** start with `AddProblemDetails()` and `UseExceptionHandler()`, then use `CustomizeProblemDetails` to add `code`. Then work out how to map "the database is unreachable" (an `NpgsqlException`, often wrapped in another exception) to 503 rather than 500. An `IExceptionHandler` implementation is the idiomatic way to do that.
- **Route parameters and 400:** a route constraint (`{id:long}`) makes `/products/abc` a 404, because the route doesn't match. Without a constraint, binding fails, and in Production that's a 400. The contract asks for 400. Find out what the Development environment does differently.
- **Health checks:** `AddHealthChecks().AddDbContextCheck<...>()` comes from the `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` package. Tag your checks so that `/health/live` runs none of them and `/health/ready` runs the database check.
- **Seeding:** `HasData` puts seed rows inside a migration. Seeded identity values need explicit IDs; check what that does to the identity sequence for rows inserted later.
- **Npgsql naming:** the `EFCore.NamingConventions` package can give you `snake_case` names automatically. Writing them by hand is fine too.
- **Why not migrate at startup?** In production you'd usually run several instances of a service. If each one applied migrations when it started, they'd race each other. TS-010 decides how migrations run once the service is in a container.

## Learning objectives
- EF Core fundamentals: DbContext, entity configuration, migrations, seed data, the Npgsql provider.
- Configuration layering in ASP.NET Core: `appsettings.json`, then `appsettings.Development.json`, then user secrets, then environment variables.
- Implementing an API to a written contract, including every error path.
- The difference between liveness and readiness, in practice.

## Questions to answer
1. In what order does ASP.NET Core read configuration sources, and which one wins when two of them set the same key?
2. Where does `dotnet user-secrets` store the secrets, and why is that safer than a git-ignored `appsettings.Local.json`?
3. What does a migration contain, and what's the model snapshot file for?
4. Why must `/health/live` not check the database?
5. What did the framework return for an invalid ID or an unreachable database before you customised error handling? Which parts of that would have leaked internal details?

## Definition of done
- [ ] All acceptance criteria met
- [ ] Questions answered in `notes/m1.md`
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
