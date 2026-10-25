using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using TinyShop.UserService.Infrastructure;

namespace TinyShop.UserService.Api.Extensions;

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
        app.MapHealthEndpoints();

        return app;
    }

    /// <summary>
    /// Liveness runs no checks: it only proves the process answers HTTP.
    /// Readiness runs the checks tagged "ready" (the database). See contracts.md §5.4.
    /// </summary>
    private static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(DependencyInjection.ReadyTag),
        });
    }
}
