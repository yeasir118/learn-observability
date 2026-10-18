---
key: TS-007
title: Product database and debug proxy in Compose
type: Task
status: In Progress
priority: High
points: 2
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, docker, compose, postgres, networking]
depends_on: [TS-006]
created: 2026-10-18
---

# TS-007: Product database and debug proxy in Compose

## Summary
Create `tinyshop/infrastructure/compose/compose.yaml` with the first two containers: `product-db` on its internal network, and `product-db-proxy` behind the `debug` profile, so that tools on the host can reach the database through `127.0.0.1:5433`.

## Background
- This is the first half of step 1. The product service itself (TS-008) will run on the host and connect through the proxy.
- Everything in this ticket is already designed in [architecture.md](../tinyshop/docs/architecture.md): see §2 (inventory), §3 (networks) and §8 (local development). This ticket implements that design; it doesn't change it.
- Infrastructure files live under `tinyshop/infrastructure/` ([architecture.md, repository layout](../tinyshop/docs/architecture.md#repository-layout)). This ticket creates the first of them.

## Scope
**In scope**
- `tinyshop/infrastructure/compose/compose.yaml` containing `product-db`, `product-db-proxy`, `product-network`, `debug-network` and the `product-db-data` volume
- `tinyshop/infrastructure/compose/.env.example`, with your real values in a git-ignored `.env` in the same folder
- A health check on `product-db`

**Out of scope**
- Database tables and seed data (TS-008, through EF Core migrations)
- The user and order databases (their own tickets)

## Acceptance criteria
- [ ] AC1: `docker compose up -d`, run in `tinyshop/infrastructure/compose/`, starts **only** `product-db`. `docker compose --profile debug up -d` also starts `product-db-proxy`.
- [ ] AC1a: `compose.yaml` sets the project name `tinyshop`, so `docker network ls` shows `tinyshop_product-network`, not `compose_product-network`.
- [ ] AC2: `product-db` uses `postgres:18-alpine`, creates the database `tinyshop_products`, and stores its data in the named volume `product-db-data`, mounted at the path the Postgres 18 image expects.
- [ ] AC3: The database password comes from `.env` through variable substitution and never appears in `compose.yaml`. `.env.example` lists every variable with a placeholder value and a comment.
- [ ] AC4: `product-network` is `internal: true`, and `product-db` is attached to it and nothing else. `product-db` publishes no ports.
- [ ] AC5: `product-db` has a health check using `pg_isready`, and `docker compose ps` shows it as `healthy`.
- [ ] AC6: `product-db-proxy` is on `debug-network` and `product-network`, publishes `127.0.0.1:5433` to its listening port, and forwards to `product-db:5432`. Pin its image tag.
- [ ] AC7: **Proof, from the host:** with the debug profile running, a client on Windows connects to `localhost:5433` and can run `SELECT version();`. Any client is fine: DBeaver Community, `psql` for Windows, or VS Code's PostgreSQL extension.
- [ ] AC8: **Proof, isolation:** with the debug profile running, `Test-NetConnection <your-LAN-IP> -Port 5433` fails, while `Test-NetConnection localhost -Port 5433` succeeds. Without the debug profile, both fail.
- [ ] AC9: **Proof, persistence:** create a table, run `docker compose down`, run `up` again, and the table is still there. Then run `docker compose down -v`, and it's gone.
- [ ] AC10: **CI:** add a job named `compose` that runs `docker compose -f tinyshop/infrastructure/compose/compose.yaml --env-file tinyshop/infrastructure/compose/.env.example config --quiet`, so an invalid Compose file fails CI. Add `compose` to the ruleset's required checks.

## Technical notes
- **Init behaviour:** the Postgres image creates `POSTGRES_DB` and runs scripts in `/docker-entrypoint-initdb.d` **only when the data directory is empty**. If you change those settings later, nothing happens until you delete the volume. This catches out almost everyone once.
- **`pg_isready`:** look at its `-U` and `-d` options. Without them it checks as a default user, which can produce confusing log lines.
- **socat:** the `alpine/socat` image's entrypoint is `socat`, so `command:` is just socat's arguments. Find out what `fork` and `reuseaddr` do and whether you need them.
- **Project directory:** Compose treats the folder that contains `compose.yaml` as the project directory. It reads `.env` from there, resolves relative paths from there, and by default names the project after it, which would be `compose`. Look up the top-level `name:` element.
- **Variable substitution:** Compose reads `.env` from the project directory to substitute `${VAR}` in `compose.yaml`. That's different from `env_file:`, which sets variables inside a container. Make sure you know which one you're using and why.
- **Windows:** if PostgreSQL is installed locally, it already uses port 5432, which is one reason the proxy uses 5433.

## Learning objectives
- Compose basics: services, networks, volumes, profiles, health checks, variable substitution.
- How the official Postgres image initialises a database, and why initialisation runs only once.
- Turning the design in architecture.md into a working configuration, and proving it with tests that should fail as well as tests that should succeed.

## Questions to answer
1. What's the difference between `.env` (for Compose variable substitution) and `env_file:` on a service?
2. Why does changing `POSTGRES_DB` after the first `up` do nothing? How do you make it take effect?
3. What exactly does `docker compose down -v` delete that `down` doesn't?
4. In AC8, why does connecting to your LAN IP fail? Which layer refuses the connection?

## Definition of done
- [ ] All acceptance criteria met
- [ ] Questions answered in `notes/m1.md`
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
- 2026-10-18: Compose file and `.env` moved to `tinyshop/infrastructure/compose/` (decision D15, updated); added AC1a for the project name
- 2026-10-18: In progress. compose.yaml, .env.example and the CI `compose` job written and verified
