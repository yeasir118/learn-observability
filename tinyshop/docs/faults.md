# TinyShop fault switches

Failures on purpose, for the lab exercises (TS-016). Every switch is an environment variable, and all of them are **off by default** (epic decision D11). Turning one on changes no code and rebuilds no image.

## Turning a fault on and off

1. Set the variable in `tinyshop/infrastructure/compose/.env` (the full list, all off, is in `.env.example`).
2. From that folder: `docker compose up -d`. Compose recreates only the services whose environment changed, in seconds; nothing is rebuilt.
3. Check: each service logs one Warning per active fault at startup:

   ```bash
   docker compose logs payment-service | grep "Fault injection is ON"
   ```

   ```text
   Fault injection is ON: Faults:DeclineRate=0.2: 20 % of payments are declined (every user)
   ```

To turn it off, set the variable back to its off value (or delete the line) and run `docker compose up -d` again.

An invalid value (`2` for a rate, `abc` for a delay, `0` for an ID, or decline and error rates adding up to more than 1) stops the service at startup with a message naming the setting. `docker compose ps` then shows it exited, and `docker compose logs <service>` says why.

## The switches

Rates are between `0` and `1`. Delays are `hh:mm:ss`, fractions allowed (`00:00:00.500`): at most one minute for payments, 25 s for the database (below). An empty ID means everyone.

| `.env` variable | Service | Off | Effect |
|---|---|---|---|
| `PRODUCT_SERVICE_FAULT_ERROR_RATE` | product-service | `0` | That share of product requests fails with 500 `internal_error` |
| `PRODUCT_SERVICE_FAULT_PRODUCT_ID` | product-service | empty | Limits the error rate to `GET /products/{this ID}`; the list and all other products are unaffected |
| `PAYMENT_SERVICE_FAULT_DELAY` | payment-service | `00:00:00` | Every payment takes this much longer (on top of the normal 50 ms) |
| `PAYMENT_SERVICE_FAULT_DECLINE_RATE` | payment-service | `0` | That share of payments is declined with `card_declined` |
| `PAYMENT_SERVICE_FAULT_ERROR_RATE` | payment-service | `0` | That share of payments fails with a technical error |
| `PAYMENT_SERVICE_FAULT_USER_ID` | payment-service | empty | Limits all three payment faults to this user's payments |
| `PRODUCT_SERVICE_FAULT_DATABASE_DELAY` | product-service | `00:00:00` | Every database command of product-service waits this long in Postgres |
| `USER_SERVICE_FAULT_DATABASE_DELAY` | user-service | `00:00:00` | The same, for user-service |
| `ORDER_SERVICE_FAULT_DATABASE_DELAY` | order-service | `00:00:00` | The same, for order-service |

Inside the services these are the configuration keys `Faults:ErrorRate`, `Faults:ProductId`, `Faults:Delay`, `Faults:DeclineRate`, `Faults:UserId` and `Faults:DatabaseDelay` (Compose passes them as `Faults__<Name>`). Running a service with `dotnet run`, set them the same way, for example `Faults__ErrorRate=0.2`.

**Never affected:** the health endpoints (`/health/live`, `/health/ready`, and payment-service's gRPC health check). A fault never makes a container unhealthy.

## What each fault looks like

### product-service errors

The fault throws an exception before the controller action runs, exactly like a bug would: the service logs it as an error with a stack trace (`InjectedFaultException: Injected fault (Faults:ErrorRate=0.2): GET /products/2 failed on purpose.`) and answers 500 `internal_error`.

| Where you look | What you see |
|---|---|
| Shop page (`GET /api/products`) | The product list fails: 500 `internal_error`, passed through by the gateway, with product-service's `traceId` |
| Placing an order | order-service's call `GET /products/{id}` gets the 500, so the order fails with **502 `dependency_failed`**, and no order is created (contracts.md §6) |

With `PRODUCT_SERVICE_FAULT_PRODUCT_ID=1`, the shop page works, and only orders for product 1 (Laptop) fail.

### payment-service delay, declines and errors

The delay comes first; then one random number decides: error, decline or approval. So `DECLINE_RATE=0.1` and `ERROR_RATE=0.1` mean 10 % declined, 10 % failed and 80 % approved.

| Fault | gRPC answer to order-service | What the browser gets for `POST /api/orders` | The order |
|---|---|---|---|
| Delay | the normal answer, later | the normal answer, later | as without the delay |
| Decline | `success = false`, `failure_reason = "card_declined"` | **201**, the order with `"status": "FAILED"`, `"failureReason": "card_declined"` | `FAILED` |
| Error | `INTERNAL` ("Payment processing failed.") | **502 `payment_unavailable`**, with `orderId` | `FAILED` |

A decline is a business outcome (the request worked; the card was refused), an error is a failure (the request didn't work). The error is logged by payment-service with its stack trace, and by order-service as a Warning.

**A delay of a minute** outlasts nginx's 60 s, and the order is left `PENDING` for good. Measured in TS-016's experiments: the browser gets nginx's 504 `upstream_timeout` after 60 s; nginx closes its connection, the gateway cancels its call, order-service cancels the gRPC call, and payment-service stops ("cancelled by the caller"). order-service treats that as "my caller left", not as a payment failure, so nothing ever updates the order. See contracts.md §7.

### Database delay

Every command the service itself sends through EF Core (its queries and its writes) gets a statement appended, `DO $$BEGIN PERFORM pg_sleep(n); END$$;`, so **Postgres** waits before the command completes. The time is spent inside the database call, not in application code: that's what makes Failure Exercise 3 answerable with a trace. EF Core's own commands, such as the readiness check's `SELECT 1` and the migrations, aren't delayed.

| Service | What gets slower |
|---|---|
| product-service | The shop page's product list, and every order (order-service asks product-service for the product) |
| user-service | The user lookup, and every order (order-service asks user-service for the user) |
| order-service | Placing an order (several commands: the insert and the status update each wait), the order and user-orders pages |

**At most 25 s.** Npgsql cancels a command after 30 s (its default command timeout). Because the wait runs *after* the real statement, a longer one wouldn't fail the request: the rows have already arrived, the wait is cut short, and the request succeeds after an arbitrary 30 s. So the setting is capped below that.

## Settings per exercise

Set these in `.env`, run `docker compose up -d`, generate traffic (the shop page, or TS-017's load generator), and turn them off again afterwards.

| Exercise | Settings | What to expect |
|---|---|---|
| Failure Exercise 1: application failure (TS-019, outline §12) | `PRODUCT_SERVICE_FAULT_ERROR_RATE=0.3` (optionally `PRODUCT_SERVICE_FAULT_PRODUCT_ID=1`) | About 30 % of product requests fail; orders fail with 502 `dependency_failed` |
| Failure Exercise 2: slow payment (outline §17) | `PAYMENT_SERVICE_FAULT_DELAY=00:00:03` | Every order takes about 3 s longer |
| Failure Exercise 3: slow database (outline §18) | `PRODUCT_SERVICE_FAULT_DATABASE_DELAY=00:00:02` | The product list and every order take about 2 s longer |
| Failure Exercise 4: error rate (outline §21) | `PAYMENT_SERVICE_FAULT_ERROR_RATE=0.2` | About 20 % of orders fail with 502 `payment_unavailable` |
| Incident A: checkout is slow (outline §25) | `ORDER_SERVICE_FAULT_DATABASE_DELAY=00:00:01`, or `PAYMENT_SERVICE_FAULT_DELAY=00:00:03` with `PAYMENT_SERVICE_FAULT_USER_ID=124` for "sometimes" | Orders take seconds; the product list stays fast |
| Incident B: 10-20 % of orders fail (outline §25) | `PAYMENT_SERVICE_FAULT_ERROR_RATE=0.15` | About 15 % of orders fail |
| Incident C: one user's order fails (outline §25) | `PAYMENT_SERVICE_FAULT_ERROR_RATE=1`, `PAYMENT_SERVICE_FAULT_USER_ID=123` | Every order of user 123 fails; everyone else's works |

These incidents are only "blind" if someone else sets the faults: the startup Warnings and the `InjectedFaultException` messages name them on purpose (TS-016, decision 8).

## Not supported

- Changing a fault without recreating the container: a container's environment is fixed while it runs (TS-016, decision 2).
- Faults that change over time (intermittent bursts) or hide themselves from the logs.
- Faults in the gateway, nginx or Postgres itself. For "gone" or "frozen", stop or pause the container: `docker compose stop gateway`, `docker compose pause order-service`.
