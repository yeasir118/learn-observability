# gateway image: the public API (/api/*), forwarding to the internal services (TS-014).
# Same pattern as the other services' Dockerfiles, minus the migration stages: no database.
# Build context: tinyshop/ (not this folder), because the build needs the shared files there
# (Directory.Build.props, Directory.Packages.props, global.json) and the shared projects (shared/).
# From tinyshop/:
#   docker build -f services/gateway/Dockerfile -t tinyshop/gateway:local .

# ---- Build stage: SDK image, compiles and publishes --------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Shared MSBuild configuration
COPY Directory.Build.props Directory.Packages.props global.json ./

# Project files first: the restore layer below is reused until a .csproj or a package version
# changes, so editing .cs files doesn't download packages again.
# One project (TS-014, decision 1): the gateway has no domain, use cases or database.
COPY services/gateway/src/TinyShop.Gateway.Api/TinyShop.Gateway.Api.csproj services/gateway/src/TinyShop.Gateway.Api/
# Shared plumbing (decision D25): the Api project references ServiceDefaults, which references
# Abstractions. Not ServiceDefaults.Data: that's for *.Infrastructure projects only.
COPY shared/TinyShop.ServiceDefaults.Abstractions/TinyShop.ServiceDefaults.Abstractions.csproj shared/TinyShop.ServiceDefaults.Abstractions/
COPY shared/TinyShop.ServiceDefaults/TinyShop.ServiceDefaults.csproj shared/TinyShop.ServiceDefaults/

# Restore the Api project, not the solution: tests/ isn't in the build context (.dockerignore).
RUN dotnet restore services/gateway/src/TinyShop.Gateway.Api/TinyShop.Gateway.Api.csproj

# Now the source code. shared/ first: it changes less often than the gateway, so its layer is
# more often reused from the cache.
COPY shared/ shared/
COPY services/gateway/src/ services/gateway/src/

# UseAppHost=false: skip the native launcher executable; the image starts the app with "dotnet".
RUN dotnet publish \
    services/gateway/src/TinyShop.Gateway.Api/TinyShop.Gateway.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    -p:UseAppHost=false

# ---- Runtime stage: ASP.NET Core runtime only, no SDK, no source -------------------------
# Must stay the LAST stage: "docker build" without --target builds the last one.
# Alpine variant: much smaller, and it includes BusyBox wget, which the Compose health check
# uses (TS-014, decision 9).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

WORKDIR /app
COPY --from=build /app/publish/ .

# The base image already sets this to 8080; stated here so the port is documented next to EXPOSE.
# EXPOSE only documents the port: it publishes nothing (Compose gives the gateway no "ports").
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Non-root. $APP_UID is the numeric ID (1654) of the "app" user that .NET images provide.
USER $APP_UID

ENTRYPOINT ["dotnet", "TinyShop.Gateway.Api.dll"]
