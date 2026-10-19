using TinyShop.ProductService.Api.Extensions;
using TinyShop.ProductService.Application;
using TinyShop.ProductService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Each layer registers its own services; Program.cs only composes them.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi();

var app = builder.Build();

app.UseApi();

app.Run();
