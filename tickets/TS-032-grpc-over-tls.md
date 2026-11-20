---
key: TS-032
title: "gRPC over TLS: order-service to payment-service"
type: Story
status: To Do
priority: Medium
points: 3
assignee: yeasir118
reporter: claude
epic: TS-027
labels: [m1.5, tls, grpc, http2, needs-refinement]
depends_on: [TS-031]
created: 2026-11-19
---

# TS-032: gRPC over TLS: order-service to payment-service

## Summary
The payment call moves from HTTP/2 without TLS (h2c) to gRPC over TLS 1.3, where HTTP/2 is chosen during the handshake (ALPN) instead of being assumed. payment-service's port no longer has to be HTTP/2-only, and its health probe speaks TLS.

## Background
- Epic TS-027, decisions D30, D31, D33. architecture.md §2: "Payment's port 8080 speaks only HTTP/2", because without TLS a client can't negotiate the protocol. With TLS, it can.

## Scope
**In scope**
- payment-service: TLS on 8443, HTTP/1.1 and HTTP/2 negotiated by ALPN
- order-service's gRPC channel: `https://payment-service.tinyshop.internal:8443`, trusting only the TinyShop root
- `grpc_health_probe` with `-tls` and the root as CA
- TLS matrix rows for the hop; architecture.md §2 and §5.2, contracts.md §9 updated

## Acceptance criteria (draft)
- [ ] AC1: Orders are paid over gRPC with TLS 1.3 and a verified server; ALPN selects `h2` (shown with `openssl s_client -alpn h2`).
- [ ] AC2: h2c (plaintext HTTP/2) to payment-service is refused.

## Learning objectives
- ALPN: how a TLS handshake also negotiates the application protocol
- Why gRPC without TLS needs "prior knowledge" HTTP/2

## Activity log
- 2026-11-19: Created with epic TS-027
