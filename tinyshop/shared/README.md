# tinyshop/shared

Technical plumbing shared by the .NET services (decision D25, ticket TS-024). The pattern is the
*microservice chassis*: one place for cross-cutting concerns, so every service handles them the
same way.

| Project | What it holds | Referenced by |
|---|---|---|
| `TinyShop.ServiceDefaults` | ASP.NET Core plumbing: controllers and their JSON conventions, Problem Details (the generic error codes, the customizer, the error catalog), API versioning, Swagger, health endpoints, and the request pipeline. More is added in TS-024's later phases | Each service's **Api** project only |
| `tests/TinyShop.ServiceDefaults.UnitTests` | Tests for the shared code, run once here instead of once per service | Nothing (CI job `dotnet (shared)`) |

`TinyShop.Shared.slnx` builds and tests everything in this folder. Each service's solution also
lists the shared projects it references, so the IDE and `dotnet format` cover them there too.

## How a service uses it

Two calls: one registers the services, one builds the pipeline. Everything else a service passes
in is what makes it different from the others.

```csharp
// Api/Extensions/ServiceCollectionExtensions.cs
services.AddServiceDefaults(new ServiceDefaultsOptions
{
    ApiTitle = "TinyShop Product Service",                 // OpenAPI document
    ApiDescription = "Internal API for the product catalogue. ...",
    AddErrorCodes = ProductErrorCodes.Register,            // this service's own error codes
});
services.AddExceptionHandler<DataStoreUnavailableExceptionHandler>();   // service-specific additions

// Program.cs
app.UseServiceDefaults();   // exception handler, status-code pages, Swagger (Development), controllers, health
```

The JSON conventions from contracts.md §1 and §2.1 (camelCase validation keys, a generic message for
malformed JSON, UTC timestamps with a `Z` and milliseconds) are part of the defaults, so they apply
to every service, including one that has no request bodies or timestamps yet.

A service can still add to the shared setup afterwards, for something that really is its own:
calling `services.AddControllers(...)` again returns the same MVC builder (MVC is only registered once).

## Error codes: what's shared and what each service adds

The codes every service returns (`validation_failed`, `not_found`, `service_unavailable`, ...) and
the rules for building a Problem Details response are shared. Codes only one service returns stay in
that service, in `Api/Errors/<Service>ErrorCodes.cs`, which adds them at startup through
`AddErrorCodes` (above):

```csharp
// ProductErrorCodes.Register:
catalog.Add(ProductNotFound, "Product not found");
// A service can also claim a status nobody else maps (order-service: 502 is dependency_failed):
catalog.Add(DependencyFailed, "Dependency failed", forStatus: StatusCodes.Status502BadGateway);
```

A controller picks a code by setting its type URI: `Problem(type: ProblemType.For(code))`.
Adding a code that already exists, or mapping a status that's already mapped, throws at startup.

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
