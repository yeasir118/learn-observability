# TinyShop Observability Lab

## Overview

This project is a hands-on introduction to **observability in distributed systems**.

The objective is not to build a production-ready e-commerce system. The objective is to build a small distributed application, deliberately introduce failures and performance problems, and then learn how **logs, metrics, traces, and correlation** allow engineers to understand what is happening inside a running system.

The project should be implemented using entirely open-source technologies.

### Required technologies

- .NET / ASP.NET Core
- C#
- PostgreSQL
- React
- HTTP/REST
- gRPC
- Docker / Docker Compose
- OpenTelemetry
- Prometheus
- Grafana
- Loki
- Tempo

### Primary learning objective

By the end of the project, you should be able to answer:

> How can I investigate an unknown problem in a distributed application without relying primarily on reading the source code?

The project should progressively demonstrate why observability is necessary.

---

# 1. System to Build

Build a small e-commerce application called **TinyShop**.

The system should contain:

```text
                         ┌───────────────┐
                         │    React      │
                         │   Frontend    │
                         └───────┬───────┘
                                 │
                              HTTP
                                 │
                                 ▼
                         ┌───────────────┐
                         │ API Gateway   │
                         │ ASP.NET Core  │
                         └───────┬───────┘
                                 │
                 ┌───────────────┼───────────────┐
                 │               │               │
                HTTP            HTTP            HTTP
                 │               │               │
                 ▼               ▼               ▼
          ┌────────────┐  ┌────────────┐  ┌────────────┐
          │  Product   │  │   Order    │  │    User    │
          │  Service   │  │  Service   │  │  Service   │
          └─────┬──────┘  └─────┬──────┘  └─────┬──────┘
                │               │               │
                │              gRPC             │
                │               │               │
                │               ▼               │
                │       ┌──────────────┐        │
                │       │   Payment    │        │
                │       │   Service    │        │
                │       └──────┬───────┘        │
                │              │                │
                ▼              ▼                ▼
          ┌──────────┐   ┌──────────┐    ┌──────────┐
          │PostgreSQL│   │PostgreSQL│    │PostgreSQL│
          │ Products │   │  Orders  │    │  Users   │
          └──────────┘   └──────────┘    └──────────┘
```

The services should be intentionally small.

Do not implement authentication, payment processing, complex business rules, message queues, caching, Kubernetes, or other production concerns.

The complexity should come from communication between components, not from business logic.

# 2. Services

## 2.1 React Frontend

The frontend should provide a minimal interface.

Required functionality:

- Display products.
- Select a product.
- Enter a user ID.
- Create an order.
- Display the result of the order operation.

Example:

```text
TinyShop

Products
--------------------------------
Laptop       $1000     [Buy]
Keyboard      $100     [Buy]
Mouse          $50     [Buy]

User ID: [ 123 ]

             [ Create Order ]
```

The frontend communicates only with the API Gateway.

It should not communicate directly with the internal services.

# 3. API Gateway

**Technology:**

- ASP.NET Core
- HTTP/REST

The gateway is the public entry point.

Example endpoints:

- `GET /api/products`
- `POST /api/orders`
- `GET /api/orders/{id}`

The gateway should forward requests to internal services.

For example:

```text
Browser
   │
   │ POST /api/orders
   ▼
Gateway
   │
   │ HTTP
   ▼
Order Service
```

The gateway should not contain significant business logic.

# 4. Product Service

**Technology:**

- ASP.NET Core
- HTTP/REST
- PostgreSQL

**Endpoints:**

- `GET /products`
- `GET /products/{id}`

Example response:

```json
{
  "id": 1,
  "name": "Keyboard",
  "price": 100.00
}
```

Seed the database with approximately 5 products.

# 5. User Service

**Technology:**

- ASP.NET Core
- HTTP/REST
- PostgreSQL

**Endpoint:**

- `GET /users/{id}`

Example:

```json
{
  "id": 123,
  "name": "Alice"
}
```

Seed the database with several users.

# 6. Order Service

**Technology:**

- ASP.NET Core
- HTTP/REST
- PostgreSQL

**Endpoints:**

- `POST /orders`
- `GET /orders/{id}`

Creating an order should require:

- `user_id`
- `product_id`
- `quantity`

Example:

```json
{
  "userId": 123,
  "productId": 1,
  "quantity": 2
}
```

The Order Service should communicate with other services.

For example:

```text
                    Order Service
                         │
             ┌───────────┼───────────┐
             │           │           │
            HTTP        HTTP        gRPC
             │           │           │
             ▼           ▼           ▼
        User Service  Product     Payment
                       Service     Service
```

The order creation flow should be approximately:

1. Validate user
2. Retrieve product
3. Calculate total
4. Create order
5. Request payment
6. Update order status
7. Return result

The exact implementation can be simplified.

# 7. Payment Service

**Technology:**

- ASP.NET Core
- gRPC

The Payment Service should expose a gRPC endpoint such as:

`ProcessPayment`

**Request:**

- `order_id`
- `user_id`
- `amount`

**Response:**

- `success`
- `transaction_id`

The payment service does not need a real database.

It should simply simulate payment processing.

For example:

```text
ProcessPayment()
    ↓
Wait 50ms
    ↓
Return success
```

Later, this service will become one of the primary sources of deliberately introduced failures.

# 8. Communication Requirements

The project must demonstrate both HTTP and gRPC communication.

Use HTTP for:

- Gateway → Order Service
- Gateway → Product Service
- Gateway → User Service
- Order Service → User Service
- Order Service → Product Service

Use gRPC for:

- Order Service → Payment Service

This is intentional.

One of the learning objectives is to understand how observability works across different communication protocols.

# 9. Database Requirements

Use PostgreSQL.

There should be three logical databases:

- `tinyshop_users`
- `tinyshop_products`
- `tinyshop_orders`

Each service should own its database.

Do not create a shared database used by all services.

The objective is to simulate a basic microservice architecture:

```text
User Service
    ↓
Users Database

Product Service
    ↓
Products Database

Order Service
    ↓
Orders Database
```

The database schemas can remain extremely small.

# 10. Docker Compose

The complete system should be runnable with Docker Compose.

The initial system should contain approximately:

- frontend
- gateway
- order-service
- product-service
- user-service
- payment-service
- postgres-users
- postgres-products
- postgres-orders

Later add:

- otel-collector
- prometheus
- loki
- tempo
- grafana

The goal is to make the entire learning environment reproducible.

# 11. Phase 1 — Build Without Observability

Do not install OpenTelemetry yet.

The first version of the application should have only basic application logging.

For example:

```text
Creating order
Fetching user
Fetching product
Processing payment
Order created
```

Do not spend significant time designing the logs.

The purpose of this phase is to establish a baseline.

# 12. Failure Exercise 1 — Application Failure

Introduce an intentional failure into the Product Service.

For example:

`GET /products/1`

should sometimes fail with:

`500 Internal Server Error`

Then attempt to answer:

- Which service failed?
- Why did the request fail?
- Which request caused the failure?
- Did the gateway fail or did Product Service fail?
- How would this be investigated if the application were running on another machine and you had no debugger attached?

Write down the answers.

This is an important part of the assignment.

# 13. Phase 2 — Structured Logging

Introduce structured logging.

Logs should contain useful fields such as:

```json
{
  "timestamp": "...",
  "level": "Information",
  "service": "order-service",
  "message": "Creating order",
  "user_id": "123",
  "product_id": "1"
}
```

Errors should contain additional context:

```json
{
  "timestamp": "...",
  "level": "Error",
  "service": "payment-service",
  "message": "Payment failed",
  "order_id": "1001",
  "error": "Payment timeout"
}
```

The objective is to understand the difference between:

- unstructured text

and:

- structured events

Do not focus on building a sophisticated logging framework.

Focus on answering:

> What information would I want if I were investigating this event six hours later?

# 14. Phase 3 — Correlation ID

Introduce a request/correlation ID.

A request entering the gateway should receive an identifier:

`X-Request-ID: 7f3a1c`

The identifier should travel through the system.

For example:

```text
Browser
   │
   │ request-id = 7f3a1c
   ▼
Gateway
   │
   │ request-id = 7f3a1c
   ▼
Order Service
   │
   ├── request-id = 7f3a1c → User Service
   │
   ├── request-id = 7f3a1c → Product Service
   │
   └── request-id = 7f3a1c → Payment Service
```

Every log related to that request should contain:

`request_id = 7f3a1c`

Then deliberately cause an order to fail.

Try to reconstruct the entire request by searching for its ID.

You should be able to see something similar to:

```text
Gateway        request received
Order Service  order creation started
User Service   user retrieved
Product        product retrieved
Payment        payment started
Payment        payment failed
Order Service  order failed
Gateway        response 500
```

**Learning objective**

Understand why correlation is necessary in distributed systems.

# 15. Phase 4 — OpenTelemetry

Now introduce OpenTelemetry.

The application should send telemetry through an OpenTelemetry Collector.

The architecture should become:

```text
Applications
     │
     │ OTLP
     ▼
┌─────────────────────┐
│ OpenTelemetry       │
│ Collector           │
└──────────┬──────────┘
           │
     ┌─────┼─────┐
     │     │     │
     ▼     ▼     ▼
   Loki  Tempo  Prometheus
```

At this stage, do not try to understand every OpenTelemetry configuration option.

Focus on the concepts:

```text
Instrumentation
       ↓
Telemetry
       ↓
Collector
       ↓
Backend
```

# 16. Phase 5 — Distributed Tracing

Enable OpenTelemetry tracing.

A successful order should produce a trace resembling:

```text
Trace: abc123

Gateway
│
└── Order Service
    │
    ├── User Service
    │
    ├── Product Service
    │
    └── Payment Service
        │
        └── Database / internal operation
```

Inspect the trace in Grafana/Tempo.

Understand:

- Trace
- Span
- Parent span
- Child span
- Span duration
- Service name
- Attributes
- Status
- Trace ID
- Span ID

Do not memorize definitions.

Use the trace viewer until the relationship becomes intuitive.

# 17. Failure Exercise 2 — Slow Payment

Modify the Payment Service:

```text
ProcessPayment()
    ↓
Wait 3 seconds
    ↓
Return success
```

Create an order.

Observe the trace.

Your trace should reveal something conceptually similar to:

```text
Gateway                  ───────────── 3.1s
  Order Service          ───────────── 3.0s
    User Service         ── 20ms
    Product Service      ── 30ms
    Payment Service      ──────────── 2.9s
```

Answer:

- Which service caused the latency?
- How much time did each service consume?
- Was the gateway itself slow?
- Would application logs alone make this as obvious?

# 18. Failure Exercise 3 — Slow Database

Now remove the delay from Payment Service.

Instead, introduce a delay around a database operation in one of the services.

For example:

```text
Database query
      ↓
Wait 2 seconds
      ↓
Return result
```

Generate requests.

Use tracing to determine:

- Which service is slow?
- Which operation is slow?
- Is the network call slow?
- Is the database operation slow?

This exercise is intended to demonstrate why tracing is more useful than simply recording:

`GET /products took 2 seconds`

# 19. Phase 6 — Metrics

Introduce metrics.

Start with only three categories.

## Request count

Measure how many requests each service receives.

Conceptually:

`http_requests_total`

## Request duration

Measure request latency.

Conceptually:

`http_request_duration`

## Errors

Measure failed requests.

Conceptually:

`http_request_errors_total`

Do not attempt to create dozens of metrics.

The objective is to understand what metrics represent.

# 20. Prometheus

Send metrics to Prometheus.

Use Prometheus to investigate questions such as:

- How many requests does Order Service receive?
- What is the error rate?
- Which service receives the most traffic?
- What is the average request duration?
- Is latency increasing over time?

Understand the difference between:

- one request

and:

- the behavior of the system over time

This distinction is fundamental.

# 21. Failure Exercise 4 — Error Rate

Modify Payment Service so that approximately 20% of payment attempts fail.

Generate traffic.

Observe the metrics.

You should be able to determine that:

```text
Payment Service
    ↓
Error rate increased
```

Then use traces to investigate individual failed requests.

Then use logs to understand the specific failure.

This should demonstrate the relationship:

```text
Metrics
   ↓
Something is wrong
   ↓
Traces
   ↓
Where is it wrong?
   ↓
Logs
   ↓
What exactly happened?
```

This is a simplified model, but it is useful for learning.

# 22. Phase 7 — Loki

Send application logs to Loki.

Use Grafana to search logs.

Your logs should contain fields such as:

- `service`
- `level`
- `timestamp`
- `trace_id`
- `span_id`
- `request_id`

Search for:

```text
service = payment-service
level = error
```

Then investigate a specific failed request.

Find its:

`trace_id`

Use that trace ID to find the corresponding trace.

# 23. The Important Exercise — Logs + Traces

Create one failed order.

Start with only the log:

`Payment failed`

Determine:

- What order failed?
- Which user was involved?
- Which request caused it?
- What was the complete request path?
- How long did each service take?
- Which operation failed?
- What was the underlying error?

Use:

```text
Loki
 ↓
Trace ID
 ↓
Tempo
 ↓
Trace
 ↓
Problematic span
 ↓
Logs
```

The objective is to experience telemetry correlation, not merely configure tools.

# 24. Phase 8 — Grafana Dashboard

Create a simple dashboard.

Do not attempt to create a beautiful production dashboard.

Create approximately four panels:

```text
┌──────────────────────┬──────────────────────┐
│ Requests / second    │ Error rate           │
│                      │                      │
├──────────────────────┼──────────────────────┤
│ Request latency      │ Requests by service  │
│                      │                      │
└──────────────────────┴──────────────────────┘
```

The dashboard should help answer:

- Is the system currently healthy?
- Which service is receiving traffic?
- Are errors increasing?
- Is latency increasing?

# 25. Final Investigation Exercises

At the end of the project, stop looking at the source code.

Pretend you are an engineer investigating an unknown production incident.

## Incident A — Checkout is Slow

You receive:

> Users report that creating an order sometimes takes several seconds.

You do not know:

- Which service is slow.
- Whether the problem is consistent.
- Whether the database is involved.
- Whether the gateway is responsible.

Use your observability tools to investigate.

Write down:

1. What did the metrics tell you?
2. What did the traces tell you?
3. What did the logs tell you?
4. What was the root cause?

## Incident B — Orders Are Failing

You receive:

> Approximately 10–20% of orders are failing.

Investigate without reading the source code first.

Determine:

- Which service is responsible?
- What operation is failing?
- Is the failure isolated to one endpoint?
- Is the problem affecting all requests?
- Can you identify a specific failed request?
- Can you trace that request across services?

## Incident C — One User Reports a Failure

You receive:

```text
User ID: 123
Approximate time: 14:30
Problem: Order failed
```

Try to find the exact request.

Then reconstruct:

```text
Browser
   ↓
Gateway
   ↓
Order Service
   ↓
User Service
   ↓
Product Service
   ↓
Payment Service
```

Determine where the failure occurred.

This exercise demonstrates why high-cardinality identifiers and correlation can be extremely valuable.

# 26. Required Concepts to Understand

At the end of the project, you should be able to explain in your own words:

## Observability

- What does observability mean?
- Why is it different from simply having logs?

## Logs

- What is a log?
- What makes a log useful?
- Why are structured logs preferable to arbitrary strings?

## Metrics

- What is a metric?
- What questions are metrics good at answering?
- Why can't metrics alone explain every failure?

## Traces

- What is a trace?
- What is a span?
- How are spans related?
- Why are traces particularly useful for microservices?

## Correlation

- What is a trace ID?
- What is a span ID?
- What is a request/correlation ID?
- Why should telemetry from different services be correlated?

## OpenTelemetry

- What problem does OpenTelemetry solve?
- What is instrumentation?
- What does the OpenTelemetry Collector do?
- Why is the Collector separate from the telemetry backend?

## Prometheus

- What does Prometheus store?
- What kind of questions should be answered using metrics?

## Loki

- What does Loki provide?
- How is searching logs different from looking at traces?

## Tempo

- What does Tempo provide?
- Why do we need a tracing backend?

## Grafana

- What role does Grafana play?
- Is Grafana itself the observability system?
- How does it relate to Loki, Prometheus and Tempo?

# 27. Final Architecture

At the end of the exercise, your system should conceptually look like:

```text
                         ┌───────────────┐
                         │    React      │
                         └───────┬───────┘
                                 │
                                 ▼
                         ┌───────────────┐
                         │ API Gateway   │
                         └───────┬───────┘
                                 │
                                 ▼
                         ┌───────────────┐
                         │ Order Service │
                         └───┬─────┬─────┘
                             │     │
                    ┌────────┘     └─────────┐
                    ▼                        ▼
              User Service             Product Service
                    │                        │
                    ▼                        ▼
                 Postgres                 Postgres

                         Order Service
                              │
                             gRPC
                              │
                              ▼
                       Payment Service


                    ┌────────────────────┐
                    │ OpenTelemetry      │
                    │ Instrumentation    │
                    └─────────┬──────────┘
                              │
                             OTLP
                              │
                              ▼
                    ┌────────────────────┐
                    │ OpenTelemetry      │
                    │ Collector          │
                    └──────┬───┬───┬─────┘
                           │   │   │
                 ┌─────────┘   │   └─────────┐
                 ▼             ▼             ▼
              ┌─────┐     ┌──────────┐    ┌──────┐
              │Loki │     │Prometheus│    │Tempo │
              └──┬──┘     └────┬─────┘    └──┬───┘
                 │             │              │
                 └─────────────┼──────────────┘
                               ▼
                         ┌──────────┐
                         │ Grafana  │
                         └──────────┘
```

# 28. Suggested Repository Structure

A possible repository structure:

```text
tinyshop-observability/
│
├── src/
│   ├── frontend/
│   ├── gateway/
│   ├── order-service/
│   ├── product-service/
│   ├── user-service/
│   └── payment-service/
│
├── proto/
│   └── payment.proto
│
├── infrastructure/
│   ├── postgres/
│   └── observability/
│       ├── otel-collector/
│       ├── prometheus/
│       ├── loki/
│       ├── tempo/
│       └── grafana/
│
├── docker-compose.yml
│
└── README.md
```

The exact structure is not important.

Keep the repository easy to navigate.

# 29. Constraints

To preserve the learning objective:

- Do not use Kubernetes.
- Do not use cloud observability services.
- Do not use commercial observability platforms.
- Do not introduce Kafka or RabbitMQ.
- Do not introduce Redis.
- Do not build authentication.
- Do not spend time on UI design.
- Do not implement complex business logic.
- Do not attempt production-grade high availability.
- Do not spend excessive time tuning databases.
- Do not blindly copy an observability stack without understanding each component.

The system should remain small enough that you can understand the entire request path.

# 30. Definition of Done

The project is complete when you can demonstrate all of the following.

## Application

- [ ] React frontend works.
- [ ] API Gateway works.
- [ ] Product Service works.
- [ ] User Service works.
- [ ] Order Service works.
- [ ] Payment Service works.
- [ ] PostgreSQL is used by User, Product and Order services.
- [ ] HTTP communication exists between services.
- [ ] gRPC communication exists between Order and Payment services.
- [ ] Everything runs using Docker Compose.

## Logging

- [ ] Services produce structured logs.
- [ ] Logs contain service identity.
- [ ] Logs contain useful contextual information.
- [ ] Requests can be correlated.

## Tracing

- [ ] OpenTelemetry instrumentation is enabled.
- [ ] Traces cross service boundaries.
- [ ] HTTP calls appear in traces.
- [ ] gRPC calls appear in traces.
- [ ] Individual spans can be inspected.
- [ ] Trace IDs can be correlated with logs.

## Metrics

- [ ] Request volume is measured.
- [ ] Request latency is measured.
- [ ] Errors are measured.
- [ ] Prometheus receives metrics.

## Visualization

- [ ] Loki contains application logs.
- [ ] Tempo contains traces.
- [ ] Prometheus contains metrics.
- [ ] Grafana can query all three.
- [ ] A basic Grafana dashboard exists.

## Investigation

- [ ] A slow request can be investigated.
- [ ] A failed request can be investigated.
- [ ] A service causing latency can be identified.
- [ ] A specific failed request can be traced across services.
- [ ] Logs, metrics and traces can be used together.

# 31. Final Reflection

After completing the exercises, write a short document answering these questions.

## Question 1

Before adding observability, what information was difficult or impossible to obtain?

## Question 2

What questions are easiest to answer with metrics?

## Question 3

What questions are easiest to answer with traces?

## Question 4

What questions are easiest to answer with logs?

## Question 5

Why is correlation between logs and traces valuable?

## Question 6

What does the OpenTelemetry Collector actually add to the architecture?

## Question 7

If you could keep only one of logs, metrics, or traces, what would you lose?

## Question 8

What changed in your debugging workflow after adding observability?

## Question 9

Imagine this application had 100 services instead of 5.

Which parts of your current approach would stop scaling?

## Question 10

What information would you want your system to expose if you knew absolutely nothing about the next incident?

## Core Principle

Do not judge the success of this project by how sophisticated the final Grafana dashboard looks.

Judge it by whether you have developed this ability:

```text
Unknown problem
      │
      ▼
Observe system behavior
      │
      ▼
Form a hypothesis
      │
      ▼
Use telemetry to investigate
      │
      ▼
Locate the problematic component
      │
      ▼
Inspect the relevant request/operation
      │
      ▼
Determine the cause
      │
      ▼
Verify the hypothesis
```

That investigative loop is the real subject of this project.

The technologies are simply the tools used to learn it.
