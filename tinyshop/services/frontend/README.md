# TinyShop frontend

The web app: list products, look up a user, place an order, view orders (TS-026). React, TypeScript and Vite; React Router for the pages. It only ever calls the five public routes of [contracts.md §4](../../docs/contracts.md#4-public-api-gateway), through **relative** `/api/...` URLs.

## Run it locally

Requires the Node.js major in [`.nvmrc`](.nvmrc), and the gateway answering on `http://localhost:5100`. That's the setup of [architecture.md §8](../../docs/architecture.md#8-local-development): the services and the gateway with `dotnet run`, the databases through the debug proxies.

```bash
npm ci          # install exactly what package-lock.json says
npm run dev     # http://localhost:5173
```

The dev server answers `/api/...` itself by forwarding it to the gateway (the proxy in [`vite.config.ts`](vite.config.ts)), so the browser only ever talks to one origin and there's no CORS. A gateway somewhere else:

```bash
GATEWAY_URL=http://localhost:5200 npm run dev
```

### Against the stack in Docker

With the whole stack running in Docker (`docker compose up -d`), the gateway publishes no port (TS-014), but the frontend container does: point the dev server at nginx, which forwards `/api/` to the gateway.

```bash
GATEWAY_URL=http://localhost:8000 npm run dev
```

```text
browser ─► Vite :5173 ─/api─► nginx :8000 ─► gateway :8080
```

The app then comes from Vite (with hot reload), and only its API calls go through the container's nginx.

| Page | Calls |
|---|---|
| `/` shop: products, user lookup, order form | `GET /api/products`, `GET /api/users/{id}`, `POST /api/orders` |
| `/orders/{id}` | `GET /api/orders/{id}` |
| `/users/{id}/orders` | `GET /api/orders?userId={id}` |

## Checks

The same as the CI job `frontend`:

```bash
npm run lint        # oxlint
npm run typecheck   # tsc
npm test            # Vitest (npm run test:watch while working)
npm run build       # production build into dist/
npm run preview     # serves dist/ on http://localhost:4173, with the same /api proxy
```

## Design notes

- **No client-side validation** beyond required fields: the service that owns the data validates (contracts.md §2.4), and the app shows its 400 `validation_failed` field by field.
- **Every failure becomes one `ApiError`** ([`src/api/client.ts`](src/api/client.ts)): a Problem Details response keeps its `title`, `code` and `traceId`; any other error response (an empty body, or a proxy's own page) keeps its status; no response at all has status `null`. The trace ID is shown in full: it's what to search the logs for.
- **No retry for `POST /api/orders`**, neither automatic nor a button: if the first request reached the server, a second one creates a second order (contracts.md §8, known gaps). Reads have a "Try again" button.
- **Money is display-only:** cents are divided by 100 for formatting and never used for arithmetic.
- **Payment always approves** until TS-016's fault switches; a declined order (`FAILED`) is covered by a test.

## The container (TS-015)

The [`Dockerfile`](Dockerfile) builds `dist/` in a Node stage and copies it into an unprivileged nginx (uid 101, port 8080); nothing from Node reaches the final image. Its build context is this folder, so [`.dockerignore`](.dockerignore) here applies, not `tinyshop/.dockerignore`. [`nginx/default.conf`](nginx/default.conf) replaces the image's default server block:

| Path | nginx does |
|---|---|
| `/assets/*` | the file, cached for a year (`immutable`: its name changes with its content); a missing file is a 404 |
| `/api/*` | forwards to `gateway:8080` unchanged, resolving the name per request; sets `X-Forwarded-For` and `-Proto`; its own 502/504 as Problem Details |
| anything else | the file if there is one, else `index.html` (client-side routes), with `no-cache` |

In the stack, Compose builds and runs it as the service `frontend` on `http://localhost:8000`. On its own, without a gateway (`/api/` then answers 502):

```bash
docker build -t tinyshop/frontend:local .
docker run --rm -p 8000:8080 tinyshop/frontend:local
```

`docker compose logs frontend` shows one line per request, with the gateway's address, status and time for `/api/` calls (`upstream_addr`, `upstream_status`, `upstream_time`).
