---
key: TS-030
title: HTTPS at the edge
type: Story
status: To Do
priority: High
points: 5
assignee: yeasir118
reporter: claude
epic: TS-027
labels: [m1.5, tls, nginx, browser, needs-refinement]
depends_on: [TS-029]
created: 2026-11-19
---

# TS-030: HTTPS at the edge

## Summary
The shop is served over HTTPS: nginx terminates TLS with a certificate from the TinyShop PKI, plain HTTP only redirects, and your browser trusts the TinyShop root because you installed it on purpose (and know how to remove it). The load generator and the smoke test speak HTTPS too.

## Background
- Epic TS-027, decisions D30, D31 (Mozilla intermediate at the edge), D33 (ports), D38.
- Open questions from the epic, decided here: the host ports (443/80 or 8443/8080), and HSTS on `localhost`.

## Scope
**In scope**
- nginx: an HTTPS server (TLS 1.2 and 1.3, the Mozilla intermediate profile), HTTP/2, the HTTP port redirecting to HTTPS, HSTS (as decided), `server_tokens off`
- The frontend's certificate and key, mounted read-only; nginx's health check over HTTPS
- The TinyShop root in the Windows trust store, with an install and a removal runbook in `tls.md`; the risk explained, and why the name constraints (TS-029) limit it
- k6 over HTTPS: trusting the root (`SSL_CERT_FILE`), `BASE_URL` with `https://`; its `http_req_tls_handshaking` numbers compared with `phase-1-baseline`
- A scan of the edge with `testssl.sh`, findings recorded
- The `system` CI job, the reachability matrix's browser row, architecture.md and contracts.md updated

**Out of scope**
- TLS behind nginx (TS-031)

## Acceptance criteria (draft)
- [ ] AC1: The browser opens the shop over `https://` without a warning; `http://` redirects.
- [ ] AC2: Only TLS 1.2 and 1.3 are accepted at the edge (shown with `openssl s_client` and `testssl.sh`).
- [ ] AC3: The smoke test runs over HTTPS in CI and verifies the certificate.
- [ ] AC4: Installing and removing the root are documented and tested once.

## Learning objectives
- Server authentication on the web; what the browser checks, in which order
- Why the edge still allows TLS 1.2, and what HSTS does (and how it can bite)
- What installing a root certificate really means for your machine

## Activity log
- 2026-11-19: Created with epic TS-027
