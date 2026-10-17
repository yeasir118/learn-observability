---
key: TS-004
title: HTTP API contracts
type: Task
status: To Do
priority: Highest
points: 3
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, docs, api-design, distributed-systems]
depends_on: [TS-003]
created: 2026-10-18
---

# TS-004: HTTP API contracts

## Summary
Specify every HTTP endpoint in TinyShop, both the public gateway API and the internal service APIs, in `tinyshop/docs/contracts.md`. This includes request and response bodies and every error case. Services are then built to match this document, not the other way round.

## Background
The error model and status-code rules below were decided by the tech lead (decisions D6–D9 in TS-001). Your job is to apply them to every endpoint and to decide the details that are still open (listed further down).

### Error model (decided)
- Every error response uses RFC 9457 Problem Details (`application/problem+json`).
- Every error response includes an extension field, `code`: a stable, machine-readable `snake_case` string. Clients branch on `code`, never on `title` or `detail`.
- Public error messages are generic. Never expose stack traces, internal hostnames or the names of internal services to the client.

### Status-code rules (decided)
| Code | Meaning |
|---|---|
| 400 | The request is malformed (missing fields, wrong types, `quantity <= 0`) |
| 404 | The resource named in the URL doesn't exist |
| 422 | The request is well-formed but refers to something that doesn't exist (an unknown user or product in `POST /orders`) |
| 500 | I broke (my own bug or an unhandled exception) |
| 502 | A dependency failed: it returned a 5xx, or I couldn't connect to it |
| 503 | I can't serve right now (for example, my own database is unreachable) |
| 504 | A dependency timed out |

### Rules for passing errors along (decided)
1. **The gateway passes responses through unchanged.** It forwards the downstream status, headers and body as they are. It only produces its own 502 or 504 when the service it forwards to can't be reached or times out.
2. **Services translate, never pass through.** When Order gets a 404 from User, it returns its own 422 (`user_not_found`). When Order gets a 500 from Product, it returns its own 502. A dependency's status code describes that dependency's resource, not yours.
3. **The gateway rewrites `Location`** from internal paths (`/orders/42`) to public paths (`/api/orders/42`).
4. **The service that owns the data validates requests.** The gateway doesn't validate request bodies.

### Order behaviour (decided)
- One product per order, with a quantity. The order stores a snapshot of the product name and unit price taken at creation time.
- Order status values are `PENDING`, `PAID` and `FAILED`.
- A payment decline returns `201` with `status: FAILED` and a failure reason. This is provisional and gets revisited in Exercise 4.
- A technical payment failure (the Payment service errored or couldn't be reached) marks the order `FAILED` and returns `502`.
- A payment timeout leaves the order `PENDING` and returns `504`, because the outcome is unknown. This is a known gap.
- Order 404s from User and Product are translated to 422.
- `GET /orders?userId=` doesn't check whether the user exists. An unknown user returns an empty list.

## Scope
**In scope**
- Public API: `GET /api/products`, `GET /api/users/{id}`, `POST /api/orders`, `GET /api/orders/{id}`, `GET /api/orders?userId={id}`
- Internal APIs:
  - Product: `GET /products`, `GET /products/{id}`
  - User: `GET /users/{id}`
  - Order: `POST /orders`, `GET /orders/{id}`, `GET /orders?userId={id}`
- `GET /health` on every HTTP service
- A table showing how Order translates each failure of its dependencies (User, Product, Payment)

**Out of scope**
- The Payment gRPC contract (that's TS-005, but it must be consistent with the payment rows of your translation table)
- Authentication, pagination, versioning

## Acceptance criteria
- [ ] AC1: Every endpoint listed above is documented with its method, path, path and query parameters, request body (with field names, types and whether they're required), success status and body, and a sample request/response.
- [ ] AC2: Every endpoint lists **all** of its error responses, each with status, `code` and when it happens. `POST /api/orders` must cover at least: invalid body, unknown user, unknown product, User failing, Product failing, Payment declined, Payment failing, Payment timing out, and the orders database being unavailable.
- [ ] AC3: A common section defines the Problem Details shape once, including the `code` field, with one full example.
- [ ] AC4: A **dependency-failure translation table** for the Order service has rows for (dependency × outcome) and columns for the resulting status, `code` and order status (`PENDING`, `FAILED`, or not created).
- [ ] AC5: A **defaults** section records the default timeouts in M1: the `HttpClient` default and the gRPC client default. Look them up; don't guess.
- [ ] AC6: A **known gaps** section covers at least duplicate orders on retry and orders stuck in `PENDING` after a timeout.
- [ ] AC7: Every open decision below is made and justified in one or two sentences.

## Open decisions for you
1. **JSON casing.** camelCase is recommended, since it's the System.Text.Json web default. Either way, be consistent.
2. **Money representation.** JSON number or string? What .NET type, and what Postgres column type? Think about floating-point rounding.
3. **Timestamps.** Format and time zone.
4. **IDs.** Integer or UUID, and why. Consider what the user types into the UI.
5. **Health response.** Just `200 OK`, or a body? What exactly is "healthy" checking (process alive or database reachable)?

## Technical notes
- RFC 9457 replaces RFC 7807. ASP.NET Core supports it natively; look up `AddProblemDetails`.
- ASP.NET Core automatically adds a `traceId` field to Problem Details. Note it now. It becomes important in later phases.
- When writing the translation table, ask of every row: "If the client retried this exact request, could it succeed?" If yes, it's 5xx. If no, it's 4xx.

## Learning objectives
- Designing a contract before writing code, and why contracts are the hardest thing to change in a distributed system.
- The difference between client faults and server faults, and between business failures and technical failures.
- Why services translate errors at their boundaries instead of passing them through.

## Questions to answer
1. Why is 404 the wrong status for `POST /api/orders` when the user doesn't exist, but the right status for `GET /api/users/{id}`?
2. The gateway passes statuses through unchanged, but Order translates them. Why the difference?
3. A client receives a `502` from `POST /api/orders`. From the response alone, can it tell which service failed? Should it be able to? (Keep your answer; Failure Exercise 1 comes back to it.)
4. Why does a payment timeout leave the order `PENDING`, while a connection failure to Payment marks it `FAILED`?
5. Why is a floating-point type (`double`) wrong for money?

## Definition of done
- [ ] All acceptance criteria met
- [ ] Questions answered in `notes/m1.md`
- [ ] Reviewed and approved
- [ ] Squash-merged into `master`, branch deleted, board updated

## Activity log
- 2026-10-18: Created
