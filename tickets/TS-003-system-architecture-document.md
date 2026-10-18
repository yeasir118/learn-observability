---
key: TS-003
title: System architecture document
type: Task
status: Done
priority: Highest
points: 3
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, docs, docker, networking]
depends_on: [TS-002]
created: 2026-10-18
---

# TS-003: System architecture document

## Summary
Before any code is written, design and document every container, port and network in TinyShop, and how a request travels through them, in `tinyshop/docs/architecture.md`.

## Background
- The outline gives the logical architecture (outline §1, §8–10). This ticket turns it into the physical deployment: real container names, ports and networks.
- Decision D10 in TS-001: each database is reachable only by the service that owns it, and this is enforced by Docker networks.
- Only the frontend publishes a port to the host by default (TS-001 acceptance criteria).

## Scope
**In scope**
- Container inventory
- Network design and a reachability matrix
- A component diagram
- A sequence diagram for `POST /api/orders`
- Naming conventions

**Out of scope**
- Writing `docker-compose.yml` (later tickets implement this design)
- Observability containers (a later epic)
- TLS (epic M1.5)

## Acceptance criteria
- [ ] AC1: A **container inventory** table with one row per container (9 in total). For each: the container/service name (which is also its DNS name), its image or build source, its role, the port it listens on inside the container, its host-published port (or "none"), and the networks it's on.
- [ ] AC2: A **network design** section listing every Docker network, what it's for, and which containers are attached to it.
- [ ] AC3: A **reachability matrix** showing which container can open a connection to which. It must show that:
  - the browser can reach only the frontend
  - Order cannot reach `postgres-users`
  - Gateway cannot reach any database
- [ ] AC4: A **component diagram** in Mermaid (GitHub renders it) showing the containers, networks and protocols (HTTP, gRPC, Postgres).
- [ ] AC5: A **sequence diagram** in Mermaid for `POST /api/orders`, from the browser to the databases and back. It must show both the success path and the payment-declined path.
- [ ] AC6: A **naming conventions** section covering container names, database names (`tinyshop_users`, `tinyshop_products`, `tinyshop_orders`) and environment-variable naming.
- [ ] AC7: A note on **local development**: during step 1 you'll run a service directly on the host against Postgres in Docker. How will the host reach that Postgres, and why doesn't that conflict with "only the frontend publishes ports"?

## Technical notes
- In a Compose project, the service name is the DNS name other containers use to reach it. Choose names you'll want to type a hundred times.
- From .NET 8 onwards, ASP.NET Core container images listen on port **8080** by default, not 80. Decide whether to keep the default or override it, and say why.
- A container can be attached to several networks. Work out which containers need more than one network.
- Look up what Compose's `internal: true` network option does and whether any of your networks should use it.
- Keep it simple: plain tables and two diagrams. Don't polish it.

## Learning objectives
- The difference between a logical architecture (which components exist and who calls whom) and a physical one (containers, ports and networks).
- How Docker's networking provides DNS-based service discovery and isolation between networks.
- Why "the database belongs to one service" can be enforced by the infrastructure, not just by convention.

## Questions to answer
1. What is the difference between a container's listening port, `expose`, and `ports` in Compose? Which of them actually affects whether another container can connect?
2. Inside a container, which DNS server answers a lookup for `product-service`? How would you check that from a shell?
3. Why must the browser never be given internal service names such as `http://gateway:8080`?
4. What does `internal: true` on a Docker network prevent, and what does it not prevent?
5. If two containers are on different networks, but one of them publishes a port on the host, can the other one reach it? How?

## Definition of done
- [ ] All acceptance criteria met
- [ ] Questions answered in `notes/m1.md`
- [ ] Reviewed and approved
- [ ] Merged into `master`, branch deleted, board updated

## Activity log
- 2026-10-18: Created
- 2026-10-18: Done, merged into master
