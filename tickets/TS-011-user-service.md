---
key: TS-011
title: User service
type: Story
status: To Do
priority: High
points: 5
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, dotnet, ef-core, docker, needs-refinement]
depends_on: [TS-010]
created: 2026-10-18
---

# TS-011: User service

## Summary
As a shopper, I can enter my user ID and see my name, so that I know I'm ordering as the right person.

Technically: build `user-service`, `user-db` and `user-db-proxy` by following the pattern established for the product service in TS-007 to TS-010, with less guidance.

## Background
- Contract: [contracts.md §3 (User) and §5.2](../tinyshop/docs/contracts.md#52-user-service). Ports and networks: architecture.md §2, §3 and §8 (`localhost:5102` on the host, `127.0.0.1:5434` for the proxy).
- This is the "you do it" ticket. Repeating the pattern without step-by-step guidance shows what you've really absorbed, and what you'd want to share between services.

## Scope
**In scope**
- `user-service` (on the host first, then containerized), `user-db`, `user-db-proxy`, `user-network`
- Migrations with seed data (at least user 123 "Alice", plus four others)
- Integration tests, and CI for build, test and image
- Everything the product service has: Problem Details with `code`, health endpoints, non-root image, health check, startup order

**Out of scope**
- Extracting shared code into a library (but write down what you were tempted to copy, for the refinement discussion)

## Acceptance criteria
- [ ] AC1: `GET /users/{id}` matches contracts.md §5.2, including every error.
- [ ] AC2: `user-db` is reachable only by `user-service`. Show that `product-service` can't reach it.
- [ ] AC3: Integration tests cover the contract, and CI runs them. CI also builds the image.
- [ ] AC4: `docker compose up` on empty volumes brings up the product and user slices, and both become healthy.
- [ ] AC5: Your notes list every piece of code or configuration you copied from product-service, as input for deciding whether to share it.

## Technical notes
To be refined before pickup. Points to discuss: whether to share code (a `Directory.Build.props`, a common library for Problem Details and health checks), and whether CI should build images with a matrix strategy.

## Definition of done
- [ ] All acceptance criteria met
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
