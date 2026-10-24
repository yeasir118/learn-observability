# product-service image.
# Build context: tinyshop/ (not this folder), because the build needs the shared files there
# (Directory.Build.props, Directory.Packages.props, global.json). From tinyshop/:
#   docker build -f services/product-service/Dockerfile -t tinyshop-productservice .

# ---- Build stage: SDK image, compiles and publishes --------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Shared MSBuild configuration
COPY Directory.Build.props Directory.Packages.props global.json ./

# Project files first: the restore layer below is reused until a .csproj or a package version
# changes, so editing .cs files doesn't download packages again.
COPY services/product-service/src/TinyShop.ProductService.Api/TinyShop.ProductService.Api.csproj services/product-service/src/TinyShop.ProductService.Api/
COPY services/product-service/src/TinyShop.ProductService.Application/TinyShop.ProductService.Application.csproj services/product-service/src/TinyShop.ProductService.Application/
COPY services/product-service/src/TinyShop.ProductService.Domain/TinyShop.ProductService.Domain.csproj services/product-service/src/TinyShop.ProductService.Domain/
COPY services/product-service/src/TinyShop.ProductService.Infrastructure/TinyShop.ProductService.Infrastructure.csproj services/product-service/src/TinyShop.ProductService.Infrastructure/

# Restore the Api project, not the solution: tests/ isn't in the build context (.dockerignore),
# and the Api project brings in the other three through its project references.
RUN dotnet restore services/product-service/src/TinyShop.ProductService.Api/TinyShop.ProductService.Api.csproj

# Now the source code
COPY services/product-service/src/ services/product-service/src/

# UseAppHost=false: skip the native launcher executable; the image starts the app with "dotnet".
RUN dotnet publish \
    services/product-service/src/TinyShop.ProductService.Api/TinyShop.ProductService.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    -p:UseAppHost=false

# ---- Runtime stage: ASP.NET Core runtime only, no SDK, no source -------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app
COPY --from=build /app/publish/ .

# The base image already sets this to 8080; stated here so the port is documented next to EXPOSE.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Non-root. $APP_UID is the numeric ID (1654) of the "app" user that .NET images provide;
# a number, unlike a name, lets platforms such as Kubernetes verify the image runs as non-root.
USER $APP_UID

ENTRYPOINT ["dotnet", "TinyShop.ProductService.Api.dll"]
