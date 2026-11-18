# Load generator (k6)

Steady, realistic traffic for TinyShop (TS-017): browsing, user lookups, orders and some deliberately invalid requests, sent through nginx as a browser would. While it runs, k6's dashboard shows it live; at the end k6 prints a summary, leaves an HTML report and exits with the result.

| File | What it is |
|---|---|
| `tinyshop.js` | The k6 script: the scenario, the journeys, their checks and the pass/fail rules |
| `reports/` | One HTML report per run (git-ignored; `.gitkeep` keeps the folder) |

Nothing here is built into an image: Compose mounts this folder into the stock `grafana/k6` image (architecture.md §1, the "baked in or mounted" rule).

## Running it

From `tinyshop/infrastructure/compose/`:

```bash
# 1. The stack, healthy
docker compose up -d --wait

# 2. One load-test run; repeat as often as you like
docker compose --profile load run --rm --service-ports load-generator

# 3. Done testing: everything, the load profile included
docker compose --profile load down
```

- **`run`** shows only k6's output (`up load-generator` works too, but mixes in every dependency's logs). It runs once, for `LOAD_GENERATOR_DURATION` at `LOAD_GENERATOR_RATE` iterations per second (`.env`; defaults `30s` and `10`), and waits for the stack to be healthy first.
- **`--service-ports`:** `run` doesn't publish a service's ports by default, and the dashboard needs its port.
- **`--rm`** removes the container after the run. Otherwise a stopped one is left behind, and after a `down` without `--profile load` it still points at a network that no longer exists ("network … not found").
- **Live:** <http://localhost:5665> while the run is going. It closes when the run ends. Loopback only: not reachable from other machines.
- **Afterwards:** k6's summary in the terminal, and `reports/tinyshop-<start time, UTC>.html` (open it in a browser). Runs of only a few seconds write no report: k6 needs some data first.
- **The result** is the command's exit code (`echo $?`; in PowerShell `$LASTEXITCODE`): `0` when every response was expected and every check passed, `99` when a threshold failed.
- **Ctrl+C** ends a run early; k6 still prints its summary and writes the report.
- **After changing a fault** in `.env`, run step 1 again before the next run.

One-off settings without editing `.env`: `LOAD_GENERATOR_RATE=50 LOAD_GENERATOR_DURATION=2m docker compose --profile load run --rm --service-ports load-generator` (Git Bash or Linux).

## What it sends

Each iteration is one visitor doing one thing, picked at random by weight:

| Weight | Journey | Requests | Expected |
|---|---|---|---|
| 45 % | Browse | `GET /api/products` | 200, a non-empty list |
| 15 % | Look up a user | `GET /api/users/{id}`, a seeded user (123-127) | 200, the user asked for |
| 20 % | Place an order and read it back | `POST /api/orders` (seeded user, product 1-5, quantity 1-5), then `GET` its `Location` | 201 with a `Location`, then 200 |
| 10 % | A user's orders | `GET /api/orders?userId={id}` | 200, a list (empty is fine) |
| 10 % | Invalid order | `POST /api/orders` with an unknown user or product (999999) | **422**, `user_not_found` or `product_not_found` |

Only the seed data is used, so no setup or cleanup is needed. Every valid order is a real order in `order-db`.

## How it judges

- **Open model** (`constant-arrival-rate`): new iterations start at the set rate whether earlier ones have finished or not, as real visitors keep arriving. A slow system therefore gets the same load, not less. Each iteration in flight needs a virtual user (VU): the script allows one second's worth up front and five seconds' worth at most (`RATE` x 5). If k6 reports `dropped_iterations`, requests are taking longer than that and the load wasn't fully sent.
- **Expected statuses:** k6 counts every 4xx and 5xx as failed by default. The invalid order sets `http.expectedStatuses(422)` on its own request only, so its 422 is a success and a 422 anywhere else is still a failure.
- **Checks** look at each response (status, and one fact about the body or headers).
- **Thresholds** make the run pass or fail: `http_req_failed` rate `== 0`, `checks` rate `== 1`, and `orders_placed` count `> 0` (a custom metric: orders that came back 201 with a `Location`; `http_reqs` for `POST /api/orders` would count the invalid orders too). Response times are reported, never judged: there's no SLO yet.
- **Route names:** every request is tagged with its route (`GET /api/orders/{id}`), so the summary and the report group by route instead of by URL. Empty thresholds on `http_req_duration{name:...}` and `http_reqs{name:...}` make the summary print one row per route.

## The network

The load generator is on `load-network` only, shared with `frontend`: it reaches nginx and nothing behind it (architecture.md §3). The network isn't internal, because the dashboard publishes a port; that also gives it internet access, so `K6_NO_USAGE_REPORT=true` stops k6 from reporting usage to Grafana.

## Running the script without Compose

Against the stack's published port, from the repository root (Git Bash; in PowerShell pipe the file in with `Get-Content`):

```bash
docker run --rm -i -e BASE_URL=http://host.docker.internal:8000 -e RATE=5 -e DURATION=30s \
  grafana/k6:2.3.0 run - < tinyshop/infrastructure/load/tinyshop.js
```

The script's own defaults are `BASE_URL=http://host.docker.internal:8000`, `RATE=10` and `DURATION=30s`; Compose always sets all three.

## Known limits

- **Linux hosts and `reports/`:** the image runs as uid 12345. Docker Desktop ignores folder ownership, but on Linux that user can't write to a `reports/` folder owned by your user. Make it writable (`chmod a+w reports`, as CI does) or run the service as your user.
- **k6 shows the user's view only:** what failed or was slow, not which service caused it. That's the later milestones' job.

## In CI

Two jobs use it on every pull request:
- **`compose`** parses the script (`k6 inspect`), without starting anything.
- **`system`** runs it as the end-to-end smoke test (TS-018): after the stack is up and the reachability matrix is checked, `docker compose --profile load run --rm load-generator` at 5 iterations per second for 30 s. Any unexpected answer, a failed check, or no order created at all (`orders_placed`, a counter of our own) fails the job. The HTML report is attached to every run as the artifact `k6-report`; when the job fails, the container logs are attached as `compose-logs`.
