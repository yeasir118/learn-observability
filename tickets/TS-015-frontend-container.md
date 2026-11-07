---
key: TS-015
title: Frontend container and nginx reverse proxy
type: Story
status: In Progress
priority: High
points: 5
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, docker, nginx, networking, frontend, compose, ci]
depends_on: [TS-014, TS-026]
created: 2026-10-18
---

# TS-015: Frontend container and nginx reverse proxy

## Summary
Package the React app (TS-026) as an nginx image that serves the built files and reverse-proxies `/api/*` to the gateway; run it in Compose on `edge-network`, published on host port 8000, the only port of the default stack; and prove the reachability matrix again with the real frontend instead of TS-014's stand-in. Done together: the assignee drafts each phase, Claude completes and reviews it.

## Background
- **Decision D12:** the app is Claude's (TS-026) and already runs with the Vite dev server. This ticket is everything around it: the build, the image, nginx and the networking.
- **The two phases of loading a web app** (M1 discussion) become concrete: the browser downloads the app from nginx, then the app calls `/api/...` on the same origin, and nginx forwards those calls to the gateway.
- **From TS-014:**
  - **Q5 is resolved:** the gateway owns `/api/*`, so nginx forwards the path **unchanged**.
  - **Decision 4:** `X-Forwarded-*` headers were deferred to nginx; decided here (decision 4 below).
  - **Decision 8 applies again:** an entry point doesn't wait for what it calls.
  - **The matrix script** (`infrastructure/scripts/check-reachability-matrix.sh`) uses a stand-in frontend until this ticket; the real one replaces it.
- **Timeouts:** [contracts.md §7](../tinyshop/docs/contracts.md#7-default-timeouts-in-m1): nginx waits 60 s for the gateway, the gateway 100 s for a service. Question 3.
- **Settles architecture.md Q3:** nginx runs as non-root.

## Scope
**In scope**
- **Image:** a multi-stage Dockerfile in `services/frontend/` (build context: that folder): a Node stage runs `npm ci` and `npm run build`, and the nginx stage serves `dist/`. Plus a `.dockerignore`.
- **nginx config**, in `services/frontend/` next to the Dockerfile, since it's baked into the image:
  - static files, with a fallback to `index.html` for client-side routes
  - caching headers
  - the `/api/` reverse proxy, with runtime DNS and `X-Forwarded-*`
  - nginx's own `/api/` errors as Problem Details
  - an access log that shows the upstream hop
- **Compose:** the `frontend` service on `edge-network`, published `8000:8080`, with a health check and no `depends_on`
- **Network proof:** the matrix script uses the real frontend; the stand-in code is removed
- **CI and Dependabot:**
  - CI `image` entry; the image matrix gains a `context` field
  - the `network` job passes with the real frontend
  - Dependabot `docker` entry for `services/frontend`
- **Docs:**
  - the frontend README: "Against the stack in Docker" switches from the temporary `gateway-forward` container to `GATEWAY_URL=http://localhost:8000 npm run dev` (Vite → nginx → gateway; no extra Compose service needed)
  - architecture.md: §2 frontend row, §3, §4 (port, stand-in), §8, Q3
  - contracts.md §4: nginx's own errors
  - epic D12

**Out of scope**
- The React app itself (TS-026)
- TLS and HTTP/2 (epic M1.5)
- Security headers (CSP and others), compression, rate limiting, authentication
- Caching API responses
- Timeouts of our own (D5)

## Decisions (made in refinement, 2026-11-04)
| # | Decision | Why |
|---|---|---|
| 1 | **Runtime image `nginxinc/nginx-unprivileged`**, Alpine, on the current stable branch (an even minor, for example `1.30-alpine`: nginx's major is always 1, so the minor is the line to pin, D23). It listens on **8080**; Compose publishes **`8000:8080`** on all interfaces. Settles **Q3**. | Non-root like every other container (TS-010). An unprivileged process can't bind ports below 1024, hence 8080. All interfaces is intended for the frontend (architecture.md §2, AC10). |
| 2 | **Build stage `node:<LTS>-alpine`**, the same major as TS-026's `.nvmrc`; it runs only `npm ci` and `npm run build` (which type-checks), not lint or tests. The final image has no Node, no `node_modules` and no source. | The build tooling isn't needed to serve static files, and would only add size and attack surface. The CI `frontend` job already runs every check; repeating them in each image build costs time and proves nothing new. |
| 3 | **nginx resolves `gateway` at request time:** `resolver 127.0.0.11` (Docker's DNS) with a short `valid=`, and the upstream in a variable (`set $gateway ...; proxy_pass $gateway;`, no URI part, so the path passes unchanged). **No `depends_on: gateway`.** | A literal `proxy_pass http://gateway:8080` is resolved once at startup: nginx refuses to start if the gateway doesn't exist yet ("host not found in upstream"), and keeps the old IP after the gateway is recreated (502 until nginx restarts). Runtime resolution removes both, and with them the reason for `depends_on` (TS-014 decision 8). |
| 4 | **nginx sets `X-Forwarded-For` (`$remote_addr`) and `X-Forwarded-Proto`, replacing what the client sent** (not `$proxy_add_x_forwarded_for`, which appends to it). The gateway passes them through (TS-014 decision 4); nothing trusts them yet. | nginx is the first hop we control, so it's the only one that knows the real client address. Appending would forward whatever a browser claims (question 6). |
| 5 | **nginx's own errors on `/api/` are Problem Details:** `error_page` for 502/504 returns `application/problem+json` with `upstream_unreachable` / `upstream_timeout` (no `traceId`: no .NET code ran). The gateway's own responses, errors included, pass through untouched (`proxy_intercept_errors` stays off). | The app expects §2.1 errors from `/api` (TS-026 decision 3). Same rule as the gateway: errors of the hop behind are passed on, only the proxy's own errors are its to write. |
| 6 | **Static files:** `try_files $uri /index.html` for client-side routes, but a missing file under `/assets/` is a real 404. `index.html` with `Cache-Control: no-cache`; `/assets/*` (content-hashed names) `max-age=31536000, immutable`. | Reloading `/orders/1001` must work (AC2). A missing script must not come back as HTML with status 200. Hashed names make caching forever safe; a cached `index.html` would point at old assets after a deploy (question 5). |
| 7 | **Health check:** BusyBox `wget --spider http://localhost:8080/` in Compose. It checks that nginx serves the app, not the gateway. | Same reasoning as TS-014 decision 8: a stopped gateway must not make the frontend unhealthy; nginx answers its own 502 for `/api` meanwhile. |
| 8 | **Access log** in a named format with `$upstream_addr`, `$upstream_status`, `$upstream_response_time` and `$request_time`, written to stdout. | Shows hop 2 next to hop 1 (AC11), and the gap between the two times is nginx's own share. A small preview of M2. |
| 9 | **Build context `services/frontend/`**, not `tinyshop/`: the CI `image` matrix gains a `context` field (`tinyshop` for the .NET services). | The frontend needs nothing shared; a smaller context is a faster, more predictable build. |
| 10 | **The matrix script drops the stand-in** and requires a running `frontend` service; the frontend's port becomes 8080. The browser row stays "✓ (via :8000)". | The proof should test the real container. Dead fallback code would hide a missing frontend. |

## Phases
Each phase ends working and reviewed. You draft each one; Claude completes and reviews it.
0. **Understand:** how nginx matches a request (`server`, `location` and their precedence), `try_files`, `proxy_pass` with and without a URI, and how nginx resolves names. Claude explains with a small runnable demo.
1. **Image:** Dockerfile and `.dockerignore`; `docker run` alone serves the app (its API calls fail: no gateway yet).
2. **Static serving:** the fallback, the `/assets/` 404 and the caching headers (decision 6).
3. **The proxy:** `/api/` with runtime DNS, `X-Forwarded-*`, the error pages and the access log; then the Compose service. AC1-AC3 in the browser.
4. **Proof and CI:** the matrix script with the real frontend, the CI `image` entry with `context`, the `network` job and Dependabot.
5. **Experiments:** recreate the gateway, stop it, the phone on your LAN, hop 2 in the access log, a slow upstream.
6. **Docs.**

## Acceptance criteria
- [ ] AC1: `http://localhost:8000` loads the app, and placing an order works end to end.
- [ ] AC2: Reloading `/orders/1001` in the browser works (the `index.html` fallback); a missing `/assets/x.js` gives a 404, not `index.html`.
- [ ] AC3: In the browser's Network tab, every API call goes to `localhost:8000/api/...`, and there are no CORS errors.
- [ ] AC4: The image has no Node.js, no `node_modules` and no source; nginx runs as non-root and listens on 8080.
- [ ] AC5: nginx forwards the path and query unchanged, and sets `X-Forwarded-For` and `X-Forwarded-Proto`, replacing any the client sent. Verified against an echo server that stands in for the gateway.
- [ ] AC6: With the gateway stopped, `/api/...` answers nginx's own 502 `upstream_unreachable` as Problem Details, and the app shows it. The gateway's own errors (for example its 404 `not_found` for `/api/nope`) arrive unchanged.
- [ ] AC7: The frontend starts and is healthy while the gateway is stopped. After `docker compose up -d --force-recreate gateway`, the app works again without restarting nginx.
- [ ] AC8: `index.html` is sent with `no-cache`, `/assets/*` with a one-year `immutable` cache.
- [ ] AC9: The reachability matrix is proven with the real frontend: the stand-in is gone, the CI `image` and `network` jobs pass, and Dependabot watches the frontend's Dockerfile.
- [ ] AC10: **Experiment:** open the app from your phone using your PC's LAN IP. Record what you had to allow in Windows Firewall.
- [ ] AC11: nginx's access log shows hop 2 (nginx → gateway: the gateway's address, status and time) for a request you also saw in DevTools (hop 1).
- [ ] AC12: Docs updated:
  - architecture.md: §2 frontend row (image, port 8080), §3, §4 (port, stand-in text), §8, Q3 resolved
  - contracts.md §4: nginx's own errors
  - epic D12

## Technical notes
These are pointers, not the solution.
- **The unprivileged image** already moves the PID file and temporary paths to where a non-root user can write. Put your server block where the image's default one is (`/etc/nginx/conf.d/default.conf`) rather than replacing `nginx.conf`.
- **A variable in `proxy_pass`** changes two things: names are resolved at request time (which needs a `resolver`), and with no URI part the original request URI is passed as is. Check both with the echo server.
- **`location` matching:** `/api/` (prefix) versus `/` (the fallback) versus `/assets/`. Which wins for `/api/products`, and why? Also: should `/api` without a trailing slash reach the gateway?
- **`error_page` for proxied requests** applies only to errors nginx generates itself while `proxy_intercept_errors` is off. That's exactly the split decision 5 wants.
- **Echo server for AC5:** in a throwaway Compose project, give an HTTP echo container the network alias `gateway`. nginx's runtime DNS then sends `/api` calls to it. Pin its version.
- **HTTP version to the gateway:** check which version nginx speaks upstream and what `Connection` header it sends; TS-014's hop-by-hop rules are on the receiving end (question 7).

## Learning objectives
- How nginx chooses a `location`, serves static files and proxies the rest
- DNS inside Docker over time: startup resolution versus request-time resolution, and what a recreated container does to cached addresses
- Who may set `X-Forwarded-*`, and why the first proxy overwrites them
- HTTP caching for a single-page app: what may be cached, for how long, and why
- How a published port reaches the LAN, and what binding to an interface changes

## Questions to answer
Write the answers in `notes/`.
1. `proxy_pass http://gateway:8080;` versus `proxy_pass http://gateway:8080/;`: what path reaches the gateway for `/api/orders/1001` in each case, and why?
2. The gateway is recreated while nginx keeps running. What happens with a literal `proxy_pass` and with `resolver` plus a variable, and why?
3. order-service hangs for 120 seconds on `POST /api/orders`. What does the browser get, from which component, and after how long? What happens to the order?
4. Why could your phone reach the app? What would publishing `127.0.0.1:8000:8080` change?
5. Why must `index.html` not be cached, when `/assets/*.js` can be cached for a year?
6. Who can be trusted to set `X-Forwarded-For`? What would `$proxy_add_x_forwarded_for` forward if a browser sent its own value?
7. Which HTTP version and `Connection` header does nginx use towards the gateway? What does the gateway do with that header (TS-014, hop-by-hop)?

## Definition of done
- [ ] All acceptance criteria met
- [ ] Questions answered in `notes/`
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
- 2026-10-18: Updated for the `tinyshop/infrastructure/` layout
- 2026-10-18: Paths updated for the per-service layout (`tinyshop/services/<service>/{src,tests}`)
- 2026-11-04: Refined. The React app split out into TS-026 (Claude); this ticket is the container, nginx and networking, done together. Task → Story, points 3 → 5. Decisions 1-10, phases 0-6, AC5-AC9 and AC12, and questions added; `needs-refinement` removed. Depends on TS-026
- 2026-11-07: Decision 2 says what the image build runs. Developing on the host against the Docker stack goes through nginx (`GATEWAY_URL=http://localhost:8000`), so no separate gateway proxy in Compose
- 2026-11-07: Started (branch `TS-015-frontend-container`)
