# tinyshop/shared

Technical plumbing shared by the .NET services (decision D25, ticket TS-024). The pattern is the
*microservice chassis*: one place for cross-cutting concerns, so every service handles them the
same way.

| Project | What it holds | Referenced by |
|---|---|---|
| `TinyShop.ServiceDefaults` | ASP.NET Core plumbing: health endpoints (more is added in TS-024's later phases) | Each service's **Api** project only |

## What may go in

Code that is the same in every service and says nothing about the business:
- HTTP conventions (error responses, versioning, Swagger, health endpoints)
- Data-access plumbing (translating database failures)
- Telemetry setup (M2)
- Test support (assertions, test-database fixtures)

## What must never go in

- **Domain types, DTOs, request or response models.** Sharing them couples the services'
  contracts: one service changing a type would silently change another's API.
- **Service-specific error codes** (`user_not_found`, `payment_timeout`). Each service registers
  its own.
- **`DbContext`s, migrations, repositories.** Each service owns its database (D10).
- **Clients for other services.** The caller owns its client, or generates it from the contract
  (`proto/`), as order-service does for payment-service.

If you're unsure, the test is: *would a change to this code mean a change to some service's
behaviour or contract?* If yes, it belongs in that service.

## Rules the build enforces

`infrastructure/scripts/check-service-boundaries.sh` (CI job `service-boundaries`):
- A service's projects may reference projects in their own service folder and in `shared/`.
- They may never reference another service's projects.
- A `shared/` project may never reference a service: shared code can't depend on any one service.

## Consequences of sharing by project reference

A change here changes every service at once. CI builds and tests every service on every push, so
a change that breaks one service fails the pull request. The alternative, an internal NuGet
package with versions each service picks up separately, is discussed in TS-024 (question 3).
