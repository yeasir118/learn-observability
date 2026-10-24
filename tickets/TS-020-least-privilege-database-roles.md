---
key: TS-020
title: Least-privilege database roles for product-service
type: Task
status: In Review
priority: High
points: 3
assignee: claude
reporter: yeasir118
epic: TS-001
labels: [m1, postgres, security, compose, testing]
depends_on: [TS-010]
created: 2026-10-25
---

# TS-020: Least-privilege database roles for product-service

## Summary
Stop using the Postgres superuser for everything. product-db gets three roles with one job each: the superuser only initialises the database, `product_migrator` owns the schema and is used only by the migration job, and `product_app` is read-only and used by product-service.

## Background
- After TS-010, both `product-db-migrate` and `product-service` connected as `POSTGRES_USER`, the superuser the image creates. A service that only reads products could drop tables, create roles, or read any other database on the server.
- The least-privilege split was noted as the remaining gap in the TS-010 migrations discussion.
- Decision D24 (epic TS-001) makes this the pattern for every service that owns a database.

## Scope
**In scope**
- A shared, reusable init script: `tinyshop/infrastructure/postgres/initdb/10-create-service-roles.sh`
- product-db configured through its environment; separate connection strings for the migration job and the service
- Integration tests running against the same script and roles, plus tests for the role rules
- Documentation: architecture.md §10 "Database roles", local-development guidance in §8

**Out of scope**
- Password rotation, and short-lived credentials (for example from Vault; see the roadmap)
- user-db and order-db (they reuse the script when they're built: TS-011, TS-013)

## Acceptance criteria
- [x] AC1: The init script creates `<prefix>_migrator` and `<prefix>_app`; neither is a superuser or can create roles or databases.
- [x] AC2: `PUBLIC` can't connect to the database or create anything in the schema; a role without grants is refused at connection time.
- [x] AC3: The migrator owns the `public` schema and every table the migrations create.
- [x] AC4: The app role can only `SELECT`. Insert, update, delete, create table, temporary tables, drop and alter are refused.
- [x] AC5: Default privileges: a table the migrator creates later is automatically readable by the app, and only readable.
- [x] AC6: The script is reusable: with `APP_TABLE_PRIVILEGES=SELECT, INSERT, UPDATE` the app can insert into identity columns (sequence privileges granted) and update, and still can't delete.
- [x] AC7: Bad input is rejected: a privilege list containing anything but the four DML keywords stops initialisation with exit code 1, before anything is created, whether the script is executed or sourced.
- [x] AC8: Compose: `product-db-migrate` connects as `product_migrator`, `product-service` as `product_app`; the stack starts on an empty volume, and a second `up` is harmless.
- [x] AC9: The database health check uses TCP, so the migration job can't start while init scripts are still running.
- [x] AC10: Integration tests use the same script and roles; role tests fail if the app role is given too much (verified by mutation).
- [x] AC11: `.env.example` files and architecture.md are updated; CI's Compose check passes with the template.

## Upgrading an existing local stack
The script runs only when a volume is created, so an existing `product-db-data` volume keeps the old single superuser. To switch:
1. Update `infrastructure/compose/.env` from `.env.example`: remove `PRODUCT_DB_USER` and `PRODUCT_DB_PASSWORD`, and set `PRODUCT_DB_ADMIN_PASSWORD`, `PRODUCT_DB_MIGRATOR_PASSWORD` and `PRODUCT_DB_APP_PASSWORD`.
2. Recreate the database: `docker compose --profile '*' down -v`, then `docker compose up -d --build`. The migration job re-seeds the data.
3. Update the local `ConnectionStrings:ProductDb` user secret to `Username=product_app` with the new password, and pass the migrator's connection string explicitly to `dotnet ef database update --connection` (architecture.md §8).

## Learning objectives
- Least privilege applied to databases: separate roles for schema changes and for runtime access.
- Postgres privileges: `PUBLIC`, ownership, schema `USAGE` and `CREATE`, default privileges.
- How the official Postgres image initialises a database, and its traps (runs once; sourced vs executed scripts; the temporary socket-only server).

## Questions to answer
1. Why isn't a `GRANT SELECT ON ALL TABLES` enough, and what do default privileges add?
2. Why does the app role get "must be owner of table" for `DROP`, but "permission denied" for `INSERT`?
3. What would go wrong if the database health check used the Unix socket (no `-h`)?
4. Which privileges will order-service's app role need, and why does `INSERT` also require sequence privileges?

## Definition of done
- [x] All acceptance criteria met
- [ ] Questions answered in `notes/`
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-25: Created and implemented by Claude at the assignee's request. Verified under a separate Compose project: roles and owners, every allowed and refused operation per role, default privileges, a reusable INSERT configuration with the script sourced, injection rejected (exit 1) in both execution modes, fresh and repeated startup. Integration tests: 36 (11 new role tests), all passing; a mutation that over-grants the app role is caught. During testing, found and fixed a bug: the validation didn't stop the script when it was executed rather than sourced
