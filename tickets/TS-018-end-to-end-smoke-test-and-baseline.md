---
key: TS-018
title: End-to-end smoke test in CI and phase-1-baseline tag
type: Task
status: In Progress
priority: High
points: 3
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, ci, docker, compose, k6]
depends_on: [TS-016, TS-017]
created: 2026-10-18
---

# TS-018: End-to-end smoke test in CI and phase-1-baseline tag

## Summary
Prove that the complete system works, end to end, from a fresh clone: on your machine by hand, and in CI on every pull request, where the k6 load generator places real orders through the frontend and fails the build on any unexpected answer. Then check the epic's acceptance criteria and tag the result `phase-1-baseline`, the point every later phase is compared against. The assignee writes the CI steps and does the fresh clone and the tag; Claude completes and reviews the CI steps, and does the rest.

## Background
- Epic TS-001's acceptance criteria: a fresh clone followed by `docker compose up` must give a working system with no manual steps.
- CI tests each service on its own (`dotnet`, `frontend`), builds every image (`image`), and since TS-014 starts the whole stack to prove the reachability matrix (`network`). Nothing yet proves that a request **works** through all of it: nginx, the gateway, three services, gRPC and three databases together.
- TS-017's load generator already does exactly that, with thresholds that fail on any unexpected answer, and its exit code is the result.

## Scope
**In scope**
- The CI job that starts the stack: a short k6 run after the matrix check, the HTML report as an artifact on every run, and the container logs as an artifact when the job fails
- A threshold that guarantees orders were actually placed
- A deliberate failure, to prove the failure path (red build, logs attached)
- A fresh-clone run on your machine
- The epic's acceptance criteria checked; the `phase-1-baseline` tag

**Out of scope**
- Load or performance testing in CI: the run is short and gentle, and response times aren't judged (no SLO)
- Caching Docker layers in CI to make the job faster
- Failure Exercise 1: TS-019

## Decisions (made in refinement, 2026-11-17)
| # | Decision | Why |
|---|---|---|
| 1 | **The smoke test goes into the existing `network` job, renamed `system`.** Order: build and start the stack, the matrix, the smoke test, then the artifacts and `down`. | That job already builds and starts the whole stack (several minutes); a second job would do it all again. "system" says what it is now: the whole system, its networks and its behaviour. The `master` ruleset requires only `ci-passed`, so the rename only changes `ci-passed`'s `needs`. |
| 2 | **The smoke test is the load generator itself:** `docker compose --profile load run --rm load-generator`, at 5 iterations per second for 30 s (`LOAD_GENERATOR_RATE`, `LOAD_GENERATOR_DURATION` set by the step). | Its thresholds fail on any unexpected answer, and its exit code fails the step: no second test tool. It also proves TS-017's script really runs. 30 s at 5/s is about 150 journeys and 30 orders, enough for a report (decision 4), short enough for CI. |
| 3 | **A new threshold: at least one order was placed:** a custom k6 metric, `orders_placed` (a `Counter`, incremented for each 201 with a `Location`), with `count > 0`. | Journeys are random. 30 orders are expected, but "expected" isn't "guaranteed": a broken weights table would let the run pass without one order. The threshold makes "an order was placed and checked" a rule (AC2). Not `http_reqs{name:POST /api/orders}`, the first draft: that route also carries the invalid orders, so it stays above 0 even when no order can be created (found in phase 2). |
| 4 | **The k6 report is uploaded on every run** (`if: always()`), the container logs **when the job fails** (`if: failure()`), both with `actions/upload-artifact`, pinned to a commit SHA (v7.0.2, released 2026-10-07). Kept 14 days. | The report shows every CI run's numbers, green or red. The logs are what you need to find out why a run failed; printing them into the job's output (as today) makes them hard to search. |
| 5 | **`reports/` is made writable before the run** (`chmod a+w`). | On Linux, k6 runs as uid 12345 and the checkout belongs to the runner's user, so k6 can't write the report (the known limit in the load README). Docker Desktop hides this; CI is the first Linux host to run it. |
| 6 | **The failure path is proven on purpose,** once: a temporary commit that turns on `PAYMENT_SERVICE_FAULT_ERROR_RATE=1` for the job must turn the build red and attach the logs. Then it's reverted. | A test that has never failed hasn't been tested. It also proves AC3, which only a failure can show. |
| 7 | **The fresh clone uses its own Compose project name** (`docker compose -p tinyshop-fresh ...`). | `compose.yaml` sets `name: tinyshop`. A clone in another folder would otherwise reuse your existing `tinyshop_*` volumes (databases already migrated and seeded), so it wouldn't be a fresh start. Your own stack must be stopped first: both publish port 8000. |
| 8 | **The tag is annotated,** on `master` after this ticket is merged: `git tag -a phase-1-baseline`. Pushed explicitly. | An annotated tag records who, when and why, like a commit; a lightweight one is only a name. `git push` doesn't send tags unless asked. |

## Phases
1. **Refinement** (this document).
2. **The smoke test in CI (assignee drafts, Claude completes and reviews):** the threshold in the script; the job renamed `system`, the `chmod`, the k6 step.
3. **Artifacts (assignee drafts, Claude completes and reviews):** the report on every run; the logs, into a file, on failure.
4. **Proof in CI (assignee pushes, Claude reads the results):** a green run with both outcomes checked (report attached, no logs); the deliberate failure (red, report and logs attached); the revert.
5. **Fresh clone and the epic (assignee: the clone; Claude: the criteria):** the fresh-clone run on your machine; every epic criterion checked and listed; docs (architecture.md §4, the load README, the tickets README's CI section).
6. **Merge and tag (assignee):** merge, then `phase-1-baseline` on `master`, pushed. Claude: the closing bookkeeping.

## Acceptance criteria
- [x] AC1: In a fresh clone in a new folder, `cp .env.example .env` followed by `docker compose up -d`, both in `tinyshop/infrastructure/compose/`, gives a healthy stack, and an order can be placed at `localhost:8000`.
- [x] AC2: The CI job starts the stack, places orders through `frontend`, checks the results, and passes. It shuts the stack down even when a step fails.
- [x] AC3: When the job fails, the container logs are attached to the run as an artifact.
- [x] AC4: Every acceptance criterion of epic TS-001 has been checked. Any that aren't met are listed with the ticket that will fix them.
- [ ] AC5: `master` is tagged `phase-1-baseline` (an annotated tag), and the tag is pushed.
- [x] AC6: Every CI run of the job attaches the k6 report.

## Epic criteria check (AC4)
Epic TS-001's acceptance criteria, checked on 2026-11-18:

| Criterion | Status | Evidence |
|---|---|---|
| A fresh clone followed by `docker compose up` gives a working system with no manual steps | met | AC1: the fresh-clone run (phase 5) worked first time. One step is unavoidable and documented: `cp .env.example .env`. Compose refuses to start without the passwords (`:?`), on purpose: secrets are never committed (D19) |
| Shopping works through the UI | met | TS-026 (the app), TS-015 (served by nginx at `localhost:8000`, all four pages checked); the same `/api/*` routes are exercised in CI by the smoke test |
| HTTP between the gateway and the services, and from Order to User and Product; gRPC from Order to Payment | met | architecture.md §5.2; TS-013, TS-014. Every order in the smoke test goes through both |
| Each service owns its database, and no service can reach another's | met | The reachability matrix, proven by name and by IP in CI's `system` job on every pull request (TS-014) |
| Only the frontend publishes a port by default | met | The matrix's browser row, in CI. The `debug` and `load` profiles publish on `127.0.0.1` only, and aren't the default stack |
| All services implement the contracts | met | Each service's integration tests and the gateway's tests check their part of contracts.md (CI `dotnet` jobs); the smoke test checks the whole chain, including the 422 errors and the `Location` rewrite |
| Faults through environment variables, without rebuilding | met | TS-016, faults.md |
| The known gaps are written down in `notes/` | met | `notes/m1.md`, "Known gaps of M1" (added in this ticket), from contracts.md §8 and the TS-015/TS-016 experiments |
| Failure Exercise 1 is written up | **not met** | TS-019, which depends on this ticket |
| `master` is tagged `phase-1-baseline` | pending (phase 6) | AC5 |

## Technical notes
These are pointers, not the solution.
- **`if:` on a step:** a step normally runs only if every step before it succeeded. `if: always()` runs it anyway; `if: failure()` runs it only when an earlier step failed. The existing "Remove the stack" step already uses `always()`.
- **Environment variables for one step:** a step can have its own `env:`. Variables set in the shell win over `.env` (here `.env.example`) when Compose substitutes `${...}`.
- **`docker compose run` and the exit code:** `run` returns the container's exit code, so a failed threshold (99) fails the step. Without `--service-ports`, no port is published: the dashboard still runs inside the container, which is harmless.
- **`upload-artifact`:** takes a `name`, a `path` and optionally `retention-days` and `if-no-files-found`. Paths are relative to the repository root, not to the job's `working-directory`.
- **Logs into a file:** `docker compose logs --no-color --timestamps > <file>`. Write it somewhere the upload step can find it (the runner's temporary folder `${{ runner.temp }}` is a good place: never inside the repository).

## Learning objectives
- What a smoke test is, and why a system test belongs in CI next to the unit tests
- GitHub Actions: step conditions (`always()`, `failure()`), step `env`, artifacts
- An exit code as the contract between a tool and CI
- What "works from a fresh clone" really tests
- Git tags: annotated and lightweight, and pushing them

## Questions to answer
Write the answers in `notes/`.
1. What does this smoke test prove that the unit and integration tests of each service can't?
2. Why must "Remove the stack" run with `if: always()`, and why are the logs uploaded only `if: failure()` but the report `always()`?
3. What did the fresh clone check that running the stack in your usual folder doesn't? Name something that could work there and still fail in a fresh clone.
4. Why prove the failure path with a deliberate failure?
5. Annotated or lightweight tag: what's the difference, and why does a baseline deserve an annotated one?

## Definition of done
- [ ] All acceptance criteria met
- [x] Questions answered in `notes/`
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
- 2026-10-18: Updated for the `tinyshop/infrastructure/` layout
- 2026-11-17: Refined. Decisions 1-8, phases, AC6, technical notes, learning objectives and questions added; the smoke test is TS-017's load generator, in the existing job renamed `system`; `needs-refinement` removed. The assignee writes the CI steps and does the fresh clone and the tag; Claude does the rest
- 2026-11-17: Started (branch `TS-017-phase-1-baseline`, renamed `TS-018-end-to-end-smoke-test`)
- 2026-11-18: Phase 2 done: the job renamed `system`, the `chmod` and the smoke-test step (assignee), comments and the `orders_placed` counter (Claude). Rehearsed locally in a separate Compose project (`ts018`, `.env.example`): matrix OK; smoke test 179 requests, every check passed, 31 orders placed, exit code 0. With `PAYMENT_SERVICE_FAULT_ERROR_RATE=1`: `orders_placed` 0, three thresholds crossed, exit code 99. Learned on the way: a fault set only on one command is undone by the next `docker compose` command without it (`run` recreates the dependency to match its config)
- 2026-11-18: Phase 3 done: three steps replace "Show container status and logs": the logs (`ps` and `logs` into `$RUNNER_TEMP`) and their upload `if: failure()`, the k6 report's upload `if: always()` (`if-no-files-found: warn`), both kept 14 days, `actions/upload-artifact` pinned to v7.0.2 (assignee); comments (Claude). Artifacts can only be checked on GitHub: phase 4
- 2026-11-18: Phase 4 done (assignee; results checked on GitHub): pushed with a draft pull request; green run with `k6-report` attached and the log steps skipped; a temporary commit with `PAYMENT_SERVICE_FAULT_ERROR_RATE: "1"` in the job's `env` turned the job red, with `k6-report` and `compose-logs` attached and the stack removed; reverted with `git revert` (commits `ce481ec`, `d926198`). AC2, AC3, AC6 met
- 2026-11-18: Phase 5, Claude's part: epic criteria checked (table above; Failure Exercise 1 open, TS-019); "Known gaps of M1" added to `notes/m1.md`; epic criteria ticked where met; docs: architecture.md §4 (the job's new name), the load README ("In CI", `orders_placed`, the Linux limit), tickets README's CI policy (the `system` job)
- 2026-11-18: Phase 5, the assignee's part: fresh clone of the branch into a new folder, `cp .env.example .env`, `docker compose -p tinyshop-fresh up -d --build --wait`: healthy, shopping works at `localhost:8000`, worked first time; the clone and its volumes removed. AC1 and AC4 met
- 2026-11-18: Questions answered in `notes/m1.md`. Left before Done: merge, then the `phase-1-baseline` tag (AC5, phase 6)
