# Failure Exercise 1: application failure without observability

## Before

### TS-004 question 3
> A client receives a `502` from `POST /api/orders`. From the response alone, can it tell which service failed? Should it be able to?

From the response alone, the client won't be able to tell the exact service name that failed. And it shouldn't be able to do this either since that exposes the internals to the outside world and poses security risks.

*Checked against contracts.md §2 before the exercise:* not the exact service, but more than nothing. The body's `code` narrows it down: `upstream_*` means the edge (nginx or the gateway) gave up, `dependency_failed` means order-service's user- or product-service call failed, `payment_unavailable` means the payment failed (and the order exists, `FAILED`). So a client can tell **where in the chain** and **what kind** of failure, without internal names. The body also carries a `traceId`, meant for "give this to support so they can find your request"; whether anything on the server side can be found with it is a question for the exercise.

### Predictions
Written before the incident, without looking anything up, and without knowing which fault was switched on (Claude ran the incident blind, see "The incident"). Not corrected afterwards: comparing them with what happened is part of the exercise (AC5).

- **The user:** an order fails. What do they see, and which status code?
  - They can see different status codes based on the situation. But I am focused on 3: 500 means the problem is with nginx and 502, 504 means the problem is with upstream service.
- **k6:** which checks go red? Roughly what share of all requests fails?
  - Cannot say for sure. Order service itself can fail, in which case checks related to order service will fail, and if other service(s) fail on which the order service depends, then the checks related to those services fail.
- **The logs:** which containers show something about the failure, and what will such a line contain?
  - I would inspect gateway logs first. Then the upstream service from which the error is originating, to see what internal problem that particular service is facing.
- **Linking:** how will you know that a gateway line and a service's line belong to the same request?
  - Timestamp, request path.
- **The plan:** in what order will you look at things?
  - Check the code. If it is related to nginx, then check nginx logs, else start on gateway.
  - Based on gateway logs, check the particular upstream service logs whose request-response are failing.

### The normal picture
Every fault off, the load generator's traffic (2 minutes at 10 per second; checked again in a 20 s run at 5 per second: 122 requests, 0 % failed, median 5 ms, p95 181 ms).

**Every container logs every request, in its own way:**

| Container | What it logs per request | Lines per request (approx.) |
|---|---|---|
| frontend (nginx) | one access-log line: time, client IP, method and path, status, size, upstream address and status, times | 1: the best overview |
| gateway | its calls to the services (`System.Net.Http.HttpClient.<service>.*`): start, sending, response headers with status and time, end. Not the incoming request itself | 4 per upstream call |
| product-service, user-service | an application line (`ProductCatalogService`, `UserDirectoryService`) and the SQL it ran (`Microsoft.EntityFrameworkCore.Database.Command`, with the full query and its time) | 2 log entries, the SQL spread over several lines |
| order-service | the same, plus its calls to user- and product-service (`HttpClient.IUserDirectory.*`, ...) | the most: about 10 per order |
| payment-service | business lines: "Processing payment for order 1021, user 126, 10000 cents", "Payment for order 1021 approved: txn_..." | 2 per payment |

A normal request in nginx's log:
```text
frontend-1  | 2026-11-19T10:53:47+00:00 172.25.0.3 "POST /api/orders HTTP/1.1" 201 300 upstream_addr=172.22.0.2:8080 upstream_status=201 upstream_time=0.072 request_time=0.071
```

The gateway's call to product-service for one `GET /api/products` (a request-response cycle):
```text
gateway-1  | 2026-11-19T10:22:23.140824490Z info: System.Net.Http.HttpClient.product-service.LogicalHandler[100]
gateway-1  | 2026-11-19T10:22:23.140852022Z       Start processing HTTP request GET http://product-service:8080/products
gateway-1  | 2026-11-19T10:22:23.140855514Z info: System.Net.Http.HttpClient.product-service.ClientHandler[100]
gateway-1  | 2026-11-19T10:22:23.140856602Z       Sending HTTP request GET http://product-service:8080/products
gateway-1  | 2026-11-19T10:22:23.142876972Z info: System.Net.Http.HttpClient.product-service.ClientHandler[101]
gateway-1  | 2026-11-19T10:22:23.142886332Z       Received HTTP response headers after 1.8637ms - 200
gateway-1  | 2026-11-19T10:22:23.142887685Z info: System.Net.Http.HttpClient.product-service.LogicalHandler[101]
gateway-1  | 2026-11-19T10:22:23.142888600Z       End processing HTTP request after 1.9372ms - 200
```

**What a line contains:** a timestamp, a level (`info`), a category (which part of the code wrote it; in the gateway, the name of the service it called), and a text message: the path, the status, the time taken.

**What connects lines across containers:** only the **time** and the **path or business IDs** (an order ID appears in nginx's `GET /api/orders/1021` and in payment-service's lines). There's **no shared request ID**: a search for `traceId`, request or correlation IDs across every container's log found nothing.

**Noise that's always there:** each service with a database runs `SELECT 1` every 10 s (the health check), logged like any other query. Under load, the logs scroll by at hundreds of lines a minute, mostly multi-line SQL.

## The incident
Run blind by Claude in a separate Compose project (`ts019-incident`): traffic from **11:26:06 UTC** for about 2 minutes, the load generator at 10 journeys per second plus a "shopper" placing 10 orders by hand through `localhost:8000`. The evidence was handed over as files (`compose-logs.txt`, `k6-summary.txt`, `k6-report.html`, `shopper-requests.txt`, `compose-ps.txt`), with the stack left running.

What the evidence showed, before any digging:
- **k6:** about 2 % of requests failed (28 of 1,413), all of them orders: `order: status is 201` failed 28 times out of 241 (88 % passed). Every other journey stayed green. The report's "HTTP Performance overview" shows the failures spread over the whole run, between about 2 % and a peak of about 4 % per interval: a steady fault, not a burst.
- **The shopper:** 1 of 10 orders failed:
  ```text
  [2026-11-19T11:26:46Z] POST /api/orders {"userId":126,"productId":4,"quantity":1} -> 502
      {"type":"https://tinyshop.example/problems/dependency_failed","title":"Dependency failed","status":502,"detail":"A service this request depends on failed. Try again later.","instance":"/orders","traceId":"00-b0b08a39b0898fc79f6398ea6660ed27-16fd79d56431bdc9-00","code":"dependency_failed"}
  ```

## The investigation
My path: k6's report and summary first, to see the incident. Then `shopper-requests.txt` for the failing request, its status code and timestamp. Then `compose-logs.txt`, filtered by that timestamp: too many lines from too many services, so I added `502` and `gateway-1` to the filter and found order-service returning the failure. Then the same timestamp with `order-service-1`: its dependency, product-service, was failing. Then the same with `product-service-1`: the actual issue, an `InjectedFaultException` with a rate of 40 %.

```bash
grep 502 shopper-requests.txt
grep -B 1 " 502" compose-logs.txt | grep "gateway-1"
grep -E '2026-11-19T11:26:14\.' compose-logs.txt | grep "order-service-1"
grep -E '2026-11-19T11:26:14\.' compose-logs.txt | grep "product-service-1"
```

k6 doesn't tell when a request failed, only the rate of failing requests.

*Found in the review:* the failure followed above (11:26:14) is one of **k6's**, not the shopper's. The shopper's order reached nginx at 11:26:47.223, about a second after the time it recorded, and k6 had another failure at 11:26:46.009. Both lead to the same place, but searching for the shopper's own time lands on k6's failure. See question 3.

### 1. Which service failed?
**product-service.** Following one failure down the chain, each hop shows a different status:

| Hop | What it logged | Status |
|---|---|---|
| nginx | `"POST /api/orders HTTP/1.1" 502 ... upstream_status=502` | passed on the gateway's 502 |
| gateway | `HttpClient.order-service ... Received HTTP response headers after 10.4913ms - 502` | passed on order-service's 502 unchanged |
| order-service | `HttpClient.IProductCatalog ... - 500`, then `warn: ... product-service answered 500 for products/4` and `dependency_failed while handling POST /orders (dependency: product-service)` | **translated** product-service's 500 into 502 `dependency_failed` |
| product-service | `fail: ... InjectedFaultException: Injected fault (Faults:ErrorRate=0.4): GET /products/4 failed on purpose.` | **created** the error: 500 |

```bash
grep '^frontend-1.*" 502 ' compose-logs.txt
grep "^order-service-1.*2026-11-19T11:26:46\." compose-logs.txt | grep -A3 -iE "warn|fail:| 500"
grep "^product-service-1.*2026-11-19T11:26:46\." compose-logs.txt | grep -A2 "fail:"
```
**Confidence: certain**, for the failures followed. **Missing:** a way to follow one request through the hops: each step took a new `grep` on the time, and a guess that lines from the same moment belong together.

### 2. Why did the request fail?
product-service threw an unhandled exception for `GET /products/4`: `InjectedFaultException ... Faults:ErrorRate=0.4`. Its startup log says it outright: `Fault injection is ON: Faults:ErrorRate=0.4: 40 % of product requests fail with 500 (GET /products/4 only)`.

**Confidence: certain, but only because the fault names itself.** A real bug would show an exception type and a stack trace, but no "failed on purpose". The *what* would be just as clear; the *why* (bad data, a bug, a full disk?) would need the code, or more context in the log. **Missing:** context in the error line (which input, which user), and a way to see that every failure is `GET /products/4` without reading 29 stack traces.

### 3. Which request caused the failure?
- **The shopper's:** `POST /api/orders {"userId":126,"productId":4,"quantity":1}`, at nginx at 11:26:47.223 from `172.25.0.1` (the host, through the published port).
- **k6's 28:** visible in nginx's log as 502s from `172.26.0.3` (the load generator), but nginx doesn't log request bodies, so which user and product they were is only in order-service's and product-service's lines from the same moment.
- **The common factor:** all 29 failures were orders for **product 4**: every exception says `GET /products/4`. 29 of the 63 calls for product 4 failed (46 %, close to the 40 %); orders for other products never failed.

```bash
grep '^frontend-1.*" 502 ' compose-logs.txt | awk '{print $5}' | sort | uniq -c   # 1 x 172.25.0.1, 28 x 172.26.0.3
grep -c "GET /products/4 failed on purpose" compose-logs.txt                       # 29
```
**Confidence: certain for the shopper's request, guessed for any single k6 request.** Matching by time is fragile:
- The shopper recorded 11:26:46; nginx logged 11:26:47.223. Clocks and "when" differ between client and server.
- k6 had another failure at 11:26:46.009, so the shopper's own time pointed at the wrong request. Only the **client IP** told them apart.
- In two seconds of the run, two orders failed in the same second; timestamp plus status can't separate those.

**Missing:** an ID that the client gets and every log line carries. The 502's `traceId` (`00-b0b08a39...`) would be exactly that, but it appears **nowhere** in the logs (0 matches).

### 4. Did the gateway fail, or did the product service?
**product-service.** The gateway didn't fail: it passed order-service's 502 through unchanged (contracts.md §2.4). order-service didn't fail either: it **translated** product-service's 500 into 502 `dependency_failed`, as its contract says. Only product-service logged a `fail:`. The shopper's response body agrees: `dependency_failed` comes only from services behind the gateway (§2.3), and `"instance": "/orders"` is order-service's path, not the gateway's.

**Confidence: certain.** **Missing:** nothing, here, once the chain was followed; but it took three hops of `grep` to see that the gateway was only a messenger.

### 5. How would you investigate this if the system ran on another machine, with no debugger?
What I relied on that a remote system wouldn't give me for free:
- **All logs in one file**, from every container, on **one clock**. On real servers they'd be on different machines, rotated, or gone with a restarted container.
- **A handful of failures** to follow by hand. At 1,000 requests a second, `grep` on a timestamp returns thousands of lines.

What I'd want instead:
- **Central logs** collected from every service, searchable by fields (service, status, route, user), not by `grep` on text.
- **A request ID in every log line** (the `traceId`), returned to the client, so one failure can be followed straight through the chain.
- **Metrics**: error rate per service and per route, so "product-service fails 40 % of `GET /products/4`" is visible on a graph without reading a single log line.
- **Traces**: one picture of each request's hops, with the failing one marked.

That list is M2 and M3.

## After

**Time taken:** about 40 minutes, within the 45-minute time box. For 29 failures of one fault, in one file, on one clock: most of it went into narrowing thousands of log lines down by hand.

**What I set (revealed after the investigation, by Claude):** `PRODUCT_SERVICE_FAULT_ERROR_RATE=0.4`, `PRODUCT_SERVICE_FAULT_PRODUCT_ID=4`. Both were found in the logs: the rate in every exception and in the startup warning, the product in every `GET /products/4`.

### TS-004 question 3, revisited
Before: the client can't tell which service failed, and shouldn't. After: still true, but the response says more than I expected, and less than it seems:
- **More:** `dependency_failed` told me the failure was **behind order-service**, in one of its dependencies, not at the edge. That was the right first step of the investigation.
- **Less:** the `traceId` looks like the thing to hand to support, but in M1 **no log contains it**: nobody could find the request with it.
- **A leak:** `"instance": "/orders"` is order-service's internal path, passed through by the gateway. A small piece of the inside, visible from outside.

So: the client shouldn't learn *which* service, but it should get an ID that the people running the system can use. M1 hands out the ID without the means to use it.

### Predictions compared with what happened
| Prediction | What happened |
|---|---|
| "500 means the problem is with nginx, 502/504 with an upstream service" | The 500 came from **product-service**, and never reached the user: order-service turned it into a 502. nginx's own errors are 502/504 (`upstream_*`) too. A status alone doesn't say *where*; the `code` does. |
| k6: "cannot say for sure" which checks fail | Only the order checks (28 of 241 orders); every other journey stayed green, because only `GET /products/{id}` was broken, and only orders call it. About 2 % of all requests. |
| Logs: gateway first, then the service the error comes from | The right direction, but the gateway only showed that order-service answered 502. It took one more hop, order-service, to name product-service. nginx's line alone already showed which request failed, and from which client. |
| Linking by timestamp and path | Worked, barely. Second-precision times matched two failures at once, the shopper's own time pointed at k6's request, and only the client IP separated them. |
| The plan: check the `code` first | Right: `dependency_failed` pointed behind order-service straight away. |
