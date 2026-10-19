---
key: TS-014
title: Gateway and network segmentation
type: Story
status: To Do
priority: High
points: 5
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, dotnet, gateway, networking, needs-refinement]
depends_on: [TS-013]
created: 2026-10-18
---

# TS-014: Gateway and network segmentation

## Summary
Build the `gateway`, the single public entry point for `/api/*`, as hand-written `HttpClient` forwarding. Then prove that the complete network design behaves exactly as the reachability matrix says.

## Background
- Decision D4: hand-written forwarding, not YARP.
- Public routes and how the gateway passes responses through: [contracts.md §2.4 and §4](../tinyshop/docs/contracts.md#4-public-api-gateway).
- Networks: `edge-network` and `backend-network`. The gateway publishes no ports. The frontend arrives in TS-015, so until then test from containers on `edge-network`.
- Settles architecture.md Q5: the gateway owns `/api/*`.

## Scope
**In scope**
- `tinyshop/services/gateway/`: forwarding for the five public routes, `Location` rewriting, and its own `upstream_unreachable` / `upstream_timeout` / `internal_error` responses
- `edge-network` in Compose
- A script that checks the reachability matrix
- Tests, and CI

## Acceptance criteria
- [ ] AC1: Every public route forwards the method, path, query string, body and the relevant headers, and passes the status, headers and body back unchanged, except for `Location`.
- [ ] AC2: Hop-by-hop headers (`Connection`, `Transfer-Encoding`, and so on) are handled correctly. Your notes explain what they are and why a proxy mustn't copy them.
- [ ] AC3: When a downstream service is stopped, the gateway returns 502 `upstream_unreachable`.
- [ ] AC4: **The reachability matrix is proven:** a script in `tinyshop/infrastructure/scripts/` (PowerShell or bash) runs netshoot containers on each network and tests every ✓, ◐ and ✗ cell of architecture.md §4. Its output matches the document exactly. Any difference is a bug in either the Compose file or the document, so fix whichever is wrong.
- [ ] AC5: Tests cover forwarding, `Location` rewriting and the upstream errors. CI builds, tests and builds the image.

## Technical notes
To be refined before pickup. Points to discuss: streaming request and response bodies versus buffering them, `HttpCompletionOption.ResponseHeadersRead`, which headers to forward, and how to run the matrix script in CI.

## Definition of done
- [ ] All acceptance criteria met
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
- 2026-10-18: Updated for the `tinyshop/infrastructure/` layout
- 2026-10-18: Paths updated for the per-service layout (`tinyshop/services/<service>/{src,tests}`)
