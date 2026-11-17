---
key: TS-017
title: Load generator (k6)
type: Task
status: Done
priority: Medium
points: 3
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, k6, load, compose, networking]
depends_on: [TS-015]
created: 2026-10-18
---

# TS-017: Load generator (k6)

## Summary
Add a k6 load generator as a Compose service behind a `load` profile. It sends a steady, realistic mix of traffic through the frontend, as browsers would: browsing products, looking up users, placing orders and reading them back, plus some invalid requests. Every run ends with k6's summary and leaves an HTML report; while it runs, k6's dashboard shows it live. The assignee writes the k6 script; Claude completes and reviews it, and builds the rest.

## Background
- Metrics and the later exercises are meaningless without traffic (outline §19-§21 and §25); TS-018 runs this generator as CI's end-to-end smoke test.
- k6 is open source (AGPL-3.0) and runs as a container, `grafana/k6`.
- The fault switches (TS-016, `tinyshop/docs/faults.md`) make the traffic fail or slow down on purpose.
- k6 shows only the **client's** view: what a user experiences, not which service caused it. That gap is what M2 and M3 fill.

## Scope
**In scope**
- `tinyshop/infrastructure/load/`: the k6 script, a README, and `reports/` (git-ignored) for the HTML reports; mounted into the stock k6 image at run time
- A Compose service `load-generator` under the `load` profile, with a pinned image, and its own network to the frontend
- Rate and duration as `.env` settings
- CI: the script is parsed (`k6 inspect`) on every pull request
- Docs: architecture.md (§2, §3, §7), faults.md, the README

**Out of scope**
- Running load in CI: TS-018
- Sending k6's metrics to Prometheus or Grafana: M3
- Pass/fail limits on response times: there's no SLO yet

## Decisions (made in refinement, 2026-11-16)
| # | Decision | Why |
|---|---|---|
| 1 | **An open model: `constant-arrival-rate`.** New iterations start at a fixed rate (e.g. 10/s) whether earlier ones have finished or not; `preAllocatedVUs`/`maxVUs` give k6 room to keep up. | With a fixed number of virtual users, each waits for its answer before sending the next, so a slow system automatically gets less load, exactly when a fault makes it slow (*coordinated omission*). Real users keep arriving; the exercises need the honest model. |
| 2 | **One weighted mix of journeys**, using the seed data only (users 123-127, products 1-5): browse `GET /api/products` 45 %; look up a user 15 %; place an order and read it back through its `Location` 20 %; a user's orders 10 %; invalid 10 % (an unknown user or product: 422). | A realistic shape: mostly reads, some writes, some client errors. The read-back also exercises the gateway's `Location` rewrite. No setup and no cleanup needed. |
| 3 | **Every request declares its expected statuses** (`http.expectedStatuses`, k6 checks). A 422 in the invalid journey is expected; a 5xx is always a failure. **Thresholds:** `http_req_failed` rate `== 0` and `checks` rate `== 1`, so an unexpected response makes k6 exit non-zero at the end. | The generator doubles as a smoke test (AC4, TS-018). With faults on, the traffic keeps flowing and only the result turns red, which is what the exercises need. Response times are reported, not judged: no SLO yet. |
| 4 | **Requests are grouped by route** (`tags: { name: 'GET /api/orders/{id}' }`). | Otherwise every order ID becomes its own row in the summary and the report: a first look at the cardinality problem that returns with metrics (M3). |
| 5 | **Its own network, `load-network`, shared only by `frontend` and `load-generator`. Not internal.** The target is `http://frontend:8080`. | It reaches nginx and nothing else, like a browser: the same "one controlled entry point" idea as the database debug proxies. `edge-network` would let it skip nginx and call the gateway; the host's published port would need `host-gateway` tricks on Linux. Not internal because Docker ignores published ports on internal networks, and the dashboard needs one (decision 7). |
| 6 | **Image `grafana/k6:2.3.0`**, an exact version (a small third-party image, architecture.md §2). Dependabot's `docker-compose` entry watches it. **`K6_NO_USAGE_REPORT=true`.** | 2.3.0 was the newest release more than two weeks old at refinement (2.4.0 was four days old). k6 reports anonymous usage to Grafana by default; with internet access (decision 5) that would go out on every run. |
| 7 | **Built-in web dashboard and HTML report, both on:** `K6_WEB_DASHBOARD=true`, `K6_WEB_DASHBOARD_HOST=0.0.0.0` inside the container, published on **`127.0.0.1:5665`** only; `K6_WEB_DASHBOARD_EXPORT` writes one HTML report per run into the mounted `reports/`, named with the run's start time. | Live graphs while the test runs, and a self-contained report to open afterwards, with no extra infrastructure. Checked in refinement: the dashboard listens on `127.0.0.1` inside the container unless told otherwise (a published port couldn't reach it); the report is written by the image's non-root user to a mounted folder; and k6 skips the report for runs of a few seconds ("not enough data"). Loopback only, like the debug proxies; and the `load` profile isn't the default stack, so "only the frontend publishes a port" still holds there. |
| 8 | **Settings in `.env`:** `LOAD_GENERATOR_RATE` (iterations per second, default 10) and `LOAD_GENERATOR_DURATION` (default `30s`, a quick run; exercises set longer ones), passed to the script as environment variables. | One place for every knob, like the fault switches. |
| 9 | **CI parses the script** with `k6 inspect`, using the image pinned in `compose.yaml`, in the `compose` job. | Catches a broken script on every pull request without running load; TS-018 runs it for real. |

## Phases
1. **Refinement** (this document).
2. **The script (assignee drafts, Claude completes and reviews):** the scenario and its options from the environment, the journeys and their weights, expected statuses and checks, route tags, thresholds.
3. **Compose (Claude):** `load-generator` under the `load` profile, `load-network`, the dashboard port, the reports mount, `.env.example`, `.gitignore`.
4. **Proof (Claude):** a run with every fault off (no failures, exit code 0, report written, dashboard reachable); a run with `PAYMENT_SERVICE_FAULT_ERROR_RATE=0.2` (about 20 % of orders fail, exit code non-zero); the reachability matrix of the default stack still OK; the load generator can reach the frontend and nothing else.
5. **CI and docs (Claude):** `k6 inspect` in the `compose` job; architecture.md §2 (a load-profile table), §3 (`load-network`), §7 (the `.env` names); faults.md; `infrastructure/load/README.md`.

## Acceptance criteria
- [x] AC1: `docker compose --profile load up load-generator` produces a steady mix of requests, with the rate and duration set in `.env`.
- [x] AC2: The mix includes valid orders and deliberately invalid ones (an unknown user or product), so 4xx traffic exists too, and k6 counts those as expected.
- [x] AC3: k6's end-of-test summary shows the request count and how long requests took, per route.
- [x] AC4: The run fails (non-zero exit code) on unexpected responses, such as 5xx while no faults are switched on, and passes with every fault off.
- [x] AC5: Every run of normal length leaves an HTML report in `tinyshop/infrastructure/load/reports/`, which git ignores.
- [x] AC6: While a run is going, k6's dashboard is at `http://localhost:5665`, from this machine only.
- [x] AC7: The load generator can reach the frontend and nothing else; the default stack's reachability matrix is unchanged.
- [ ] AC8: CI parses the script on every pull request.
- [x] AC9: Docs updated: architecture.md §2, §3, §7; faults.md; the load README.

## Technical notes
These are pointers, not the solution.
- **One scenario, weighted journeys:** pick a journey per iteration from a random number and the weights; each journey is a small function. Scenario options can come from `__ENV`.
- **`http.expectedStatuses(...)`** is set per request (`responseCallback`), not globally, since a 422 is expected only in the invalid journey.
- **Read-back:** the `Location` of `POST /api/orders` is relative (`/api/orders/1001`); prepend the base URL.
- **The report's name:** `K6_WEB_DASHBOARD_EXPORT` is a plain path, so a per-run name has to be built where k6 is started (the Compose command), not in the script.
- **`maxVUs`:** with a 3 s payment delay and 2 orders/s, at least 6 iterations are in flight at once. Too few VUs and k6 reports `dropped_iterations` instead of sending the load.

## Learning objectives
- Open and closed load models, and why the closed one hides slowness
- What a load test's numbers say (rate, failure rate, percentiles) and what they can't (where the time went)
- Expected and unexpected failures in a test
- A dedicated network as the only way in

## Questions to answer
Write the answers in `notes/`.
1. Open against closed model: with `PAYMENT_SERVICE_FAULT_DELAY=00:00:03`, what happens to the number of orders per second in each, and why?
2. Why is a 422 in the invalid journey not a failure, while a 502 is?
3. What does p95 tell you that the average doesn't?
4. With a payment delay on, what can the k6 report tell you, and what can't it? What would you need to find which service is slow?
5. Why group requests by route (`GET /api/orders/{id}`) instead of the real URL?

## Definition of done
- [ ] All acceptance criteria met
- [x] Questions answered in `notes/`
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
- 2026-10-18: Updated for the `tinyshop/infrastructure/` layout
- 2026-11-16: Refined. Decisions 1-9, phases, AC5-AC9, technical notes, learning objectives and questions added; the target is `frontend:8080` (TS-015); the HTML report and the live dashboard added; `needs-refinement` removed; points 2 → 3. The assignee writes the script; Claude does the rest. Checked against `grafana/k6:2.3.0`: the dashboard needs `K6_WEB_DASHBOARD_HOST=0.0.0.0` in a container, the report is written to a mounted folder, short runs skip it
- 2026-11-16: Started (branch `TS-017-load-generator`)
- 2026-11-17: Phase 2 done: the script, written by the assignee step by step (one request, then the scenario and thresholds, then each journey, route names and settings), completed and reviewed by Claude
- 2026-11-17: Phase 3 done: `load-generator` (load profile), `load-network`, the dashboard port, the reports mount, `.env.example`, `.gitignore`. Default duration `30s` instead of `5m` (decision 8 updated): a quick run by default, exercises set longer ones
- 2026-11-17: Phase 4 proof, in a separate Compose project (`ts017-p4`, `.env.example`): reachability matrix unchanged (OK); faults off: 356 requests, every check passed, exit code 0, report written, dashboard 200 on 127.0.0.1 and unreachable on the LAN address; from `load-network`, only `frontend` is reachable (by name and by IP); `PAYMENT_SERVICE_FAULT_ERROR_RATE=0.2`: 13 of 73 orders failed (18 %), only the order checks red, exit code 99. AC1-AC7 met
- 2026-11-17: Phase 5: `k6 inspect` in the `compose` job, through the `load-generator` service (`run --no-deps`; checked locally: the script passes, a broken one fails with exit 107); architecture.md (layout, §2 load-profile table and image pinning, §3 `load-network` and egress, §4 note, §7, §8 host ports), faults.md ("Watching a fault with the load generator"), `infrastructure/load/README.md`. AC9 met; AC8 once CI runs on the pull request
- 2026-11-17: Questions answered in `notes/m1.md`. The documented way to run is now `docker compose --profile load run --rm --service-ports load-generator` (only k6's output, no container left behind) and `docker compose --profile load down`
