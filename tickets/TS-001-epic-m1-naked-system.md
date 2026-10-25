---
key: TS-001
title: "M1: Naked TinyShop system"
type: Epic
status: In Progress
priority: Highest
points: null
assignee: yeasir118
reporter: claude
epic: null
labels: [m1, docker, networking, distributed-systems]
depends_on: []
created: 2026-10-18
---

# TS-001: M1, the naked TinyShop system

## Summary
Build the complete TinyShop system with **no observability tooling**: React frontend, gateway, Product, User, Order and Payment services, and three Postgres databases, all running with Docker Compose. Only basic, unstructured application logging is allowed.

M1 is the baseline that every later phase is compared against. It's also where most of the Docker, networking and distributed-systems learning happens.

## Background
- The project outline is at [resources/outline.md](../resources/outline.md), sections 1–12.
- The aim is not just a working app. You should be able to explain every container, port, network and failure mode in it.

## Decisions already made
These are final unless a new ticket reopens one.

| # | Decision | Notes |
|---|---|---|
| D1 | .NET 10 (LTS), ASP.NET Core, C# | |
| D2 | No .NET Aspire, and no OpenTelemetry zero-code agent later | Wire everything by hand so it can be understood |
| D3 | EF Core with Npgsql for data access | |
| D4 | Gateway forwarding is hand-written with `HttpClient` | Not YARP |
| D5 | No retries and no deadlines/timeouts beyond the library defaults in M1 | The defaults are documented in TS-004 and TS-005 |
| D6 | Errors use RFC 9457 Problem Details with a machine-readable `code` | See TS-004 |
| D7 | Status-code rules: 500 for my own bug, 502 when a dependency failed or was unreachable, 503 when I can't serve right now, 504 when a dependency timed out | See TS-004 |
| D8 | A payment decline returns `201` with order `status: FAILED`. Technical payment failures map to 502/504 | Provisional, revisited in Exercise 4 |
| D9 | One product per order, with a quantity. Orders store a snapshot of the product name and unit price | |
| D10 | Database per service: three Postgres containers, isolated from each other by Docker networks | |
| D11 | Fault injection is controlled by environment variables, and all faults are off by default | Its own ticket later |
| D12 | The frontend (React components) is written by Claude. Its Dockerfile and nginx config are written by the assignee | |
| D13 | TLS is out of scope for M1 and is planned as epic M1.5 | |
| D14 | Containers, ports and networks follow [architecture.md](../tinyshop/docs/architecture.md); HTTP and gRPC behaviour follows [contracts.md](../tinyshop/docs/contracts.md) | Step-0 output |
| D15 | One Compose file at `tinyshop/infrastructure/compose/compose.yaml`, extended ticket by ticket. Infrastructure files live under `tinyshop/infrastructure/`; each Dockerfile lives next to the code it builds ([layout](../tinyshop/docs/architecture.md#repository-layout)) | `compose.yaml` is the filename the Compose specification prefers. Keeps `tinyshop/` uncluttered |
| D16 | Each .NET service follows clean architecture: four projects (Api, Application, Domain, Infrastructure) with dependencies pointing inward. The Api uses controllers, API versioning (optional `api-version`, default 1.0) and Swagger (Development only). Each service is a self-contained folder `tinyshop/services/<service>/` with its own solution, `src/`, `tests/` and Dockerfile; no project may reference another service's projects (checked in CI). Shared at `tinyshop/`: build settings (`Directory.Build.props`), central package versions (`Directory.Packages.props`), a pinned SDK (`global.json`) and `.editorconfig` | Changed from minimal APIs on 2026-10-18, at the assignee's request |
| D17 | Tests: xUnit, `WebApplicationFactory` for in-process HTTP tests, Testcontainers for real Postgres in tests | Tests are written alongside each service |
| D18 | CI is GitHub Actions, introduced in TS-006 and extended by every ticket that adds something checkable (see [tickets/README.md](README.md#continuous-integration-policy)) | |
| D19 | Local development secrets use `dotnet user-secrets` (host) and `.env` (Compose). Neither is committed | |
| D20 | Pull requests are merged with a merge commit. Squash and rebase merging are disabled | Keeps every commit from a branch; the merge commit marks where each ticket landed |
| D21 | A service that owns a database applies migrations with an EF Core migration bundle, run by a one-off Compose job (`<service>-db-migrate`, built from the service Dockerfile's `migrations` target). The service starts only after the job exits 0 | Decided in TS-010; architecture.md §10 |
| D22 | .NET services run on `aspnet:10.0-alpine` and have a Compose health check on `/health/ready` using BusyBox `wget` | Decided in TS-010; architecture.md §10 |
| D23 | Images: official base images pinned to a major version, small third-party images to an exact version, Actions to a commit SHA; Dependabot watches all of them, plus NuGet | Decided in TS-010; architecture.md §2 |
| D24 | Every service database has three roles: the superuser (initialisation only), `<service>_migrator` (owns the schema, used by the migration job) and `<service>_app` (only the DML the service needs), created by the shared `infrastructure/postgres/initdb` script | Decided in TS-020; architecture.md §10, Database roles |

## Child tickets
| Step | Key | Title |
|---|---|---|
| 0 | TS-002 | Repository foundation |
| 0 | TS-003 | System architecture document |
| 0 | TS-004 | HTTP API contracts |
| 0 | TS-005 | Payment gRPC contract |
| – | TS-006 | CI pipeline foundation (GitHub Actions) |
| 1 | TS-007 | Product database and debug proxy in Compose |
| 1 | TS-008 | Product service on the host with EF Core |
| 1 | TS-009 | Product service integration tests |
| 2 | TS-010 | Containerize the product service |
| 3 | TS-011 | User service (.NET solution) |
| 3 | TS-021 | Containerize the user service |
| 4 | TS-012 | Payment service (gRPC) |
| 5 | TS-013 | Order service and the order flow |
| 6 | TS-014 | Gateway and network segmentation |
| 7 | TS-015 | Frontend container and nginx reverse proxy (React components written by Claude) |
| 8 | TS-016 | Fault injection switches |
| 8 | TS-017 | Load generator (k6) |
| 9 | TS-018 | End-to-end smoke test in CI and `phase-1-baseline` tag |
| – | TS-019 | Failure Exercise 1 write-up |

TS-011 onwards carry the `needs-refinement` label and are refined just before they're picked up.

## Acceptance criteria
- [ ] A fresh clone followed by `docker compose up` gives a working system with no manual steps.
- [ ] Shopping works through the UI: list products, look up a user, create an order, view an order.
- [ ] HTTP is used between the gateway and the services, and from Order to User and Product. gRPC is used from Order to Payment.
- [ ] Each of the User, Product and Order services owns its own Postgres database, and no service can reach another service's database over the network.
- [ ] Only the frontend publishes a port to the host by default.
- [ ] All services implement the contracts in `tinyshop/docs/contracts.md`.
- [ ] Faults can be turned on through environment variables without rebuilding any image.
- [ ] The known gaps are written down in `notes/`: duplicate orders on retry, orders stuck in `PENDING` after a payment timeout, and the absence of timeouts and retries.
- [ ] Failure Exercise 1 is written up.
- [ ] `master` is tagged `phase-1-baseline`.

## Activity log
- 2026-10-18: Created. Step-0 tickets TS-002 to TS-005 opened.
- 2026-10-18: Step-0 tickets done. Created TS-006 to TS-019 and added decisions D14 to D19
- 2026-10-18: D15 updated: infrastructure files moved under `tinyshop/infrastructure/`
- 2026-10-18: D16 changed from minimal APIs to clean architecture with controllers
- 2026-10-18: D16 updated: per-service folders and solutions instead of one root solution
- 2026-10-25: Added D21 to D23 from TS-010 (migrations, runtime image and health check, image pinning)
- 2026-10-25: Added TS-020 (least-privilege database roles) and decision D24
- 2026-10-25: TS-011 rescoped to the .NET solution; container work split into TS-021
