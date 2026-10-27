using TinyShop.ServiceDefaults.Health;

namespace TinyShop.ProductService.Api.Extensions;

internal static class WebApplicationExtensions
{
    /// <summary>Builds the HTTP request pipeline. Order matters: middleware runs top to bottom.</summary>
    public static WebApplication UseApi(this WebApplication app)
    {
        // Catches exceptions from everything registered after it and turns them into Problem Details.
        app.UseExceptionHandler();

        // Gives empty error responses (404 for an unknown route, 405, ...) a Problem Details body.
        app.UseStatusCodePages();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                foreach (var description in app.DescribeApiVersions())
                {
                    options.SwaggerEndpoint(
                        $"/swagger/{description.GroupName}/swagger.json",
                        description.GroupName.ToUpperInvariant());
                }
            });
        }

        app.MapControllers();
        // /health/live and /health/ready (contracts.md §5.4), the same in every service.
        app.MapDefaultHealthEndpoints();

        return app;
    }
}
