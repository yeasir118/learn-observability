using TinyShop.Gateway.Api.Extensions;
using TinyShop.Gateway.Api.Forwarding;
using TinyShop.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApi(builder.Configuration);

var app = builder.Build();

// The shared pipeline: error handling, Swagger (Development), health endpoints.
app.UseServiceDefaults();

// The public routes, each forwarded to an internal service (contracts.md §4).
app.MapGet("/api/products", (HttpContext context, RequestForwarder forwarder) =>
    forwarder.ForwardAsync(context, "product-service", "products"));

app.Run();

/// <summary>
/// Top-level statements compile into an internal Program class. Making it public lets the
/// integration tests host this app with WebApplicationFactory&lt;Program&gt;.
/// </summary>
public partial class Program;
