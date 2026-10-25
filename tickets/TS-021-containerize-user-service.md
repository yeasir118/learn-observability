---
key: TS-021
title: Containerize the user service
type: Story
status: To Do
priority: High
points: 3
assignee: claude
reporter: yeasir118
epic: TS-001
labels: [m1, docker, compose, postgres, networking, ci, needs-refinement]
depends_on: [TS-011]
created: 2026-10-25
---

# TS-021: Containerize the user service

## Summary
Run user-service in Compose exactly the way product-service runs: an image, its own database with least-privilege roles, a migration job, a debug proxy and network isolation.

## Background
- Split out of TS-011 at the assignee's request: TS-011 delivers the .NET solution; the container and infrastructure work, which repeats the product-service pattern the assignee has already learned (TS-007, TS-010, TS-020), moves here.
- Everything follows decisions D21-D24 and architecture.md: §2 (inventory), §3 (networks), §8 (`127.0.0.1:5434` for the proxy), §10 (startup chain, health check, database roles).

## Scope
**In scope**
- `services/user-service/Dockerfile` with `runtime` (Alpine) and `migrations` targets, like product-service's
- Compose: `user-db` (`tinyshop_users`, init script with `DB_ROLE_PREFIX=user`, `APP_TABLE_PRIVILEGES=SELECT`), `user-db-migrate`, `user-service`, `user-db-proxy` (debug profile), `user-network`; connection strings defined once with anchors
- `.env.example` entries for the three user-db passwords
- CI: add user-service to the `image` job matrix (with `migrations: true`); add its folder to Dependabot's `docker` directories

## Acceptance criteria
- [ ] AC1: `docker compose up` on empty volumes brings up the product and user slices; both services become healthy, after their migration jobs exit 0.
- [ ] AC2: `user-db` is reachable only by `user-service` and `user-db-migrate`; a container on `product-network` (or `backend-network`) can't reach it.
- [ ] AC3: From `backend-network`, `GET http://user-service:8080/users/123` returns Alice.
- [ ] AC4: CI builds both user-service images; `ci-passed` covers them.
- [ ] AC5: architecture.md and the TS-011 duplication list are updated if anything differs from product-service.

## Technical notes
To be refined before pickup. Most files will be near-copies of product-service's. Points to discuss: whether the Dockerfile's build-time placeholder connection string is still needed (user-service has a design-time DbContext factory, so `dotnet ef migrations bundle` no longer starts the app's host), and whether to extract the repeated Compose blocks with YAML anchors or `extends`.

## Definition of done
- [ ] All acceptance criteria met
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-25: Created, split out of TS-011
