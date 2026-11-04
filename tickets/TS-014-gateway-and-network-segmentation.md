---
key: TS-014
title: Gateway and network segmentation
type: Story
status: In Progress
priority: High
points: 8
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, dotnet, gateway, networking, docker, compose, ci]
depends_on: [TS-013, TS-024]
created: 2026-10-18
---

# TS-014: Gateway and network segmentation

## Summary
Build the `gateway`, the single public entry point for `/api/*`, as hand-written `HttpClient` forwarding; run it as a container on a new `edge-network`; then prove that the whole network design behaves exactly as the reachability matrix says. One ticket from code to a running, verified container, done in phases (below), with the assignee writing the forwarding code.

## Background
- **What a gateway does here:** it receives every browser request for `/api/*` (from nginx, TS-015), forwards it to the one internal service that owns it, and passes the answer back unchanged. It holds no data, validates nothing (contracts.md §2.4 rule 4) and makes only three errors of its own. That's what a *reverse proxy* does; a gateway is a reverse proxy that also decides routes.
- **Decision D4:** hand-written forwarding with `HttpClient`, not YARP, so every header and byte that passes through is code you wrote and can explain.
- **Contracts:** the five public routes and the gateway's own errors are in [contracts.md §4](../tinyshop/docs/contracts.md#4-public-api-gateway); how responses pass through, and the `Location` rewrite, in [§2.4](../tinyshop/docs/contracts.md#24-how-errors-pass-between-services); the default timeouts (D5: none of our own) in [§7](../tinyshop/docs/contracts.md#7-default-timeouts-in-m1).
- **Networks:** [architecture.md §2-§4](../tinyshop/docs/architecture.md#3-networks). The gateway joins `edge-network` (new, not internal) and `backend-network`, and publishes no port. The frontend arrives in TS-015, so until then a stand-in container on `edge-network` plays its part.
- **Shared code (D25, TS-024):** the gateway uses `TinyShop.ServiceDefaults` like every HTTP service (Problem Details, health endpoints, the pipeline), with its own error codes, and `TinyShop.Testing` in its tests.
- Settles architecture.md **Q5**: the gateway owns `/api/*`; nginx forwards the path unchanged.

## Scope
**In scope**
- **Application** (`tinyshop/services/gateway/`):
  - The five public routes of contracts.md §4, each forwarded to its internal route; anything else under `/api/` (and any internal path such as `/products/1` or another service's `/health/*`) answers the gateway's own 404 `not_found`, a wrong method 405 `method_not_allowed`
  - Forwarding: method, path, query string, request body and end-to-end headers in; status, end-to-end headers and body out, unchanged except `Location` (`/orders/1001` → `/api/orders/1001`)
  - Its own errors: 502 `upstream_unreachable`, 504 `upstream_timeout`, 500 `internal_error`
  - The upstream addresses from configuration (`Services__ProductService__BaseUrl`, ... as in order-service); `/health/live` and `/health/ready`
- **Container:** Dockerfile (same pattern as the other services, no migrations stage); the `gateway` service and `edge-network` in Compose; CI `image` entry; Dependabot docker entry
- **Network proof:** a script in `tinyshop/infrastructure/scripts/` that tests every cell of the reachability matrix, and a CI job that runs it against the real stack
- **Tests and CI:** unit and integration tests, CI `dotnet` entry
- **Docs:** architecture.md (§2 gateway row and the jobs table, which lacks `order-db-migrate`; §3 `edge-network` exists; Q5 resolved), contracts.md §4 (the gateway's 404/405 for unknown routes), epic D16 (the gateway's single project, decision 1 below)

**Out of scope**
- The frontend and nginx (TS-015)
- Timeouts, retries, circuit breakers of our own (D5; Failure Exercise 2)
- Adding `X-Forwarded-*` headers (decision 4), authentication, rate limiting, caching, TLS
- Tracing (M2), though question 5 looks at what already happens
- Exposing payment-service: it's gRPC on `payment-network`, which the gateway must never join

## Decisions (made in refinement, 2026-11-04)
| # | Decision | Why |
|---|---|---|
| 1 | **One project, `TinyShop.Gateway.Api`**, plus unit and integration test projects. Not the four clean-architecture layers (D16 gets a recorded exception). | The gateway has no domain, no use cases and no database: Domain and Application would be empty. The `.Api` name keeps the boundary check's layer rule (ServiceDefaults only from `*.Api`). |
| 2 | **Stream, don't buffer.** Send with `HttpCompletionOption.ResponseHeadersRead`; copy request and response bodies as streams. | Buffering holds every body in memory and delays the first byte. The cost: once the response has started, a failure can no longer become a Problem Details response (question 2). |
| 3 | **Headers:** copy end-to-end headers both ways; drop hop-by-hop headers (`Connection`, `Keep-Alive`, `Transfer-Encoding`, `TE`, `Trailer`, `Upgrade`, `Proxy-Authenticate`, `Proxy-Authorization`, and any header named in `Connection`); don't copy `Host` (`HttpClient` sets the upstream's). | Hop-by-hop headers describe one connection, not the message (AC2, RFC 9110 §7.6.1). Copying them breaks framing or keep-alive on the next hop. |
| 4 | **No `X-Forwarded-*` added** by the gateway; any that arrive are passed through like other end-to-end headers. | Nothing reads them yet. nginx (TS-015) is the natural place to set them; revisit there. |
| 5 | **Use `AddServiceDefaults`/`UseServiceDefaults`**, registering `upstream_unreachable` (`forStatus: 502`) and `upstream_timeout` (`forStatus: 504`). **Forwarded responses opt out of status-code pages** (`IStatusCodePagesFeature.Enabled = false`). | The shared pipeline already works without controllers (TS-024's pipeline test). But `UseStatusCodePages` gives a body to *empty* error responses: a forwarded empty 404 would get the gateway's body, which breaks "unchanged". |
| 6 | **504 only for our timeout.** `HttpClient.Timeout` expiring → 504 `upstream_timeout`; the client disconnecting (`HttpContext.RequestAborted`) → no response at all, not a 504. Connection refused or DNS failure → 502. | Both show up as cancellation; only one of them has someone waiting for an answer. |
| 7 | **Timeouts stay at the defaults** (`HttpClient.Timeout` 100 s, D5). Tests set a short timeout through configuration. | D5. contracts.md §7 already explains why the gateway's 504 can't reach a browser behind nginx's 60 s. |
| 8 | **`/health/ready` checks nothing upstream; no `depends_on` on the services** in Compose. | As order-service (TS-023): one unhealthy service must not make the entry point unready or stop it starting. |
| 9 | **Runtime image `aspnet:10.0-alpine`** (architecture.md §2 says `aspnet:10.0`: fix it), non-root, health check `*dotnet-healthcheck` (TS-025). | Same as every HTTP service: BusyBox `wget` for the health check. |
| 10 | **Integration tests against real upstream servers:** small in-process web apps on `127.0.0.1` random ports, not a stub `HttpMessageHandler`. A closed port for 502 (generalize `TinyShop.Testing`'s `UnreachableDatabase` into a closed-port helper), an endpoint that never answers plus a short timeout for 504. | Hop-by-hop headers, chunked bodies and streaming only exist on a real connection; a stub handler skips exactly what's being tested. |
| 11 | **Matrix script in bash**, runs `nicolaka/netshoot` (exact version tag, Q2) with `--network container:<container>`, so each test runs in that container's exact network position. Each target is tried by **name and by IP**. A stand-in container with the alias `frontend` on `edge-network` until TS-015. The browser row is tested from the host through published ports only. | `container:` reuses the real container's network stack instead of approximating it. A name that doesn't resolve is weaker evidence than an unreachable IP: ✗ must mean "no route". |
| 12 | **A new CI job `network`:** build the images, `docker compose up --wait` with `.env.example`, run the script, `down -v`; added to `ci-passed`. | The proof only counts if it keeps passing. TS-018 later adds the end-to-end smoke test to the same kind of job. |

## Phases
Each phase ends green (build, tests, CI) and with a review before the next. You write the forwarding code (phases 2-4); Claude explains first, then reviews.
0. **Understand:** how a reverse proxy works, hop-by-hop headers, streaming. Claude explains with a small runnable demo, no gateway code yet.
1. **Skeleton (tracer bullet):** the solution, `AddServiceDefaults` with the gateway's error codes, health endpoints, the `dotnet` CI entry and the boundary check passing. No routes yet.
2. **One route end to end:** `GET /api/products`, streamed, with its integration test against a real upstream.
3. **All five routes:** method, path, query and body forwarding, the header rules, the `Location` rewrite, the gateway's own 404/405.
4. **When upstream fails:** 502, 504 (and not on client disconnect), 500; pass-through of upstream errors and empty error bodies untouched.
5. **Container:** Dockerfile, Compose (`gateway`, `edge-network`), CI `image` entry, Dependabot; call `/api/*` from a container on `edge-network`.
6. **Network proof:** the matrix script, then the CI `network` job; fix the Compose file or the document wherever they disagree. Docs.

## Acceptance criteria
- [ ] AC1: Every public route forwards the method, path, query string, body and end-to-end headers, and passes the status, headers and body back unchanged, except `Location`, rewritten from the internal to the public path.
- [ ] AC2: Hop-by-hop headers are dropped in both directions, and `Host` isn't copied. Your notes explain what hop-by-hop headers are and why a proxy mustn't copy them.
- [ ] AC3: The gateway's own errors: a stopped upstream gives 502 `upstream_unreachable`; an upstream that doesn't answer within the timeout gives 504 `upstream_timeout`; a client that disconnects gets no 504; an unexpected failure gives 500 `internal_error`. All are Problem Details in the contracts.md §2.1 shape.
- [ ] AC4: Upstream responses pass through untouched, errors included: for example order-service's 422 `user_not_found` and 503 `service_unavailable` reach the caller with their own body and `traceId`, and an empty-bodied upstream error isn't given a gateway body.
- [ ] AC5: Only the five public routes exist: other `/api/` paths and internal paths (`/products/1`, `/health/ready` of a service) get the gateway's 404 `not_found`; a wrong method gets 405 `method_not_allowed`.
- [ ] AC6: The gateway runs in Compose: the image builds (alpine, non-root), the container is healthy, it publishes no port, it's on `edge-network` and `backend-network` only, and it doesn't wait for the services to start. `edge-network` exists and isn't internal.
- [ ] AC7: **The reachability matrix is proven:** the script tests every ✓, ◐ and ✗ cell of architecture.md §4, by name and by IP, and its output matches the document exactly. Any difference is a bug in the Compose file or the document, and whichever is wrong is fixed.
- [ ] AC8: Unit tests cover the header rules, the `Location` rewrite and the route table; integration tests cover forwarding, pass-through and the upstream errors against real upstream servers. CI builds and tests the gateway, builds its image, and runs the matrix script against the real stack.
- [ ] AC9: Docs updated: architecture.md §2 and §3 and Q5, contracts.md §4 (unknown routes), epic D16 (the gateway's single project).

## Technical notes
These are pointers, not the solution.
- **Typed clients:** one `HttpClient` per upstream through `IHttpClientFactory`, `BaseAddress` from configuration, as order-service does for user- and product-service.
- **Building the upstream request:** headers belong in two places in .NET. `HttpRequestMessage.Headers` holds request headers; content headers (`Content-Type`, `Content-Length`, ...) go on `HttpRequestMessage.Content`. The same split applies to `HttpResponseMessage.Headers` and `.Content.Headers` on the way back. A header put in the wrong one throws.
- **Request body:** wrap `HttpContext.Request.Body` in a `StreamContent`; only when the request has a body.
- **Response:** copy the status and headers, then the body stream (`CopyToAsync` into `HttpContext.Response.Body`). Let ASP.NET Core frame the response itself (`Content-Length` or chunked); don't copy `Transfer-Encoding`.
- **Timeout versus disconnect:** an expired `HttpClient.Timeout` throws `TaskCanceledException` whose `InnerException` is a `TimeoutException`; a cancelled `RequestAborted` throws a cancellation without it. Check which one before choosing 504.
- **Route table:** five entries, public route → upstream and internal path. Minimal API endpoints (`MapGet`, `MapPost`) are enough; no controllers needed. Check what the shared API versioning does to an `?api-version=` query on a route without version metadata: it must reach the upstream unchanged.
- **Browser row on Linux:** a Linux host (the CI runner) has routes to non-internal bridge networks, so it *can* reach the gateway's IP directly; Docker Desktop on Windows can't. The matrix's browser row means "through published ports", which is what a browser on another machine has. Question 3.
- **The matrix:** ◐ cells are reachable (shared network) but unused; the script checks reachability, so ◐ and ✓ both expect a connection. Ports: 80 for the frontend stand-in, 8080 for the services, 5432 for the databases.

## Learning objectives
- What a reverse proxy does to a request and a response, header by header
- Hop-by-hop versus end-to-end headers; message framing (`Content-Length`, chunked)
- Streaming versus buffering, and the point of no return once a response has started
- Telling failures apart: refused connection, timeout, client gone
- How Docker networks isolate containers, and how to prove it rather than assume it

## Questions to answer
Write the answers in `notes/`.
1. What are hop-by-hop headers? Pick two and describe what breaks if a proxy copies them.
2. The gateway streams responses. An upstream fails halfway through sending a body: what does the client receive, and why can't the gateway send a 502 at that point?
3. On the Linux CI runner, the host can reach the gateway's container IP directly, although the matrix says browser → gateway ✗. Is that a hole? What would a browser on another machine be able to reach, and why?
4. Why must the gateway's readiness not depend on the services, and its container not wait for them? Describe the failure you'd get otherwise.
5. Trigger a gateway 502 and a pass-through 404 from order-service, and compare their `traceId`s with the IDs in the services' logs. What do you notice, and which part of .NET causes it? (A preview of M2.)
6. contracts.md §2.4 says the gateway doesn't validate request bodies. Why is that the right place for the rule?

## Definition of done
- [ ] All acceptance criteria met
- [ ] Questions answered in `notes/`
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
- 2026-10-18: Updated for the `tinyshop/infrastructure/` layout
- 2026-10-18: Paths updated for the per-service layout (`tinyshop/services/<service>/{src,tests}`)
- 2026-11-04: Refined. Kept as one ticket, containerization and the network proof included (not split like TS-011/021, TS-012/022, TS-013/023), because the assignee implements the gateway hands-on. Points 5 → 8. Decisions 1-12, phases 0-6, AC3-AC9 and questions added; `needs-refinement` removed
