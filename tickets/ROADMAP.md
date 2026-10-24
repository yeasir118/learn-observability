# Roadmap

Where TinyShop is going, at the level of epics. Individual tickets live on the [board](README.md).

- **Current trajectory**: the planned path, taken in order.
- **Expansion ideas**: deliberately unscheduled. They're parked here so they aren't forgotten, and can be picked up either after the current trajectory or as a planned detour from it.
- **More ideas to look at**: further unscheduled ideas suggested by Claude, kept in their own section at the end.

## Current trajectory

| Milestone | Epic | Scope | Status |
|---|---|---|---|
| M1 | [TS-001](TS-001-epic-m1-naked-system.md) | The complete system without observability: services, databases, networks, CI, Failure Exercise 1 | In progress |
| M1.5 | not yet created | TLS: an internal certificate authority, HTTPS at the edge, TLS and mutual TLS between services, TLS to Postgres (decision D13) | Planned |
| M2 onwards | not yet created | The observability phases from [outline.md](../resources/outline.md) §13–§31: structured logging, correlation IDs, OpenTelemetry, tracing, metrics, Loki, dashboards, the failure exercises and the blind incidents | Planned |

## Expansion ideas

Not scheduled and not committed to. Each entry records what it would teach, how it would fit TinyShop specifically, and what it depends on, so turning one into an epic is mostly a matter of refining it.

**Note on the outline's constraints:** [outline.md](../resources/outline.md) §29 rules out authentication, Kafka and similar tools *for the observability lab*, to keep that lab focused. These expansions are separate epics, so they relax those rules on purpose. Each one should still keep its scope small enough to understand end to end.

**Order:** finishing at least the observability milestones first pays off for every idea below. A Kafka pipeline or an auth server is much easier to understand, and to debug, when you can trace a request across it.

### Primary

#### A. Centralized authentication and authorization
- **Learn:** OAuth 2.0 and OpenID Connect flows, JWT access tokens and their validation, token lifetimes and refresh, service-to-service authentication (the client-credentials flow), scopes and roles, where authorization decisions belong.
- **In TinyShop:**
  - an identity provider container (Keycloak, open source) on its own network
  - the frontend signs users in, and the gateway validates tokens at the edge
  - services check scopes; order-service calls other services with its own client credentials
  - the user ID in an order comes from the token instead of the request body
  - this also closes the "anyone can enumerate orders" gap in [contracts.md §8](../tinyshop/docs/contracts.md#8-known-gaps)
- **Depends on:** M1, and ideally M1.5, since tokens must never travel over plain HTTP outside a lab.
- **Size:** one epic, about 6–8 tickets.

#### B. Analytics: OLAP database and data warehousing
- **Learn:** row stores (OLTP) against column stores (OLAP), why analytical queries are slow on Postgres, columnar storage and compression, star schemas (facts and dimensions), batch loading against continuous loading, materialized views.
- **In TinyShop:**
  - ClickHouse as the analytics store, with a sales warehouse: an orders fact table, plus product, user and date dimensions
  - questions like "revenue per product per day", or "payment failure rate by hour", answered in milliseconds over millions of generated orders (TS-017's load generator can produce the volume)
  - optionally, dbt for the transformations between raw and modelled data
- **Depends on:** M1. It pairs naturally with C, which is how data gets into the warehouse continuously.
- **Size:** one epic. The batch-loading version is smaller than the streaming one.

#### C. Batch and stream processing
- **Learn:** event logs and topics, partitions and consumer groups, delivery guarantees (at-most-once, at-least-once, exactly-once), change data capture (CDC), the transactional outbox, idempotent consumers, windowed aggregations, backpressure, and batch against streaming trade-offs.
- **In TinyShop:**
  - **Streaming:** order-service publishes `OrderCreated` and `OrderPaid` events through an outbox table, to Kafka (or Redpanda, which is Kafka-compatible and lighter). Alternatively, Debezium captures changes straight from Postgres (CDC). Consumers feed ClickHouse (idea B) and compute live metrics, such as orders per minute per product.
  - **Batch:** a nightly job that rebuilds aggregates from the full history, then compared with the streaming numbers.
  - **This also fixes real gaps:** the outbox pattern is the standard fix for "payment succeeded but the status update failed" ([contracts.md §8](../tinyshop/docs/contracts.md#8-known-gaps)), and idempotent consumers are the fix for duplicate orders.
- **Depends on:** M1 and the M2 tracing work. Following one event across producer, broker and consumer is the distributed-tracing exercise all over again, only asynchronous.
- **Size:** the largest idea: one epic, or two (streaming first, then batch and comparison).

### Secondary

#### D. Graph database
- **Learn:** property graphs, graph traversals against SQL joins, the Cypher query language, when a graph model fits and when it doesn't.
- **In TinyShop:** a recommendation graph (users → bought → products) built from order events, answering "customers who bought this also bought…" with a new endpoint on product-service. Neo4j Community, or Apache AGE, which adds graph queries to Postgres itself.
- **Depends on:** C helps, because order events are the natural feed into the graph.
- **Size:** a small epic.

#### E. Machine-learning models
- **Learn:** the difference between training and serving, packaging a model (ONNX), serving it in .NET (ONNX Runtime) or as a separate Python service, model versioning, feature data, and monitoring a model's predictions in production.
- **In TinyShop:**
  - a fraud-risk score for payments, so payment-service gets a real reason to decline
  - or product recommendations to complement D
  - training uses the order history in ClickHouse (B), and serving adds a step to the payment flow
  - since TinyShop will be observable by then, a model's latency and decisions show up in traces and metrics like everything else
- **Depends on:** B for the training data. D is optional.
- **Size:** a small to medium epic.

## Turning an idea into an epic
1. Create an epic ticket from [_TEMPLATE.md](_TEMPLATE.md) (`type: Epic`), using the idea's entry above as its background.
2. Refine it together: confirm the scope, the tools and the order of child tickets, and record the decisions in the epic, the way TS-001 does.
3. Add it to the board and to the "Current trajectory" table above, and remove it from the expansion list.

## More ideas to look at (suggested by Claude)

Kept separate from the expansion ideas above, which came from the project owner. These were suggested by Claude on 2026-10-25, picked for industry demand, for fit with the main learning focus (Docker, networking, TLS, distributed systems), and for having a *real* problem to solve in TinyShop rather than a toy one. Each one attaches to something that already exists in the project. Like everything else in this document, they're unscheduled.

### Top recommendations

**1. Kubernetes and GitOps.** The biggest gap, given the demand for it.
- Move TinyShop from Compose to a local cluster (kind or k3d), with Helm or Kustomize, then deploy it with Argo CD (GitOps).
- **The fit is unusually good:**
  - The liveness/readiness split, health endpoints, non-root images and one-off migration jobs map directly onto Kubernetes probes, `securityContext` and Jobs.
  - **NetworkPolicies can express direction**, which Docker networks can't. They would fix the "flat backend zone" trade-off recorded in [architecture.md §3](../tinyshop/docs/architecture.md#3-networks).
  - It also gives TinyShop a real CD pipeline, which is currently out of scope because there's nowhere to deploy to.
- The outline excludes Kubernetes, but only for the observability lab.

**2. Resilience and chaos engineering.** Timeouts, retries with jitter, circuit breakers and bulkheads, using `Microsoft.Extensions.Resilience` (Polly), plus Toxiproxy to inject latency and network partitions *between* containers.
- **This one has a concrete payoff:** [contracts.md §7 and §8](../tinyshop/docs/contracts.md#7-default-timeouts-in-m1) already list exactly the problems these patterns fix:
  - outer timeouts shorter than inner ones
  - no cancellation
  - orders stuck in `PENDING`
  - duplicate orders (fixed with idempotency keys)
- After the observability phases, each pattern can be *seen* working in traces.

**3. Sagas and durable workflows.** The order flow is a distributed transaction without a transaction. Implement it as a saga with compensating steps: if the payment times out, reconcile or refund. Do it first by hand, then with **Temporal**, which is open source and in high demand. It answers "what happens to the `PENDING` orders", the most important gap in the system.

**4. Supply-chain security and secrets.** Mostly small additions to CI, and very in demand:
- generate an SBOM (Syft) for each image, and scan it for known vulnerabilities (Trivy)
- sign images (Cosign)
- code scanning (CodeQL)
- secrets in **Vault (OpenBao)**, with *dynamic* database credentials: short-lived users created on demand

The Vault part closes the least-privilege gap from TS-010: give the migration job a user that can change the schema, and the service a read/write-only one.

**5. SLOs, alerting and profiling.** This completes observability; the outline stops before it.
- **SLOs and error budgets:** turn "99.5% of orders succeed within 1 second" into Prometheus rules and burn-rate alerts (Alertmanager), plus written runbooks.
- **Continuous profiling with Grafana Pyroscope**, often called the fourth pillar of observability. It fits into the Grafana stack the observability phases build.

Alerting and on-call skills are what companies actually hire SREs and platform engineers for.

### Also strong

| Topic | TinyShop angle | Why |
|---|---|---|
| **Postgres operations** | A read replica, failover (Patroni), connection pooling (PgBouncer), backups with point-in-time recovery (pgBackRest), plus a restore drill | Database reliability: few developers practise this, and it matters a lot |
| **Service mesh** (Linkerd) | Automatic mutual TLS and retries, compared side by side with the TLS set up by hand in M1.5 | You learn the most when you've done it yourself first |
| **Contract testing** (Pact) | Order-service checks its expectations of product-service and user-service in CI, without running them | Tests that the contracts in contracts.md hold *between* services |
| **Caching** (Redis or Valkey) | Cache-aside for products: invalidation, thundering herds, staleness | A classic distributed-systems topic, interesting to watch through tracing |
| **Search** (OpenSearch) | Product search kept in sync by change data capture | Pairs with idea C above |
| **Real-time updates** (SignalR or Server-Sent Events) | Order status pushed to the browser when a payment completes | Pairs with async payments in a saga |

### AI-related, given the current demand
- **Semantic search with pgvector:** product embeddings stored in Postgres, giving "find something like a keyboard but quieter". You learn embeddings and vector indexes with no new database.
- **Observability for LLM features:** OpenTelemetry now has standard conventions for AI calls (model, token counts, latency). An LLM feature would be traced like everything else. That's where observability work is heading.

These fit next to idea E above (ML models).

### Probably skip
- **Cloud deployment** (AWS or Azure): valuable, but it costs money, and the outline deliberately avoids it. The Kubernetes plus GitOps work teaches most of the same skills locally.
- **GraphQL / backend-for-frontend:** in moderate demand, but it adds little to the distributed-systems focus.
- **A Backstage-style developer portal:** too heavy for one person. A `dotnet new` template for the service layout, though, would genuinely help, since four more services will be built from the same pattern.

### A sensible order, if you did all of it
```text
current trajectory (M1 → M1.5 → observability)
  → resilience and chaos (uses the observability immediately)
  → sagas / Temporal  ↔  streaming (idea C above)
  → Kubernetes and GitOps (+ NetworkPolicies, + a service mesh)
  → SLOs and alerting, supply chain and secrets (fit in anywhere)
```
