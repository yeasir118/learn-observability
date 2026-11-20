---
key: TS-031
title: "TLS inside: nginx, the gateway and the HTTP services"
type: Story
status: To Do
priority: High
points: 5
assignee: yeasir118
reporter: claude
epic: TS-027
labels: [m1.5, tls, dotnet, nginx, compose, needs-refinement]
depends_on: [TS-030]
created: 2026-11-19
---

# TS-031: TLS inside: nginx, the gateway and the HTTP services

## Summary
Every HTTP hop behind nginx is encrypted and the server verified: nginx to the gateway, the gateway to product-, user- and order-service, order-service to user- and product-service. Kestrel serves TLS 1.3 on 8443 with each service's own certificate; every client trusts only the TinyShop root and checks the name. The first rows of the TLS matrix are proven, in CI.

## Background
- Epic TS-027, decisions D29 (names under `tinyshop.internal`), D30 (narrow trust, never disabled), D31 (TLS 1.3 only inside), D33 (8443; 8080 retired), D38 (the TLS matrix).
- Open questions from the epic, decided here: host development (`dotnet run`) with or without TLS; nginx terminating and re-encrypting (rather than passing through).

## Scope
**In scope**
- Compose network aliases `<service>.tinyshop.internal`; the `Services__*__BaseUrl` settings become `https://<service>.tinyshop.internal:8443`
- Kestrel: an HTTPS endpoint on 8443 from a mounted certificate and key, TLS 1.3 only, no plaintext endpoint
- The services' `HttpClient`s: trust only the TinyShop root (a custom root trust, not the system store), hostname checked
- nginx to the gateway: `proxy_ssl_verify on`, the root as trusted certificate, the right server name (SNI)
- Health checks over HTTPS inside each container
- A TLS check script (the TLS matrix: positive and negative cases per hop), in the `system` CI job; the reachability matrix's ports updated
- architecture.md (ports, URLs), contracts.md (base URLs), `tls.md` (trust flows)

**Out of scope**
- gRPC (TS-032), Postgres (TS-033), client certificates (TS-034)

## Acceptance criteria (draft)
- [ ] AC1: Every HTTP hop behind nginx negotiates TLS 1.3 with a verified server certificate; nothing listens in plaintext on a shared network.
- [ ] AC2: A client given a certificate from another CA, or a wrong name, is refused (negative rows of the TLS matrix).
- [ ] AC3: The smoke test and both matrices pass in CI.

## Learning objectives
- TLS termination and re-encryption at a proxy
- Configuring TLS in Kestrel and trusting a private root in .NET without trusting it machine-wide
- SNI and hostname verification between services

## Activity log
- 2026-11-19: Created with epic TS-027
