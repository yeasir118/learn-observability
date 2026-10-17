---
key: TS-018
title: End-to-end smoke test in CI and phase-1-baseline tag
type: Task
status: To Do
priority: High
points: 3
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, ci, docker, compose, needs-refinement]
depends_on: [TS-016, TS-017]
created: 2026-10-18
---

# TS-018: End-to-end smoke test in CI and phase-1-baseline tag

## Summary
Prove that the complete system starts from a fresh clone with one command, both on your machine and in CI. Then tag the result `phase-1-baseline`, the point every later phase is compared against.

## Background
- Epic TS-001 acceptance criteria: a fresh clone followed by `docker compose up` must give a working system with no manual steps.
- So far, CI has tested each service separately. This adds the first test of the whole system working together.

## Scope
**In scope**
- A CI job that starts the full stack with Compose, waits until it's healthy, runs a short smoke test (or k6 in smoke mode), and always tears the stack down
- On failure, CI collects `docker compose logs` as an artifact
- A fresh-clone run on your own machine, and the git tag

## Acceptance criteria
- [ ] AC1: In a fresh clone in a new folder, `cp .env.example .env` followed by `docker compose up -d` gives a healthy stack, and an order can be placed at `localhost:8000`.
- [ ] AC2: The CI job starts the stack, places an order through `frontend`, checks the result, and passes. It shuts the stack down even when a step fails.
- [ ] AC3: When the job fails, the container logs are attached to the run as an artifact.
- [ ] AC4: Every acceptance criterion of epic TS-001 has been checked. Any that aren't met are listed with the ticket that will fix them.
- [ ] AC5: `master` is tagged `phase-1-baseline` (an annotated tag), and the tag is pushed.

## Technical notes
To be refined before pickup. Points to discuss: `docker compose up --wait`, `if: always()` and `if: failure()` in workflows, and the CI time this job adds.

## Definition of done
- [ ] All acceptance criteria met
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
