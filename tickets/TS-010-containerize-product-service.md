---
key: TS-010
title: Containerize the product service
type: Story
status: In Progress
priority: High
points: 5
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, docker, compose, networking, ef-core, ci]
depends_on: [TS-009]
created: 2026-10-18
---

# TS-010: Containerize the product service

## Summary
Package `product-service` as a Docker image and run it in Compose next to `product-db`, so the whole product slice runs with `docker compose up` and no tools on the host.

## Background
- This is step 2. The service moves from `localhost:5101` → `localhost:5433` to `8080` → `product-db:5432`. See the comparison table in [architecture.md §8](../tinyshop/docs/architecture.md#8-local-development).
- `product-service` joins `backend-network` (internal) and `product-network` (internal). Nothing is published to the host yet, because the gateway arrives in TS-014.
- This ticket resolves three open questions in architecture.md §9: Q2 (image pinning), Q4 (startup order), and how the service's health check runs inside the container.

## Scope
**In scope**
- `tinyshop/services/product-service/Dockerfile` and a `.dockerignore`
- The `product-service` service and `backend-network` in `infrastructure/compose/compose.yaml`
- A decision on how migrations run, and its implementation
- Container health check and startup ordering
- CI builds the image

**Out of scope**
- Publishing an image to a registry
- Other services

## Acceptance criteria
- [ ] AC1: A **multi-stage** Dockerfile: the SDK image builds, and the `aspnet` runtime image runs. The final image contains no SDK and no source code.
- [ ] AC2: Layer caching works: changing a `.cs` file and rebuilding doesn't re-run `dotnet restore`. Your notes show the `docker build` output that proves it.
- [ ] AC3: The container runs as a **non-root** user and listens on 8080.
- [ ] AC4: A `.dockerignore` keeps `bin/`, `obj/`, `.git`, `.env` and other irrelevant files out of the build context.
- [ ] AC5: In `compose.yaml`, `product-service` is on `backend-network` and `product-network` and publishes no ports. Its connection string comes from environment variables following the naming in architecture.md §7, and points at `product-db:5432`.
- [ ] AC6: **Migrations:** pick one approach and record the decision and its trade-offs in architecture.md:
  - a migration bundle run by a one-off container before the service starts
  - migrations applied at startup, behind a flag that's only on in Compose
  - a separate init service
- [ ] AC7: **Startup order:** `product-service` starts only once `product-db` is healthy (and, if you chose a one-off migration container, once that has finished successfully). `docker compose up` on an empty volume works on the first try, every time.
- [ ] AC8: **Health:** `product-service` has a Docker health check against `/health/ready`, and `docker compose ps` shows it as `healthy`. Record in architecture.md how the check makes its HTTP request, given that the default `aspnet` image has no `curl` or `wget`.
- [ ] AC9: **Proof:** a throwaway `nicolaka/netshoot` container on `backend-network` gets 200 from `curl http://product-service:8080/products`. The same command from `debug-network` (a network product-service is **not** on) fails, and your notes say at which step it fails (name lookup or connection) and why. A netshoot on `product-network` *would* reach it, because product-service is on that network too.
- [ ] AC10: Images are pinned according to your decision on architecture.md Q2. Q2, Q4 and the health-check question are marked resolved in architecture.md §9.
- [ ] AC11: **CI:** a job builds the product-service image (`docker build`, or `docker compose build`) on every pull request, without pushing it anywhere.

## Technical notes
- **Build context:** the Dockerfile is in `services/product-service/`, but the build needs files from higher up: `Directory.Packages.props` (without it, restore has no package versions), `Directory.Build.props` and `global.json`. So the build context is `tinyshop/`, not the service folder (architecture.md, repository layout). In `compose.yaml`, paths are relative to `infrastructure/compose/`, so that's `context: ../..` with `dockerfile: services/product-service/Dockerfile`. Copy only what this service needs into the image.
- **Where `.dockerignore` goes:** Docker reads it from the root of the build context, so with a `tinyshop/` context it's `tinyshop/.dockerignore`, shared by every image. BuildKit also supports a Dockerfile-specific `services/product-service/Dockerfile.dockerignore`. Decide which you want and why.
- **Non-root:** .NET 8+ images include an `app` user, and `$APP_UID` holds its ID. Look up how the official images expect you to use it.
- **Health check without curl:** the options include installing `curl` (which makes the image bigger and adds attack surface), using an `-alpine` runtime image (busybox includes `wget`), or a small health-check program written in .NET. Each has trade-offs; pick one and justify it.
- **`depends_on`:** look at `condition: service_healthy` and `condition: service_completed_successfully`.
- **Migration bundle:** `dotnet ef migrations bundle` produces a self-contained executable that applies migrations. Find out what it needs at run time.
- **Chiseled images** (`-noble-chiseled`) are smaller and contain no shell at all. That's appealing, but it changes what a health check can do. Consider them, then decide.

## Learning objectives
- Multi-stage builds, layer caching, build contexts, `.dockerignore`.
- Running containers as non-root, and why it matters.
- How service-to-service networking and DNS work inside Compose, and why the connection string had to change.
- Startup ordering and health in a system of several containers.
- How to run schema migrations in a containerized deployment.

## Questions to answer
1. Which line of your Dockerfile invalidates the cache when a `.cs` file changes, and why do you copy the `.csproj` and restore before copying the rest?
2. What's the size of your final image compared with the SDK image? Where does the difference come from?
3. Why is "migrate at startup" risky with several instances of a service? What did you choose, and why?
4. What happens on `docker compose up` if `product-service` starts before Postgres is accepting connections, and how does your setup prevent it?
5. The connection string changed from `localhost:5433` to `product-db:5432`. Explain each part of the change.

## Definition of done
- [ ] All acceptance criteria met
- [ ] Questions answered in `notes/m1.md`
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
- 2026-10-18: Updated for the `tinyshop/infrastructure/` layout
- 2026-10-18: Paths updated for the per-service layout (`tinyshop/services/<service>/{src,tests}`)
- 2026-10-25: Started early on the TS-009 branch: multi-stage Dockerfile with a cached restore layer, non-root (`USER $APP_UID`), shared `tinyshop/.dockerignore` (no secrets, no build output, keeps `proto/`), and the `image (product-service)` CI job (AC1-AC4, AC11). Compose service, migrations, health check and startup order remain
- 2026-10-25: AC9 corrected: the negative test must use a network product-service isn't attached to (debug-network), not product-network
- 2026-10-25: Steps 3-5 done: migration bundle (Dockerfile target `migrations`, Compose job `product-db-migrate`), startup chain, readiness health check on an Alpine runtime image; decisions in architecture.md §10. CI image job also builds the migrations target. Verified: 3 fresh-volume startups, idempotent re-run, failed migration blocks the service, unhealthy after DB loss
