using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TinyShop.ServiceDefaults.Errors;
using TinyShop.ServiceDefaults.Health;
using TinyShop.ServiceDefaults.Json;
using TinyShop.ServiceDefaults.OpenApi;

namespace TinyShop.ServiceDefaults;

/// <summary>
/// The two entry points every TinyShop HTTP service calls: <see cref="AddServiceDefaults"/> registers
/// the shared plumbing, <see cref="UseServiceDefaults"/> builds the shared request pipeline.
/// </summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>
    /// Registers controllers (with the contract's JSON conventions), Problem Details (with the
    /// service's own error codes and the database-unavailable handler), API versioning, Swagger and
    /// health checks. The service adds only what is its own (order-service: its dependency-failure handler).
    /// </summary>
    public static IServiceCollection AddServiceDefaults(
        this IServiceCollection services,
        ServiceDefaultsOptions options)
    {
        services.AddSingleton(options);

        services.AddDefaultControllers();
        services.AddDefaultProblemDetails(options.AddErrorCodes);
        // Exception handlers run in registration order, so this one comes before any the
        // service registers after calling AddServiceDefaults.
        services.AddExceptionHandler<DataStoreUnavailableExceptionHandler>();
        services.AddDefaultApiVersioning();
        services.AddDefaultSwagger();

        // The health endpoints need the health-check service, even in a service that registers
        // no checks of its own. Calling it again in Infrastructure (to add a check) is harmless.
        services.AddHealthChecks();

        return services;
    }

    /// <summary>Builds the HTTP request pipeline. Order matters: middleware runs top to bottom.</summary>
    public static WebApplication UseServiceDefaults(this WebApplication app)
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
        // /health/live and /health/ready (contracts.md §5.4).
        app.MapDefaultHealthEndpoints();

        return app;
    }

    /// <summary>
    /// Controllers, with the JSON conventions every endpoint follows (contracts.md §1, §2.1),
    /// whether or not a service uses them yet: a service that later accepts a request body or
    /// returns a timestamp gets the contract's behaviour without remembering to add it.
    /// </summary>
    private static void AddDefaultControllers(this IServiceCollection services)
    {
        services
            .AddControllers(options =>
                // Validation errors are keyed by the JSON name ("quantity"), not the C# property
                // name ("Quantity"): the "errors" map uses camelCase (contracts.md §2.1).
                options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
            .AddJsonOptions(options =>
            {
                // Timestamps in UTC with a "Z" and milliseconds (contracts.md §1).
                options.JsonSerializerOptions.Converters.Add(new UtcTimestampJsonConverter());
                // Malformed JSON gets a generic message instead of the parser's, which names .NET
                // types ("could not be converted to System.Nullable`1[System.Int32]").
                options.AllowInputFormatterExceptionMessages = false;
            });
    }

    /// <summary>
    /// The contracts' paths have no version in them (/products), so the version is optional:
    /// clients may send it as a query string (?api-version=1.0) or header (api-version: 1.0),
    /// and requests without one get 1.0.
    /// </summary>
    private static void AddDefaultApiVersioning(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                // AV0016 warns that assuming a default is meant for APIs that predate versioning.
                // Here it's deliberate: the contracts' paths carry no version and the callers
                // (the gateway, other services) don't send one.
#pragma warning disable AV0016
                options.AssumeDefaultVersionWhenUnspecified = true;
#pragma warning restore AV0016
                // Adds an "api-supported-versions" header to responses.
                options.ReportApiVersions = true;
                options.ApiVersionReader = ApiVersionReader.Combine(
                    new QueryStringApiVersionReader("api-version"),
                    new HeaderApiVersionReader("api-version"));
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                // OpenAPI document names: "v1", "v2", ...
                options.GroupNameFormat = "'v'VVV";
            });
    }

    private static void AddDefaultSwagger(this IServiceCollection services)
    {
        services.ConfigureOptions<ConfigureSwaggerOptions>();
        services.AddSwaggerGen();
    }
}
