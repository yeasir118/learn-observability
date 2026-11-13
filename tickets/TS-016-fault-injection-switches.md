---
key: TS-016
title: Fault injection switches
type: Story
status: In Progress
priority: Medium
points: 5
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, chaos, configuration, dotnet, ef-core, grpc]
depends_on: [TS-015]
created: 2026-10-18
---

# TS-016: Fault injection switches

## Summary
As the person running the lab exercises, I can turn specific failures on and off through environment variables, without rebuilding any image, so that each exercise can reproduce a realistic problem. Done together: Claude builds the plumbing and the application faults; the assignee drafts the database-delay interceptor, which Claude completes and reviews.

## Background
- **Decision D11:** all faults are off by default and controlled through environment variables.
- **What the exercises need** (outline §12, §17, §18, §21, §25):

  | Exercise | Fault | Where |
  |---|---|---|
  | 1: application failure (TS-019) | some `GET /products/{id}` fail with 500 | product-service |
  | 2: slow payment | a payment takes about 3 s | payment-service |
  | 3: slow database | a query takes about 2 s | any service with a database |
  | 4: error rate; Incident B | 10-20 % of payments fail | payment-service |
  | Incident C: one user's order fails | a fault for one user only | payment-service (its request carries `user_id`) |

- **What's there already:** payment-service waits `Payment:ProcessingDelay` (50 ms, TS-012), the simulated provider's normal latency, and always approves. order-service maps a declined payment to an order `FAILED` (still 201), and a gRPC failure to 502 `payment_unavailable` (contracts.md §2.3, §6).
- **Later:** the blind incidents (outline §25) may need faults that leave no hint in the logs. Not here (decision 8).

## Scope
**In scope**
- A `Faults` configuration section in product-, user-, order- and payment-service, validated at startup
- product-service: a share of requests fails with 500, optionally only for one product ID
- payment-service: an added delay, a share of declines, a share of technical errors, each optionally for one user ID
- product-, user- and order-service: a delay **inside** every database command, in the shared `TinyShop.ServiceDefaults.Data`
- Compose `environment` entries and `.env.example` entries, all off by default
- `tinyshop/docs/faults.md`: every switch, its effect, what the browser sees, and an example per exercise
- Tests for every fault, on and off

**Out of scope**
- Changing faults without recreating the container (decision 2)
- Faults in the gateway, nginx or the databases themselves (`docker pause`/`stop` already covers "gone" and "frozen", TS-015)
- Faults that hide themselves from the logs (decision 8)
- Retries, timeouts, circuit breakers: there's nothing to protect against yet (D5)

## Decisions (made in refinement, 2026-11-09)
| # | Decision | Why |
|---|---|---|
| 1 | **One section, `Faults`, in each service that has faults**, bound to options classes and validated with `ValidateOnStart`: rates between 0 and 1, delays between 0 and 60 s (the database delay: 25 s, see the activity log), IDs positive. Compose passes `Faults__<Name>: ${<SERVICE>_FAULT_<NAME>:-<off>}`; `.env` names follow architecture.md §7 (UPPER_SNAKE, prefixed with the container). | One place to look per service. A typo stops the service at startup instead of silently doing nothing. |
| 2 | **`IOptions`, not `IOptionsMonitor`.** | A container's environment can't change while it runs: editing `.env` and running `docker compose up -d` recreates the container, which takes seconds. Switching without a restart would need a mounted file or an admin endpoint. |
| 3 | **Each fault is injected where the real failure would happen**, so the traces of M3 show it in the right place. | A fault in the wrong layer teaches the wrong lesson: a "slow database" made with `Task.Delay` in C# would show up as slow application code (AC3). |
| 4 | **product-service: `Faults:ErrorRate` (0-1) and `Faults:ProductId` (optional).** An MVC action filter (`Api/Faults/ProductFaultFilter`) throws an `InjectedFaultException` for a matching request; the existing handler turns it into 500 `internal_error`. With `ProductId`, only `GET /products/{that id}` fails; without it, every `/products` request can. | A real 500 is an unhandled exception: logged as an error with a stack trace. That's what Exercise 1 investigates. A returned 500 would leave no trace in the logs. A filter rather than a middleware (changed in implementation): it only ever sees controller actions, so health endpoints and unknown routes are excluded by construction, it runs after model validation (a 400 stays a 400), and it gets the bound `id` without parsing the path. |
| 5 | **payment-service: `Faults:Delay`, `Faults:DeclineRate`, `Faults:ErrorRate` (0-1 each) and `Faults:UserId` (optional), all in `PaymentProcessor`.** `Delay` is added to `ProcessingDelay`. A decline is a normal response (`success = false`, `failure_reason = "card_declined"`). A technical error throws an `InjectedFaultException`, which the `ExceptionInterceptor` logs with its stack trace and answers with `INTERNAL`; order-service maps that to 502 `payment_unavailable` (changed in implementation from a thrown `RpcException(Unavailable)`, which the interceptor passes through unlogged). `DeclineRate + ErrorRate` may not exceed 1. | The simulation lives in the processor already. `ProcessingDelay` is the provider's normal latency, not a fault, so it stays as it is. A decline is a business outcome (order `FAILED`, 201); an error is a failure (502 `payment_unavailable`). The exercises need both. |
| 6 | **Database delay: `Faults:DatabaseDelay`, a `DbCommandInterceptor` in `TinyShop.ServiceDefaults.Data`**, added by product-, user- and order-service. It **appends** `DO $$BEGIN PERFORM pg_sleep(<seconds>); END$$;` to each command the application sends (EF Core's `CommandSource`: LINQ queries, `SaveChanges`, raw SQL, `ExecuteUpdate`/`ExecuteDelete`), so Postgres itself waits before the command completes. Per service only, no ID filter. EF Core's own commands (`Unknown`, migrations, ...) are never delayed, among them the readiness check's `SELECT 1`. (Changed in implementation, see the activity log: first a prefixed `SELECT pg_sleep`, which breaks EF Core's reading of results and row counts by position, and which also delayed the readiness check.) | Inside the command, the wait is part of the database call in every view: the command's duration, Postgres's own statistics, and later the database span (AC3). Shared plumbing (D25): one implementation, three services. The interceptor sees SQL, not business IDs; matching on parameter values would be fragile. A delayed readiness check would make Docker mark the service unhealthy. |
| 7 | **Randomness behind an interface** (`IFaultRandom`, `Random.Shared` in production), replaced by a fixed sequence in tests. | A test of "20 %" is then exact, not statistical: no flaky tests. Rates 0 and 1 are deterministic anyway. |
| 8 | **Active faults are visible:** at startup, one Warning lists every fault that's on. An injected exception has its own type (`InjectedFaultException`) and message. | A fault left on by accident is noticed. For now the person setting the faults is also the one investigating; faults that hide themselves wait for the blind incidents (outline §25). |
| 9 | **Health endpoints are never affected**, by any fault. | A 20 % error fault on `/health/ready` would make the container unhealthy, fail `docker compose up --wait` and the CI `network` job, and teach nothing. |
| 10 | **With every switch off, nothing changes** (AC2): no extra wait, no extra SQL, no extra random number drawn. | The exercises compare "with fault" against "without", and the CI tests must pass unchanged. |

## Phases
1. **Plumbing (Claude):** `IFaultRandom`, `FaultRate`, `InjectedFaultException` and `IFaultSettings` in `ServiceDefaults.Abstractions/Faults/`; the startup Warning in `UseServiceDefaults` (and payment-service's `UseApi`).
2. **product-service errors (Claude):** the middleware and its tests.
3. **payment-service faults (Claude):** delay, declines, errors, the user filter, tests.
4. **Database delay (assignee drafts, Claude completes and reviews):** the interceptor in `ServiceDefaults.Data`, registered in the three services, and its tests against a real Postgres (Testcontainers).
5. **Compose, `.env.example`, `docs/faults.md` (Claude).**
6. **Experiments (together):** every fault in the running stack, seen in the browser and in `docker compose logs`.

## Acceptance criteria
- [x] AC1: Every fault can be turned on with a `.env` change and `docker compose up -d`, with no image rebuild.
- [x] AC2: With every switch off, behaviour is exactly the same as before this ticket. The CI tests pass unchanged.
- [ ] AC3: The database-delay fault happens **inside** the database call (`pg_sleep`), not around it in C#. Your notes explain why that will matter for tracing later.
- [x] AC4: product-service's errors can be limited to one product ID, payment-service's faults to one user ID.
- [x] AC5: Every fault has a test showing it works when on and does nothing when off.
- [x] AC6: An invalid value (for example `ErrorRate=2`, `Delay=abc`) stops the service at startup with a message naming the setting.
- [x] AC7: With faults on, `/health/live` and `/health/ready` still answer normally and quickly.
- [x] AC8: `docs/faults.md` lists every switch, its effect, what the browser sees, and the settings for each exercise.

## Technical notes
These are pointers, not the solution.
- **`DbCommandInterceptor`:** override the reader, scalar and non-query methods, sync and async (`ReaderExecuting`, `ReaderExecutingAsync`, ...), and change `command.CommandText` before it runs. Register it with `options.AddInterceptors(...)` where each service calls `UseNpgsql`.
- **Which commands does the readiness check run?** Answered in phase 4: `AddDbContextCheck` calls `CanConnectAsync`, and Npgsql's `ExistsAsync` sends `SELECT 1` **through EF Core**, with `CommandSource.Unknown`. So the interceptor filters by command source.
- **The migration jobs use the same `DbContext`.** They get no `Faults` settings, so they're never delayed. Check that this holds.
- **An empty `.env` value:** `${X:-}` passes an empty string. Check what the configuration binder makes of that for an optional ID.
- **order-service has no deadline on the payment call** (contracts.md §7): a payment delay of 120 s holds the order for 120 s, and nginx answers 504 after 60 s (TS-015, question 3).

## Learning objectives
- Where a fault is injected decides what an investigation sees later
- EF Core interceptors: what they can see and change
- Validating configuration at startup
- Testing randomness without flaky tests

## Questions to answer
Write the answers in `notes/`.
1. Why `pg_sleep` inside the command rather than `Task.Delay` around it? What would each look like in a trace (M3)?
2. What does the browser see for a declined payment, and for a technical payment error? Why is one a 201 and the other a 502?
3. Why must the health endpoints never be affected by a fault?
4. product-service fails 20 % of its requests. What share of orders fails, and why? (Count the calls an order makes to product-service.)
5. Why is a fault that throws an exception more realistic than one that returns a 500?

## Definition of done
- [ ] All acceptance criteria met
- [ ] Questions answered in `notes/`
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
- 2026-11-09: Refined. Decisions 1-10, phases, AC6-AC8, technical notes and questions added; `needs-refinement` removed. Points 3 → 5. Claude builds the plumbing and the application faults; the assignee drafts the database interceptor
- 2026-11-09: Started (branch `TS-016-fault-injection-switches`)
- 2026-11-09: Phases 1-3 done (Claude):
  - Shared: `ServiceDefaults.Abstractions/Faults/` and the startup Warning; payment-service now references `.Abstractions` (csproj, solution, Dockerfile, shared README, epic D25)
  - product-service: `ProductFaultOptions`, `ProductFaultFilter`; integration tests (500, product filter, 400/404 unchanged, health, startup validation) and unit tests with a fixed random
  - payment-service: `PaymentFaultOptions` (with DeclineRate + ErrorRate <= 1), `PaymentProcessor`; unit tests with fake time and a fixed random, integration tests over gRPC
  - Decisions 4 and 5 updated to what was built (an action filter; an exception, answered with INTERNAL)
  - All six solutions build with `-warnaserror` and pass `dotnet format`; every test suite passes (shared 40, product 75, payment 58, user 48, order 169, gateway 78); the boundaries check and both images build
- 2026-11-09: Phase 4 (drafted by the assignee: `DatabaseFaultOptions`; completed by Claude): `DatabaseDelayInterceptor` and `DatabaseFaultExtensions` in `ServiceDefaults.Data/Faults/`, wired into product-, user- and order-service. Two findings from the tests against Postgres changed decision 6:
  - A wait **in front** of the command (`DO ...; <query>`) broke `SaveChanges`: EF Core reads each statement's affected-row count by position, saw the `DO`'s 0 for the status UPDATE, and threw `DbUpdateConcurrencyException` (order-service). The wait is now **appended**
  - The readiness check **was** delayed: Npgsql's `CanConnect`/`Exists` sends `SELECT 1` through EF Core. The interceptor now delays only the application's commands, by `CommandSource`
  - Shared 55, product 80, order 170, user 48 tests pass; Release builds with `-warnaserror`, `dotnet format`, the boundaries check, and all three service and migration images
- 2026-11-10: Phase 5 (Claude): `compose.yaml` passes each service only its own switches (`Faults__<Name>: ${<SERVICE>_FAULT_<NAME>:-<off>}`, 9 in all); `.env.example` lists them, off; `tinyshop/docs/faults.md` (every switch, what the browser sees, settings per exercise, limits such as Npgsql's 30 s command timeout and nginx's 60 s); architecture.md links it and names the `Faults__*` convention. An existing `.env` without the new variables gets every fault off (checked with `docker compose config`). AC2 and AC4-AC7 ticked from the tests (AC3 also needs the notes); AC1 and AC8's browser outcomes are checked live in phase 6 (Docker wasn't running)
- 2026-11-10: Phase 6, the live check (Claude), in a throwaway Compose project with `.env.example`, each fault switched through the environment:
  - AC1: every switch took effect with `docker compose up -d`; Compose recreated only the affected services, no image changed, and each logged its Warning. Side effect noticed: `up -d` also starts the one-off migration jobs again (Compose starts exited containers; the migrations are idempotent)
  - AC8: every row of faults.md's "what the browser sees" matched (decline 201 `FAILED`; error 502 `payment_unavailable` with `orderId`, user filter; product 1 only 502 `dependency_failed`; all products 500 with `traceId`, container healthy; payment 3 s; database delays in product, user and order), with two corrections:
    - A database delay of 31 s returned **200 after 30.4 s**: since phase 4 the wait runs after the query, so Npgsql's 30 s command timeout cuts it short without failing the request. `DatabaseDelay` is now capped at 25 s (validated; a test checks 26 s fails), and faults.md says why
    - A payment delay of 60 s: nginx's 504 after 60 s, then order 1012 stayed **`PENDING` for good**. The cancellation travels the whole chain (nginx → gateway → order-service → payment-service, "cancelled by the caller"), and order-service treats a cancelled call as "my caller left". faults.md and contracts.md §7 corrected (§7 said order-service carries on)
  - Left for the assignee: the browser side of the experiments, and the answers in `notes/` (AC3 needs them)
