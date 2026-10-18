---
key: TS-015
title: Frontend container and nginx reverse proxy
type: Task
status: To Do
priority: High
points: 3
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, docker, nginx, networking, frontend, needs-refinement]
depends_on: [TS-014]
created: 2026-10-18
---

# TS-015: Frontend container and nginx reverse proxy

## Summary
Package the React frontend as an nginx image that serves the built static files and reverse-proxies `/api/*` to the gateway, published on host port 8000. This is the only port the default stack publishes.

## Background
- **Decision D12: Claude writes the React application** (in `tinyshop/src/frontend/`) before this ticket is picked up. It covers the pages and calls agreed earlier: Shop (products, user lookup, create order), Order details, and optionally User orders. This ticket is about everything around it: the build, the image, nginx and the networking.
- Phases 1 and 2 of loading a web app (downloading the app, then the app calling the API) were explained in the M1 discussion. This ticket makes them concrete.
- Settles architecture.md Q3 (whether nginx runs as non-root).

## Scope
**In scope**
- Multi-stage Dockerfile: a Node stage runs `npm ci` and `npm run build`, and the nginx stage serves the result
- nginx configuration in `src/frontend/` next to the Dockerfile, since it's baked into the image: static files, a fallback to `index.html` for client-side routes, and a reverse proxy for `/api/`
- Vite's development proxy, so that `npm run dev` on the host works against the gateway
- The Compose service on `edge-network`, published as `8000:80` (or the port your Q3 decision gives)
- CI builds the image

## Acceptance criteria
- [ ] AC1: `http://localhost:8000` loads the app. Placing an order works end to end.
- [ ] AC2: Reloading the browser on `/orders/1001` works. It doesn't give nginx's 404, because of the fallback to `index.html`.
- [ ] AC3: In the browser's Network tab, every API call goes to `localhost:8000/api/...`, and there are no CORS errors.
- [ ] AC4: The final image contains no Node.js and no `node_modules`.
- [ ] AC5: **Experiment:** open the app from your phone using your PC's LAN IP. Record what you had to allow in Windows Firewall.
- [ ] AC6: nginx's access log shows hop 2 (nginx → gateway) for a request you also saw in DevTools (hop 1).

## Technical notes
To be refined before pickup. Points to discuss: `proxy_pass` with and without a trailing slash (it changes the path that gets forwarded), the "host not found in upstream" error at startup, `try_files`, and the `nginx-unprivileged` image.

## Definition of done
- [ ] All acceptance criteria met
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
- 2026-10-18: Updated for the `tinyshop/infrastructure/` layout
