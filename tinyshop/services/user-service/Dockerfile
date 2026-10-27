# user-service images. One Dockerfile, two images (same structure as product-service's):
#   runtime     (default, last stage) the service itself
#   migrations  a one-off container that applies EF Core migrations, then exits
# Build context: tinyshop/ (not this folder), because the build needs the shared files there
# (Directory.Build.props, Directory.Packages.props, global.json, dotnet-tools.json) and the shared
# projects (shared/). From tinyshop/:
#   docker build -f services/user-service/Dockerfile -t tinyshop/user-service:local .
#   docker build -f services/user-service/Dockerfile --target migrations -t tinyshop/user-service-migrations:local .

# ---- Build stage: SDK image, compiles and publishes --------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Shared MSBuild configuration
COPY Directory.Build.props Directory.Packages.props global.json ./

# Project files first: the restore layer below is reused until a .csproj or a package version
# changes, so editing .cs files doesn't download packages again.
COPY services/user-service/src/TinyShop.UserService.Api/TinyShop.UserService.Api.csproj services/user-service/src/TinyShop.UserService.Api/
COPY services/user-service/src/TinyShop.UserService.Application/TinyShop.UserService.Application.csproj services/user-service/src/TinyShop.UserService.Application/
COPY services/user-service/src/TinyShop.UserService.Domain/TinyShop.UserService.Domain.csproj services/user-service/src/TinyShop.UserService.Domain/
COPY services/user-service/src/TinyShop.UserService.Infrastructure/TinyShop.UserService.Infrastructure.csproj services/user-service/src/TinyShop.UserService.Infrastructure/
# Shared plumbing (tinyshop/shared, decision D25), which the Api project references: its project
# file is needed for restore, like the service's own.
COPY shared/TinyShop.ServiceDefaults/TinyShop.ServiceDefaults.csproj shared/TinyShop.ServiceDefaults/

# Restore the Api project, not the solution: tests/ isn't in the build context (.dockerignore),
# and the Api project brings in the other three through its project references.
RUN dotnet restore services/user-service/src/TinyShop.UserService.Api/TinyShop.UserService.Api.csproj

# Now the source code. shared/ first: it changes less often than the service, so its layer is
# more often reused from the cache.
COPY shared/ shared/
COPY services/user-service/src/ services/user-service/src/

# UseAppHost=false: skip the native launcher executable; the image starts the app with "dotnet".
RUN dotnet publish \
    services/user-service/src/TinyShop.UserService.Api/TinyShop.UserService.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    -p:UseAppHost=false

# ---- Migration bundle: built from the same sources as the service ------------------------
FROM build AS migrations-build

# dotnet-ef comes from the repository's tool manifest, so the version matches local use.
COPY dotnet-tools.json ./
RUN dotnet tool restore

# A migration bundle is a standalone executable that applies this project's migrations.
# It's built here, inside Linux, so it targets Linux whatever OS the developer uses.
# Unlike product-service, no placeholder connection string is needed: user-service has a
# design-time DbContext factory (Api/DesignTime), so dotnet ef never starts the app's host and
# never runs its "connection string is required" check.
RUN dotnet ef migrations bundle \
    --project services/user-service/src/TinyShop.UserService.Infrastructure \
    --startup-project services/user-service/src/TinyShop.UserService.Api \
    --configuration Release \
    --output /app/efbundle

# ---- Migrations image: runs the bundle once, then exits -----------------------------------
# Debian-based like the SDK, because the bundle is built for the SDK image's C library (glibc).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrations

WORKDIR /app
COPY --from=migrations-build /app/efbundle .

USER $APP_UID

# The bundle reads ConnectionStrings__UserDb from the environment, like the service does,
# so no password is ever baked into the image or passed on the command line.
ENTRYPOINT ["./efbundle"]

# ---- Runtime stage: ASP.NET Core runtime only, no SDK, no source -------------------------
# Must stay the LAST stage: "docker build" without --target builds the last one.
# Alpine variant: much smaller, and it includes BusyBox wget, which the Compose health check
# uses (the Debian-based aspnet image has neither curl nor wget). Trade-off: Alpine images run
# with invariant globalization (no culture-specific formatting), which TinyShop doesn't need.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

WORKDIR /app
COPY --from=build /app/publish/ .

# The base image already sets this to 8080; stated here so the port is documented next to EXPOSE.
# Single underscores, plural PORTS: ASPNETCORE_HTTPPORTS (a typo in the first draft) is ignored.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Non-root. $APP_UID is the numeric ID (1654) of the "app" user that .NET images provide;
# a number, unlike a name, lets platforms such as Kubernetes verify the image runs as non-root.
USER $APP_UID

ENTRYPOINT ["dotnet", "TinyShop.UserService.Api.dll"]
