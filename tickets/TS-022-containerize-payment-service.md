---
key: TS-022
title: Containerize the payment service
type: Story
status: To Do
priority: High
points: 3
assignee: yeasir118
reporter: yeasir118
epic: TS-001
labels: [m1, docker, compose, grpc, http2, networking, ci, needs-refinement]
depends_on: [TS-012]
created: 2026-10-25
---

# TS-022: Containerize the payment service

## Summary
Run payment-service in Compose on `payment-network`, reachable only from order-service, with a container health check that works on an HTTP/2-only port.

## Background
- Split out of TS-012 at the assignee's request; TS-012 delivered the .NET solution.
- architecture.md §2 and §3: `payment-service:8080`, HTTP/2 only (h2c), on `payment-network` only.
- **Q1 (architecture.md §9):** the service implements `grpc.health.v1`. Its port refuses HTTP/1.1 (TS-012 verified a 400), so the BusyBox `wget` check used elsewhere can't work.

## Scope
**In scope**
- `services/payment-service/Dockerfile` (Alpine runtime; no migrations stage: no database). Build context `tinyshop/` must include `proto/` (it does; see `.dockerignore`)
- Compose: `payment-service` on `payment-network` (internal), `Production`, a container health check
- CI: payment-service in the `image` matrix (no migrations); Dependabot `docker` directory

## Acceptance criteria
- [ ] AC1: Q1 is decided and recorded in architecture.md, and `docker compose ps` shows payment-service `healthy`.
- [ ] AC2: From a throwaway container on `payment-network`, `grpcurl -plaintext payment-service:8080` can call `ProcessPayment` (with reflection on, `list` shows the service; otherwise pass the proto with `-import-path`/`-proto`).
- [ ] AC3: The same call from a container on `backend-network` fails: only order-service will share `payment-network`.
- [ ] AC4: **Experiment:** capture the gRPC traffic with `tcpdump` in a netshoot container sharing payment-service's network namespace (`--network container:<name>`). Identify the HTTP/2 connection preface (`PRI * HTTP/2.0`) and the frames (SETTINGS, HEADERS, DATA). Keep the capture for comparison with TLS in M1.5.
- [ ] AC5: CI builds the image; `ci-passed` covers it.

## Technical notes
To be refined before pickup. Options for AC1:
- **`grpc_health_probe`** (a small static binary) copied into the image, checking `grpc.health.v1`: the standard tool, and the same protocol Kubernetes' native gRPC probes use.
- **A second, HTTP/1.1-only port** (for example 8081) for `/health/*`, checked with `wget`: no extra binary, but an extra port, and it doesn't exercise the gRPC stack.

Whether reflection should be on inside Compose (it describes the whole API to anyone who can connect) is part of the same discussion.

## Definition of done
- [ ] All acceptance criteria met
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-25: Created, split out of TS-012
