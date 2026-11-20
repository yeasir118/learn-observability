---
key: TS-034
title: Mutual TLS and service identity
type: Story
status: To Do
priority: High
points: 8
assignee: yeasir118
reporter: claude
epic: TS-027
labels: [m1.5, mtls, spiffe, zero-trust, authorization, needs-refinement]
depends_on: [TS-032, TS-033]
created: 2026-11-19
---

# TS-034: Mutual TLS and service identity

## Summary
Clients prove who they are too. Every workload certificate carries a SPIFFE ID (`spiffe://tinyshop.internal/<service>`); every server requires a client certificate and accepts only the callers on its allow-list. A call that the network allows but the design doesn't, such as product-service calling user-service, is refused. The application roles also log in to Postgres with a client certificate.

## Background
- Epic TS-027, decisions D34 (SPIFFE IDs, allow-lists from the call graph), D35 (health checks aren't a back door), D36 (client certificates for the database roles), D38.
- The reachability matrix's ◐ cells ("reachable but unused") are exactly the calls this ticket refuses.

## Scope
**In scope**
- Leaf certificates with both `serverAuth` and `clientAuth`, and the SPIFFE ID as a URI SAN (TS-029's profiles extended)
- Kestrel: client certificates required, validated against the TinyShop root; an authorization rule per service that compares the caller's SPIFFE ID with its allow-list; a 403 (or a refused handshake) otherwise, decided in refinement
- Every client (`HttpClient`s, the gRPC channel, nginx with `proxy_ssl_certificate`) presents its own certificate
- The health checks: a mechanism that keeps mTLS intact (D35), decided here
- Postgres: `clientcert=verify-full` with the certificate's name mapped to the role, on top of the password (`pg_ident.conf` if needed)
- TLS matrix rows: no client certificate, a certificate from another CA, and a valid certificate with the wrong identity, each refused, for every hop
- contracts.md: who may call what (the allow-lists), and the new refusal

## Acceptance criteria (draft)
- [ ] AC1: Every service-to-service call and database connection authenticates the client by certificate.
- [ ] AC2: Every ◐ cell of the reachability matrix is refused by identity, shown by a test with a real certificate of the wrong service.
- [ ] AC3: The health checks work, and none of them opens a path around the allow-list.

## Learning objectives
- Client authentication in TLS; authentication versus authorization
- Workload identity: SPIFFE IDs, and what a service mesh automates
- Zero trust in practice: identity, not location

## Activity log
- 2026-11-19: Created with epic TS-027
