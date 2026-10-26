---
key: TS-023
title: Containerize the order service
type: Story
status: To Do
priority: High
points: 3
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, docker, compose, networking, grpc, distributed-systems, ci, needs-refinement]
depends_on: [TS-013, TS-021, TS-022]
created: 2026-10-26
---

# TS-023: Containerize the order service

## Summary
Run order-service and its database in Compose, wired to user-service and product-service over `backend-network` and to payment-service over `payment-network`, and repeat the order-flow experiments inside Docker.

## Background
- Split out of TS-013 at the assignee's request; TS-013 delivered the .NET solution.
- Follows decisions D21-D24 and architecture.md: §2 (inventory: `order-db`, `order-db-proxy` on `127.0.0.1:5435`), §3 (order-service on `backend-network`, `order-network` and `payment-network`), §8, §10 (startup chain, health check, database roles).
- order-service's configuration: `ConnectionStrings__OrderDb`, and `Services__UserService__BaseUrl`, `Services__ProductService__BaseUrl`, `Services__PaymentService__BaseUrl` (`http://<service>:8080`; an `http://` address makes the gRPC client use h2c). It fails at startup if any is missing.
- Readiness checks only `order-db`, never the other services (contracts.md §5.4).

## Scope
**In scope**
- `services/order-service/Dockerfile` with `runtime` (Alpine) and `migrations` targets. Like payment-service's, the build context must include `proto/`: Grpc.Tools generates the payment client at build time
- Compose: `order-db` (`tinyshop_orders`, init script with `DB_ROLE_PREFIX=order`, `APP_TABLE_PRIVILEGES=SELECT, INSERT, UPDATE`), `order-db-migrate`, `order-service`, `order-network`; connection strings defined once with anchors
- `order-db-proxy` (debug profile, `127.0.0.1:5435`)
- `.env.example` entries for the three order-db passwords
- CI: order-service in the `image` matrix (with `migrations: true`); its folder in Dependabot's `docker` directories

## Acceptance criteria
- [ ] AC1: `docker compose up` on empty volumes brings up every slice; order-service becomes healthy after its migration job exits 0. From a container on `backend-network`, `POST http://order-service:8080/orders` with `{ "userId": 123, "productId": 2, "quantity": 2 }` returns 201 with a `PAID` order and the Keyboard snapshot.
- [ ] AC2: `order-db` is reachable only by `order-service` and `order-db-migrate`. order-service reaches payment-service; nothing else on `backend-network` does.
- [ ] AC3: **Experiment:** repeat TS-013's AC5 inside Compose (`docker compose stop product-service`, then `payment-service`). Compare the response time and the .NET error (`HttpRequestError`, gRPC status detail) with TS-013's local run, which went through a socat proxy, and explain the difference.
- [ ] AC4: **Experiment:** stop `order-db` and place an order, then check `/health/live` and `/health/ready` and what Docker reports for the container.
- [ ] AC5: CI builds both order-service images; `ci-passed` covers them.

## Technical notes
To be refined before pickup. Most files will be near-copies of user-service's (TS-021), with two differences: the `proto/` copy from payment-service's Dockerfile, and three networks for one service. Points to discuss: `depends_on` for user-, product- and payment-service (order-service starts and is ready without them; should Compose still wait for them to be healthy?), and what a stopped container looks like to a caller on an internal network (DNS failure, not "connection refused").

## Definition of done
- [ ] All acceptance criteria met
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-26: Created, split out of TS-013
