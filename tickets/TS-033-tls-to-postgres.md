---
key: TS-033
title: TLS to Postgres
type: Story
status: To Do
priority: High
points: 5
assignee: yeasir118
reporter: claude
epic: TS-027
labels: [m1.5, tls, postgres, ef-core, needs-refinement]
depends_on: [TS-031]
created: 2026-11-19
---

# TS-033: TLS to Postgres

## Summary
Every connection to the three databases is encrypted and the server verified: the services, the migration jobs and your own tools through the debug proxies. Postgres refuses plaintext connections outright.

## Background
- Epic TS-027, decisions D30 (verification never off: `VerifyFull`, not `Require`), D36 (`hostssl` only).
- Postgres negotiates TLS inside its own protocol (an `SSLRequest` first), or, since PostgreSQL 17, directly (`sslnegotiation=direct`); PostgreSQL 18 adds `ssl_tls13_ciphers` and renames `ssl_ecdh_curves` to `ssl_groups`.
- Open question from the epic, decided here: integration tests (Testcontainers) with TLS or without.

## Scope
**In scope**
- Each database: a server certificate (`<service>-db.tinyshop.internal`), `ssl = on`, TLS 1.3 minimum, a `pg_hba.conf` that accepts only `hostssl` and rejects everything else from the network
- Npgsql connection strings: `SSL Mode=VerifyFull`, the root as trusted certificate; the migration jobs the same
- The debug proxies pass TLS through unchanged; `psql` and GUI tools from the host verify the server (runbook in `tls.md`)
- TLS matrix rows: a plaintext connection is refused; a client that verifies against another CA is refused

## Acceptance criteria (draft)
- [ ] AC1: Every database connection uses TLS 1.3 with a verified server (`pg_stat_ssl` shows it).
- [ ] AC2: A plaintext connection from the network is refused by `pg_hba.conf`.
- [ ] AC3: `psql` from the host through a debug proxy connects with `sslmode=verify-full`.

## Learning objectives
- How a database protocol adds TLS; `sslmode` levels and why only `verify-full` authenticates the server
- `pg_hba.conf` as the database's own access rules

## Activity log
- 2026-11-19: Created with epic TS-027
