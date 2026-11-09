# frontend image: the React app's static files, served by nginx (TS-015).
# Build context: this folder (decision 9). Unlike the .NET services, the app needs nothing
# from tinyshop/: no shared projects, no proto files. For the same reason, the .dockerignore
# that applies is the one in this folder: Docker reads it from the context's root only, so
# tinyshop/.dockerignore never sees this build.
# From tinyshop/services/frontend/:
#   docker build -t tinyshop/frontend:local .

# ---- Build stage: Node, installs dependencies and builds dist/ ----------------------------
# Same major as .nvmrc; keep the two in step.
FROM node:26-alpine AS build

WORKDIR /app

# Manifests first: the npm ci layer below is reused until a dependency changes,
# so editing src/ doesn't download packages again.
COPY package.json package-lock.json ./
RUN npm ci

# Decision 2: build only. Lint and tests run in the CI job "frontend", not in the image.
# npm run build is tsc -b && vite build, so a type error still fails the image build.
COPY . .
RUN npm run build

# ---- Runtime stage: nginx serves dist/, nothing from Node survives -----------------------
# Decision 1: the unprivileged variant runs as uid 101 and listens on 8080, not 80.
FROM nginxinc/nginx-unprivileged:1.30-alpine

COPY --from=build /app/dist /usr/share/nginx/html

# Our server block replaces the image's default one (decision 6). Not nginx.conf: that one
# holds the unprivileged image's PID file and temp paths.
COPY nginx/default.conf /etc/nginx/conf.d/default.conf
