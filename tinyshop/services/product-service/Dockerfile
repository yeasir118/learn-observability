FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# copy shared configuration from the tinyshop root
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY global.json ./

# copy product-service solution and its project files
COPY services/product-service services/product-service
RUN dotnet publish \
    services/product-service/src/TinyShop.ProductService.Api/TinyShop.ProductService.Api.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish/ .
ENV ASPNETCORE__HTTP__PORT=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TinyShop.ProductService.Api.dll"]