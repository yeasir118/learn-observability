---
key: TS-013
title: Order service and the order flow
type: Story
status: To Do
priority: High
points: 8
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, dotnet, ef-core, grpc, distributed-systems, needs-refinement]
depends_on: [TS-011, TS-012]
created: 2026-10-18
---

# TS-013: Order service and the order flow

## Summary
As a shopper, I can place an order for a product and quantity, and later look it up, so that I can buy things and check what happened.

Technically: build `order-service`, `order-db` and `order-db-proxy`. Implement the order flow across user-service and product-service (HTTP) and payment-service (gRPC), including the full dependency-failure translation table.

## Background
- Contract: [contracts.md §3 (Order), §5.3, §6 (translation table), §7 (timeouts), §8 (known gaps)](../tinyshop/docs/contracts.md#53-order-service).
- The flow is drawn in [architecture.md §6](../tinyshop/docs/architecture.md#6-request-flow-post-apiorders). order-service is on three networks: `backend-network`, `order-network` and `payment-network`.
- **This is the heart of M1's distributed-systems learning.** Every row in the translation table is a separate way a distributed call can go wrong.

## Scope
**In scope**
- `POST /orders`, `GET /orders/{id}`, `GET /orders?userId=`, and the health endpoints
- Typed HTTP clients via `IHttpClientFactory`, and a gRPC client via the gRPC client factory, with addresses from configuration
- Every row of contracts.md §6
- Tests, Compose services, and CI

**Out of scope**
- Retries, timeouts, idempotency and reconciliation (decision D5; these are documented as known gaps)

## Acceptance criteria
- [ ] AC1: The happy path works end to end inside Compose: an order is created as `PENDING`, payment is approved, and the order becomes `PAID`, with a snapshot of the product name and price.
- [ ] AC2: Every row of contracts.md §6 is covered by a test. The tests use fake dependencies (for example stub HTTP handlers, or a fake gRPC server) so that each failure can be triggered on purpose.
- [ ] AC3: An unknown user or product gives 422. A dependency's 5xx or unreachable dependency gives 502. Payment unreachable gives 502 `payment_unavailable` with an `orderId`, and the order is `FAILED`.
- [ ] AC4: `GET /orders?userId=` returns the user's orders newest first, and an empty `items` list for unknown users.
- [ ] AC5: **Experiment:** stop `product-service` and create an order. Then stop `payment-service` and create one. Record what the client sees and what state each order is left in, and compare that with contracts.md.
- [ ] AC6: Your notes walk through each known gap in contracts.md §8 with a concrete scenario of how it would actually happen.

## Technical notes
To be refined before pickup. Points to discuss: typed clients against named clients, how to tell "couldn't connect" apart from "timeout" in .NET exceptions (`HttpRequestException` with `SocketException` inside, versus `TaskCanceledException` wrapping a `TimeoutException`), mapping `RpcException` status codes, and where the translation logic should live.

## Definition of done
- [ ] All acceptance criteria met
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
