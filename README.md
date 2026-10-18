# Learn Observability

A hands-on lab for learning observability, Docker, networking, TLS and distributed systems by building and running **TinyShop**, a small e-commerce system made of several services.

## Repository layout

| Path | Contents |
|---|---|
| [resources/](resources/) | The project brief ([outline.md](resources/outline.md)) and other reference material |
| [tickets/](tickets/) | Jira-style tickets that drive the work. Start at the [board](tickets/README.md) |
| [notes/](notes/) | Learning journal: answers to ticket questions, mistakes and findings |
| [tinyshop/](tinyshop/) | The TinyShop system |
| [tinyshop/docs/](tinyshop/docs/) | Architecture and API contracts |
| [tinyshop/proto/](tinyshop/proto/) | Protobuf (gRPC) contracts |
| [tinyshop/src/](tinyshop/src/) | Source code for the services and the frontend, each with its Dockerfile |
| `tinyshop/tests/` | Test projects (from TS-009) |
| `tinyshop/infrastructure/` | Compose file, `.env.example`, load tests, helper scripts and infrastructure config (from TS-007). Layout: [architecture.md](tinyshop/docs/architecture.md#repository-layout) |
