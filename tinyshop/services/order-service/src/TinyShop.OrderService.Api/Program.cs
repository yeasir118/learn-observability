using TinyShop.OrderService.Api.Extensions;
using TinyShop.OrderService.Application;
using TinyShop.OrderService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Each layer registers its own services; Program.cs only composes them.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi();

var app = builder.Build();

app.UseApi();

app.Run();

/// <summary>
/// Top-level statements compile into an internal Program class. Making it public lets the
/// integration tests host this app with WebApplicationFactory&lt;Program&gt;, whose test classes
/// and fixtures must be public for xUnit to discover them.
/// </summary>
public partial class Program;
