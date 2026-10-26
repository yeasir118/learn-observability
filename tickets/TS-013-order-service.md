---
key: TS-013
title: Order service and the order flow
type: Story
status: In Review
priority: High
points: 8
assignee: claude
reporter: claude
epic: TS-001
labels: [m1, dotnet, ef-core, grpc, distributed-systems, testing]
depends_on: [TS-011, TS-012]
created: 2026-10-18
---

# TS-013: Order service and the order flow

## Summary
As a shopper, I can place an order for a product and quantity, and later look it up, so that I can buy things and check what happened.

Technically: build `order-service`. Implement the order flow across user-service and product-service (HTTP) and payment-service (gRPC), including the full dependency-failure translation table.

## Background
- Contract: [contracts.md §3 (Order), §5.3, §6 (translation table), §7 (timeouts), §8 (known gaps)](../tinyshop/docs/contracts.md#53-order-service).
- The flow is drawn in [architecture.md §6](../tinyshop/docs/architecture.md#6-request-flow-post-apiorders).
- **This is the heart of M1's distributed-systems learning.** Every row in the translation table is a separate way a distributed call can go wrong.
- **Rescoped on 2026-10-26 at the assignee's request:** this ticket is the .NET solution only, implemented by Claude. The container work (`order-db`, `order-db-migrate`, `order-db-proxy`, the Dockerfile, `order-network`, the Compose runs of the happy path and the AC5 experiment) moved to [TS-023](TS-023-containerize-order-service.md).

## Scope
**In scope**
- `tinyshop/services/order-service/`: `TinyShop.OrderService.slnx`; projects Api, Application, Domain and Infrastructure; unit and integration tests
- `POST /orders`, `GET /orders/{id}`, `GET /orders?userId=`, and the health endpoints
- Typed HTTP clients via `IHttpClientFactory`, and a gRPC client via the gRPC client factory (generated from the shared proto), with addresses from configuration
- Every row of contracts.md §6
- EF Core model and the `InitialCreate` migration (the app role writes: `APP_TABLE_PRIVILEGES=SELECT, INSERT, UPDATE`)
- CI: order-service in the `dotnet` job matrix

**Out of scope**
- Everything container-related (TS-023)
- Retries, timeouts, idempotency and reconciliation (decision D5; these are documented as known gaps)

## Acceptance criteria
- [x] AC1: The happy path works end to end against the real services: an order is created as `PENDING`, payment is approved, and the order becomes `PAID`, with a snapshot of the product name and price. (Verified locally; the Compose run is TS-023 AC1.)
- [x] AC2: Every row of contracts.md §6 is covered by a test. The tests use fake dependencies (for example stub HTTP handlers, or a fake gRPC server) so that each failure can be triggered on purpose.
- [x] AC3: An unknown user or product gives 422. A dependency's 5xx or unreachable dependency gives 502. Payment unreachable gives 502 `payment_unavailable` with an `orderId`, and the order is `FAILED`.
- [x] AC4: `GET /orders?userId=` returns the user's orders newest first, and an empty `items` list for unknown users.
- [x] AC5: **Experiment (local run):** stop `product-service` and create an order. Then stop `payment-service` and create one. Record what the client sees and what state each order is left in, and compare that with contracts.md. (Repeated inside Compose in TS-023.)
- [ ] AC6: Your notes walk through each known gap in contracts.md §8 with a concrete scenario of how it would actually happen.

## Technical notes
Decisions made during implementation (the refinement points from the original ticket):
- **Typed clients, not named ones:** `UserServiceClient : IUserDirectory` and `ProductServiceClient : IProductCatalog`, registered with `AddHttpClient<TInterface, TImplementation>`. The Application layer sees only the interfaces.
- **Where the translation lives:** in Infrastructure (`Clients/DependencyHttp.cs` for HTTP, `Clients/PaymentGateway.cs` for gRPC). Transport details stop there; the use case (`OrderPlacement`) sees values, `null`, or Application exceptions, and the Api maps those to status codes.
- **Couldn't connect vs. timeout in .NET:** `HttpRequestException` (its `HttpRequestError` says ConnectionError, NameResolutionError, ResponseEnded…) means no response; `OperationCanceledException` while *our* token isn't cancelled means `HttpClient.Timeout` fired. If our token is cancelled, our caller left: that's a cancellation, not a timeout.
- **gRPC:** `DEADLINE_EXCEEDED` → timeout (order stays `PENDING`); `CANCELLED` caused by our own caller → cancellation; any other status → `payment_unavailable` (order `FAILED`).
- **Duplication:** the files listed in [TS-011's duplication list](TS-011-user-service.md#what-was-copied-from-product-service-input-for-the-share-it-discussion) were copied a third time (with a generalised exception handler and a few new error codes). The shared-library question TS-011 raised wasn't decided before this ticket; it's now D25, implemented in [TS-024](TS-024-shared-service-defaults.md).
- Ambiguous responses, now written into contracts.md §6: a 404 is "not found" only with the expected `code`; a 200 with an invalid body is a 502; a failed status update after payment is a 503 **with** `orderId`.

## Definition of done
- [ ] All acceptance criteria met
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
- 2026-10-25: Noted that D21-D24 apply, and the app role's write privileges
- 2026-10-26: Rescoped to the .NET solution (container work moved to TS-023) and implemented by Claude. Order IDs are `GENERATED ALWAYS AS IDENTITY` starting at 1001; status stored as `PENDING`/`PAID`/`FAILED` text with check constraints that mirror the domain rules (quantity range, total, transaction ID exactly when `PAID`, failure reason exactly when `FAILED`). `Location` is relative (`/orders/1001`): `CreatedAtAction` would have leaked `order-service:8080`. Timestamps get a converter for the contract's `…123Z` format. Verified: 103 unit and 76 integration tests (Testcontainers Postgres with the real role script; a TCP proxy cut mid-request for the status-update-fails row). Local run against user-, product- and payment-service from Compose (separate project, temporary socat proxies on 127.0.0.1): happy path `PAID` with snapshot; AC5: product-service stopped → 502 `dependency_failed`, no order; payment-service stopped → 502 `payment_unavailable` with `orderId`, order `FAILED`. Both took 12–35 s because the temporary proxy accepted the connection and then waited on DNS for the stopped container; compare with TS-023's run, where order-service resolves the name itself
