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

## Child tickets
| Step | Key | Title |
|---|---|---|
| 0 | TS-002 | Repository foundation |
| 0 | TS-003 | System architecture document |
| 0 | TS-004 | HTTP API contracts |
| 0 | TS-005 | Payment gRPC contract |
| 1 | not yet created | Product service running on the host, with Postgres in Docker (EF Core) |
| 2 | not yet created | Containerize the Product service, move it into Compose |
| 3 | not yet created | User service |
| 4 | not yet created | Payment service (gRPC) |
| 5 | not yet created | Order service and the order flow |
| 6 | not yet created | Gateway and network segmentation |
| 7 | not yet created | Frontend container and nginx reverse proxy |
| 8 | not yet created | Fault switches and load generator |
| 9 | not yet created | Fresh-clone verification, tag `phase-1-baseline` |
| – | not yet created | Failure Exercise 1 write-up |

Tickets for steps 1 onwards are created once the step-0 contracts have been reviewed, so they can refer to the agreed contracts.

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
