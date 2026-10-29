using TinyShop.ServiceDefaults;
using TinyShop.UserService.Api.Extensions;
using TinyShop.UserService.Application;
using TinyShop.UserService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Each layer registers its own services; Program.cs only composes them.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi();

var app = builder.Build();

// The shared pipeline: error handling, Swagger (Development), controllers, health endpoints.
app.UseServiceDefaults();

app.Run();

/// <summary>
/// Top-level statements compile into an internal Program class. Making it public lets the
/// integration tests host this app with WebApplicationFactory&lt;Program&gt;, whose test classes
/// and fixtures must be public for xUnit to discover them.
/// </summary>
public partial class Program;
