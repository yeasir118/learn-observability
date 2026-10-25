---
key: TS-012
title: Payment service (gRPC)
type: Story
status: In Review
priority: High
points: 5
assignee: claude
reporter: claude
epic: TS-001
labels: [m1, dotnet, grpc, http2, testing]
depends_on: [TS-010]
created: 2026-10-18
---

# TS-012: Payment service (gRPC)

## Summary
Implement `payment-service`: a gRPC server for `PaymentService.ProcessPayment`, generated from the proto in TS-005. It simulates processing (waits about 50 ms, then approves).

## Background
- Contract: [payment.proto](../tinyshop/proto/tinyshop/payment/v1/payment.proto) and [contracts.md §9](../tinyshop/docs/contracts.md#9-payment-grpc-contract).
- Transport: HTTP/2 without TLS (h2c); local port `5104` (architecture.md §8).
- **Rescoped on 2026-10-25 at the assignee's request:** this ticket is the .NET solution only, implemented by Claude. The container work (Dockerfile, Compose, `payment-network`, the container health probe, the grpcurl and tcpdump experiments) moved to [TS-022](TS-022-containerize-payment-service.md).

## Scope
**In scope**
- `tinyshop/services/payment-service/`: `TinyShop.PaymentService.slnx`; projects Api, Application and Domain (no Infrastructure: no database or external system yet); unit and integration tests
- Code generated from the shared proto by `Grpc.Tools` at build time
- Kestrel HTTP/2 only; gRPC reflection (Development); the gRPC health protocol
- CI: payment-service in the `dotnet` job matrix

**Out of scope**
- Everything container-related (TS-022)
- Declines and failures (fault switches, TS-016)

## Acceptance criteria
- [x] AC1: The proto is referenced from `tinyshop/proto/`, not copied (`<Protobuf Include="../../../../proto/...">` with `ProtoRoot`). There's exactly one copy of the contract.
- [x] AC2: `ProcessPayment` waits about 50 ms (`Payment:ProcessingDelay`) and returns `success = true` with a unique `transaction_id` (`txn_` + 32 hex digits).
- [x] AC3: Invalid requests return `INVALID_ARGUMENT` naming every bad field; unexpected errors return `INTERNAL` without details; a passed deadline returns `DEADLINE_EXCEEDED` (not `CANCELLED`).
- [x] AC4: The gRPC health protocol reports `SERVING`; reflection lists the service in Development and is off elsewhere.
- [x] AC5: Tests cover an approved payment through an in-process gRPC client, plus the cases above. CI builds and tests the solution.

## Definition of done
- [x] All acceptance criteria met
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
- 2026-10-18: Paths updated for the per-service layout (`tinyshop/services/<service>/{src,tests}`)
- 2026-10-25: Rescoped to the .NET solution (container work moved to TS-022) and implemented by Claude. Found and fixed during testing: (1) the exception interceptor reported a passed deadline as CANCELLED, which would have made order-service mark timed-out orders FAILED instead of keeping them PENDING (contracts.md §6); (2) the gRPC health service answered UNKNOWN: it needs at least one registered check and, by default, answers from a cache filled seconds after startup. Three CS0118 name clashes along the way (namespace TinyShop.Payment from the generated code; a Grpc folder shadowing the Grpc library), resolved by naming: the domain type is Charge, the folder is Services. Verified: 18 unit and 15 integration tests, and a local run with grpcurl (list, describe, approve, INVALID_ARGUMENT, health SERVING) and curl (the port refuses HTTP/1.1 with 400, answers HTTP/2)
