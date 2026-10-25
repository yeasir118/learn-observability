# payment-service image. Unlike product-service and user-service there's no migrations target:
# payment-service has no database.
# Build context: tinyshop/ (not this folder), because the build needs the shared files there
# (Directory.Build.props, Directory.Packages.props, global.json) and the gRPC contract (proto/).
# From tinyshop/:
#   docker build -f services/payment-service/Dockerfile -t tinyshop/payment-service:local .

# ---- Health probe: a small static binary from its official image ------------------------
# The service's port speaks only HTTP/2, so the BusyBox wget health check used by the other
# services (HTTP/1.1) gets a 400 here. grpc_health_probe speaks gRPC and checks the standard
# grpc.health.v1 protocol the service implements (architecture.md §9, Q1).
# A named stage rather than "COPY --from=<image>": Dependabot only updates images in FROM lines.
FROM ghcr.io/grpc-ecosystem/grpc-health-probe:v0.4.59 AS health-probe

# ---- Build stage: SDK image, compiles and publishes --------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Shared MSBuild configuration
COPY Directory.Build.props Directory.Packages.props global.json ./

# Project files first: the restore layer below is reused until a .csproj or a package version
# changes, so editing .cs files doesn't download packages again.
COPY services/payment-service/src/TinyShop.PaymentService.Api/TinyShop.PaymentService.Api.csproj services/payment-service/src/TinyShop.PaymentService.Api/
COPY services/payment-service/src/TinyShop.PaymentService.Application/TinyShop.PaymentService.Application.csproj services/payment-service/src/TinyShop.PaymentService.Application/
COPY services/payment-service/src/TinyShop.PaymentService.Domain/TinyShop.PaymentService.Domain.csproj services/payment-service/src/TinyShop.PaymentService.Domain/

# Restore the Api project, not the solution: tests/ isn't in the build context (.dockerignore),
# and the Api project brings in the other two through its project references.
RUN dotnet restore services/payment-service/src/TinyShop.PaymentService.Api/TinyShop.PaymentService.Api.csproj

# The gRPC contract. Grpc.Tools compiles it during the build below (the Api project references
# ../../../../proto, which resolves to /src/proto here). Copied after restore: restore doesn't
# need it, so a contract change only invalidates the steps from here on.
COPY proto/ proto/

# Now the source code
COPY services/payment-service/src/ services/payment-service/src/

# UseAppHost=false: skip the native launcher executable; the image starts the app with "dotnet".
RUN dotnet publish \
    services/payment-service/src/TinyShop.PaymentService.Api/TinyShop.PaymentService.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    -p:UseAppHost=false

# ---- Runtime stage: ASP.NET Core runtime only, no SDK, no source -------------------------
# Must stay the LAST stage: "docker build" without --target builds the last one.
# Alpine variant: much smaller; the same runtime image as the other services.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

WORKDIR /app
COPY --from=build /app/publish/ .
COPY --from=health-probe /ko-app/grpc-health-probe /usr/local/bin/grpc_health_probe

# The base image already sets this to 8080; stated here so the port is documented next to EXPOSE.
# HTTP/2 only (cleartext "h2c"): set in appsettings.json (Kestrel:EndpointDefaults:Protocols).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Non-root. $APP_UID is the numeric ID (1654) of the "app" user that .NET images provide;
# a number, unlike a name, lets platforms such as Kubernetes verify the image runs as non-root.
USER $APP_UID

ENTRYPOINT ["dotnet", "TinyShop.PaymentService.Api.dll"]
