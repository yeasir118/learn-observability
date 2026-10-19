---
key: TS-012
title: Payment service (gRPC)
type: Story
status: To Do
priority: High
points: 5
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, dotnet, grpc, http2, docker, networking, needs-refinement]
depends_on: [TS-010]
created: 2026-10-18
---

# TS-012: Payment service (gRPC)

## Summary
Implement `payment-service`: a gRPC server for `PaymentService.ProcessPayment`, generated from the proto in TS-005. It simulates processing (waits about 50 ms, then approves) and runs in Compose on `payment-network`.

## Background
- Contract: [payment.proto](../tinyshop/proto/tinyshop/payment/v1/payment.proto) and [contracts.md §9](../tinyshop/docs/contracts.md#9-payment-grpc-contract).
- Transport: HTTP/2 without TLS (h2c) on 8080, reachable only from order-service (architecture.md §2 and §3).
- This ticket resolves architecture.md open question Q1: how to health-check a service that only speaks HTTP/2.
- Faults (delays, errors) come later, in TS-016. This ticket builds the happy path only.

## Scope
**In scope**
- `tinyshop/services/payment-service/` (with its own `src/`, `tests/` and solution), with code generated from the proto by `Grpc.Tools` at build time (`GrpcServices="Server"`)
- Kestrel configured for HTTP/2 only
- gRPC server reflection, so the service can be explored with command-line tools
- Dockerfile, Compose service, health check, `payment-network`
- Tests, and CI for build, test and image

## Acceptance criteria
- [ ] AC1: The proto is referenced from `tinyshop/proto/`, not copied into the project. There's exactly one copy of the contract.
- [ ] AC2: `ProcessPayment` waits about 50 ms and returns `success = true` with a unique `transaction_id` such as `txn_<random>`.
- [ ] AC3: From a throwaway container on `payment-network`, `grpcurl -plaintext payment-service:8080 list` shows the service, and calling `ProcessPayment` returns a response.
- [ ] AC4: The same `grpcurl` command from a container on `backend-network` fails. That proves only order-service can reach the payment service.
- [ ] AC5: **Experiment:** capture the gRPC traffic with `tcpdump` in a netshoot container that shares payment-service's network namespace. Identify the HTTP/2 connection preface and the frames. Keep the capture, because it gets compared with a TLS capture in M1.5.
- [ ] AC6: Q1 is resolved and recorded in architecture.md, and the container's health check works.
- [ ] AC7: Tests cover an approved payment, through an in-process gRPC client. CI builds, tests and builds the image.

## Technical notes
To be refined before pickup. Points to discuss: options for the gRPC health check (`grpc.health.v1` with `grpc_health_probe`, or a second HTTP/1.1 port), `fullstorydev/grpcurl` against `buf curl`, and how to share a network namespace with `--network container:<name>`.

## Definition of done
- [ ] All acceptance criteria met
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
- 2026-10-18: Paths updated for the per-service layout (`tinyshop/services/<service>/{src,tests}`)
