---
key: TS-016
title: Fault injection switches
type: Story
status: To Do
priority: Medium
points: 3
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, chaos, configuration, needs-refinement]
depends_on: [TS-015]
created: 2026-10-18
---

# TS-016: Fault injection switches

## Summary
As the person running the lab exercises, I can turn specific failures on and off through environment variables, without rebuilding any image, so that each exercise can reproduce a realistic problem.

## Background
- Decision D11: all faults are off by default and controlled through environment variables.
- The faults the exercises need (outline §12, §17, §18, §21, §25):
  - product-service: a percentage of requests fail with 500
  - payment-service: an added delay
  - payment-service: a percentage of payments fail, both as declines and as technical errors
  - a delay on database calls in a chosen service
- Later, the blind incidents (outline §25) need faults that are intermittent or limited to particular inputs, such as one product ID or one user.

## Scope
**In scope**
- A configuration section (for example `Faults:*`) read by the affected services
- `.env.example` entries, all off by default
- A short `docs/faults.md` listing every switch, its effect and an example

## Acceptance criteria
- [ ] AC1: Every fault can be turned on with a `.env` change and `docker compose up -d`, with no image rebuild.
- [ ] AC2: With every switch off, behaviour is exactly the same as before this ticket. The CI tests pass unchanged.
- [ ] AC3: The database-delay fault happens **inside** the database call (for example `pg_sleep`), not around it in C#. Your notes explain why that will matter for tracing later.
- [ ] AC4: Faults can be limited to a product ID or a user ID.
- [ ] AC5: Every fault has a test showing it works when on and does nothing when off.

## Technical notes
To be refined before pickup. Points to discuss: middleware, an EF Core interceptor (`DbCommandInterceptor`), or a gRPC interceptor as the place to inject each fault; making randomness deterministic in tests; and whether to use `IOptionsMonitor` so switches can change without a restart.

## Definition of done
- [ ] All acceptance criteria met
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
