# order-service images. One Dockerfile, two images (same structure as user-service's):
#   runtime     (default, last stage) the service itself
#   migrations  a one-off container that applies EF Core migrations, then exits
# Build context: tinyshop/ (not this folder), because the build needs the shared files there
# (Directory.Build.props, Directory.Packages.props, global.json, dotnet-tools.json), the shared
# projects (shared/) and the gRPC contract (proto/), from which the payment-service client is
# generated. From tinyshop/:
#   docker build -f services/order-service/Dockerfile -t tinyshop/order-service:local .
#   docker build -f services/order-service/Dockerfile --target migrations -t tinyshop/order-service-migrations:local .

# ---- Build stage: SDK image, compiles and publishes --------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Shared MSBuild configuration
COPY Directory.Build.props Directory.Packages.props global.json ./

# Project files first: the restore layer below is reused until a .csproj or a package version
# changes, so editing .cs files doesn't download packages again.
COPY services/order-service/src/TinyShop.OrderService.Api/TinyShop.OrderService.Api.csproj services/order-service/src/TinyShop.OrderService.Api/
COPY services/order-service/src/TinyShop.OrderService.Application/TinyShop.OrderService.Application.csproj services/order-service/src/TinyShop.OrderService.Application/
COPY services/order-service/src/TinyShop.OrderService.Domain/TinyShop.OrderService.Domain.csproj services/order-service/src/TinyShop.OrderService.Domain/
COPY services/order-service/src/TinyShop.OrderService.Infrastructure/TinyShop.OrderService.Infrastructure.csproj services/order-service/src/TinyShop.OrderService.Infrastructure/
# Shared plumbing (tinyshop/shared, decision D25), which the Api project references: its project
# file is needed for restore, like the service's own.
COPY shared/TinyShop.ServiceDefaults/TinyShop.ServiceDefaults.csproj shared/TinyShop.ServiceDefaults/

# Restore the Api project, not the solution: tests/ isn't in the build context (.dockerignore),
# and the Api project brings in the other three through its project references.
RUN dotnet restore services/order-service/src/TinyShop.OrderService.Api/TinyShop.OrderService.Api.csproj

# The gRPC contract. Grpc.Tools compiles it during the build below (the Infrastructure project
# references ../../../../proto, which resolves to /src/proto here) into the PaymentServiceClient.
# Copied after restore: restore doesn't need it, so a contract change only invalidates the steps
# from here on.
COPY proto/ proto/

# Now the source code. shared/ first: it changes less often than the service, so its layer is
# more often reused from the cache.
COPY shared/ shared/
COPY services/order-service/src/ services/order-service/src/

# UseAppHost=false: skip the native launcher executable; the image starts the app with "dotnet".
RUN dotnet publish \
    services/order-service/src/TinyShop.OrderService.Api/TinyShop.OrderService.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    -p:UseAppHost=false

# ---- Migration bundle: built from the same sources as the service ------------------------
# Starts from the build stage, so proto/ is already there: building the Infrastructure project
# (which holds the migrations) generates the gRPC client too.
FROM build AS migrations-build

# dotnet-ef comes from the repository's tool manifest, so the version matches local use.
COPY dotnet-tools.json ./
RUN dotnet tool restore

# A migration bundle is a standalone executable that applies this project's migrations.
# It's built here, inside Linux, so it targets Linux whatever OS the developer uses.
# No placeholder connection string: like user-service, order-service has a design-time DbContext
# factory (Api/DesignTime), so dotnet ef never starts the app's host and never runs its startup
# checks (the connection string and the three Services:*:BaseUrl addresses).
RUN dotnet ef migrations bundle \
    --project services/order-service/src/TinyShop.OrderService.Infrastructure \
    --startup-project services/order-service/src/TinyShop.OrderService.Api \
    --configuration Release \
    --output /app/efbundle

# ---- Migrations image: runs the bundle once, then exits -----------------------------------
# Debian-based like the SDK, because the bundle is built for the SDK image's C library (glibc).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrations

WORKDIR /app
COPY --from=migrations-build /app/efbundle .

# Non-root, like the service: the job needs no more than a network connection to order-db.
USER $APP_UID

# The bundle reads ConnectionStrings__OrderDb from the environment, like the service does,
# so no password is ever baked into the image or passed on the command line.
ENTRYPOINT ["./efbundle"]

# ---- Runtime stage: ASP.NET Core runtime only, no SDK, no source -------------------------
# Must stay the LAST stage: "docker build" without --target builds the last one.
# Alpine variant: much smaller, and it includes BusyBox wget, which the Compose health check
# uses. Unlike payment-service, this port speaks HTTP/1.1 (REST), so wget can check it.
# The gRPC client needs nothing extra in the image: it's managed code inside the app.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

WORKDIR /app
COPY --from=build /app/publish/ .

# The base image already sets this to 8080; stated here so the port is documented next to EXPOSE.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Non-root. $APP_UID is the numeric ID (1654) of the "app" user that .NET images provide;
# a number, unlike a name, lets platforms such as Kubernetes verify the image runs as non-root.
USER $APP_UID

ENTRYPOINT ["dotnet", "TinyShop.OrderService.Api.dll"]
