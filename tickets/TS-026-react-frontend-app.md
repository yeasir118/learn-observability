---
key: TS-026
title: React frontend app (runs locally)
type: Story
status: To Do
priority: High
points: 3
assignee: claude
reporter: claude
epic: TS-001
labels: [m1, frontend, react, ci]
depends_on: [TS-014]
created: 2026-11-04
---

# TS-026: React frontend app (runs locally)

## Summary
Write the TinyShop web app in `tinyshop/services/frontend/`: a small React application that lists products, looks up a user, places an order and shows orders, all through the gateway's public API. It runs on the host with the Vite dev server; packaging it in a container behind nginx is TS-015.

## Background
- **Decision D12:** Claude writes the React app; the assignee reviews it. Everything around it (image, nginx, Compose, networking) is TS-015, done together.
- **The API:** only the five public routes of [contracts.md §4](../tinyshop/docs/contracts.md#4-public-api-gateway), always as **relative URLs** (`/api/...`). The app never knows a host or port: in development the Vite dev server forwards `/api` to the gateway, in the container nginx does (TS-015). That is what makes the app same-origin, with no CORS.
- **Money:** prices are integer cents; the app divides by 100 for display only and does no arithmetic (contracts.md, conventions).
- **Errors:** every error is Problem Details ([contracts.md §2.1](../tinyshop/docs/contracts.md#21-problem-details)). The app shows its `title`, `code` and `traceId`. The `traceId` is what Failure Exercise 1 (TS-019) and M2 search the logs for.
- **Running locally:** everything on the host, as [architecture.md §8](../tinyshop/docs/architecture.md#8-local-development) describes: the services with `dotnet run` (5101-5103, databases through the debug proxies), the gateway on 5100, the Vite dev server on 5173.

## Scope
**In scope**
- Vite + React + TypeScript (strict) in `tinyshop/services/frontend/`, with a committed `package-lock.json` and the Node version pinned in `.nvmrc`
- Pages (client-side routes, React Router):
  - `/`: the shop. Product list; user lookup by ID; order form (user ID, product, quantity) that creates the order and then opens it
  - `/orders/:id`: one order, including `status`, `failureReason` and `transactionId`
  - `/users/:id/orders`: a user's orders
- A small API client: `fetch`, relative URLs, Problem Details parsed into one error type
- Loading, empty and error states on every page
- Vite dev proxy: `/api` → `http://localhost:5100`, overridable with the environment variable `GATEWAY_URL`
- Tests (Vitest, Testing Library): the API client (success, Problem Details, a non-JSON error body, network failure), money formatting, and one page against a mocked `fetch`
- ESLint and a type check
- CI: a `frontend` job (`npm ci`, lint, type check, tests, build), added to `ci-passed`; Dependabot `npm` entry
- `services/frontend/README.md`: how to run it locally and against what
- Docs: architecture.md repository layout entry for `services/frontend/`

**Out of scope**
- Dockerfile, nginx, Compose, the matrix script (TS-015)
- A UI or state-management library, styling beyond plain CSS, authentication, internationalization
- Any business rule: validation and totals stay on the server (contracts.md §2.4)

## Decisions
| # | Decision | Why |
|---|---|---|
| 1 | **Vite, React, TypeScript, React Router; no other runtime libraries.** | Small enough to read in one review. Client-side routes are what TS-015's `try_files` fallback exists for. |
| 2 | **Relative `/api` URLs only, never a base URL from configuration.** | The app is served from the same origin in every environment; there's nothing to configure and no CORS. |
| 3 | **Errors show `title`, `code` and `traceId`**, and a non-Problem-Details error (for example an HTML page from a proxy) still shows its status. | The `traceId` links what a user sees to the logs (TS-019, M2). Proxies in front of the gateway can answer with something else (TS-015 decision 5). |
| 4 | **Node: the current Active LTS major**, pinned in `.nvmrc` and used by CI (`node-version-file`). TS-015's build stage uses the same major. | One Node version everywhere, as `global.json` does for .NET. |

## Acceptance criteria
- [ ] AC1: With the services and the gateway running on the host, `npm run dev` gives a working shop at `http://localhost:5173`: list products, look up a user, place an order (paid and declined), view it, list a user's orders.
- [ ] AC2: Every API call in the browser's Network tab goes to `localhost:5173/api/...`; the code contains no host, port or absolute API URL.
- [ ] AC3: Errors are shown with title, code and `traceId`: an unknown user (422 `user_not_found`), an unknown order (404), and a stopped upstream (502 `upstream_unreachable`).
- [ ] AC4: `npm ci`, lint, type check, tests and `npm run build` pass locally and in the CI `frontend` job, which `ci-passed` requires.
- [ ] AC5: `npm run build` produces static files only (`dist/`), with content-hashed asset names.

## Learning objectives (for the reviewer)
- Phase 2 of loading a web app: what the browser fetches after `index.html`, and from where
- What a production build of a single-page app is: a few static files, no server code

## Questions to answer
Write the answers in `notes/`.
1. Look at `dist/` after a build: which files are there, and why do the asset names contain a hash? (TS-015 builds its caching rules on this.)
2. `npm run dev` and the built app call the same `/api/...` URLs. Who answers them in each case?

## Definition of done
- [ ] All acceptance criteria met
- [ ] Questions answered in `notes/`
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-11-04: Created, split from TS-015: the app (Claude) and its container (TS-015, together) are separate tickets
