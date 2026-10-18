---
key: TS-017
title: Load generator (k6)
type: Task
status: To Do
priority: Medium
points: 2
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, k6, load, compose, needs-refinement]
depends_on: [TS-015]
created: 2026-10-18
---

# TS-017: Load generator (k6)

## Summary
Add a k6 load generator as a Compose service behind a `load` profile. It produces realistic, steady traffic: browsing products, looking up users, creating orders and reading them back.

## Background
- Metrics and the later exercises are meaningless without traffic (outline §19–§21 and §25).
- k6 is open source (AGPL-3.0) and runs as a container.
- The generator should behave like a browser, so it enters through `frontend` on `edge-network`, not straight at the gateway.

## Scope
**In scope**
- `tinyshop/infrastructure/load/` with a k6 script and its settings, mounted into the stock k6 image at run time
- A Compose service `load-generator` under the `load` profile, with a pinned image

## Acceptance criteria
- [ ] AC1: `docker compose --profile load up` produces a steady mix of requests, with the rate and duration set by environment variables.
- [ ] AC2: The mix includes valid orders and some deliberately invalid ones (an unknown user or product), so 4xx traffic exists too.
- [ ] AC3: k6's end-of-test summary shows the request count and how long requests took.
- [ ] AC4: The script fails on unexpected responses (5xx while no faults are switched on), so it doubles as a smoke test.

## Technical notes
To be refined before pickup. Points to discuss: k6 executors (constant arrival rate versus virtual users), thresholds, and whether to target `http://frontend:80` or `host.docker.internal:8000`.

## Definition of done
- [ ] All acceptance criteria met
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
- 2026-10-18: Updated for the `tinyshop/infrastructure/` layout
