using TinyShop.Gateway.Api.Extensions;
using TinyShop.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApi(builder.Configuration);

var app = builder.Build();

// The shared pipeline: error handling, Swagger (Development), health endpoints.
app.UseServiceDefaults();

app.MapGatewayRoutes();

app.Run();

/// <summary>
/// Top-level statements compile into an internal Program class. Making it public lets the
/// integration tests host this app with WebApplicationFactory&lt;Program&gt;.
/// </summary>
public partial class Program;
