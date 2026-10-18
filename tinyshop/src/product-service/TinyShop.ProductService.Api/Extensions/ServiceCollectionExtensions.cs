using System.Reflection;
using Asp.Versioning;
using TinyShop.ProductService.Api.Errors;
using TinyShop.ProductService.Api.OpenApi;

namespace TinyShop.ProductService.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    /// <summary>Registers everything the HTTP layer needs.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddControllers();

        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = ProblemDetailsCustomizer.Customize);
        services.AddExceptionHandler<DataStoreUnavailableExceptionHandler>();

        services.AddVersioning();
        services.AddSwagger();

        return services;
    }

    /// <summary>
    /// The contract's paths have no version in them (/products), so the version is optional:
    /// clients may send it as a query string (?api-version=1.0) or header (api-version: 1.0),
    /// and requests without one get 1.0.
    /// </summary>
    private static void AddVersioning(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                // AV0016 warns that assuming a default is meant for APIs that predate versioning.
                // Here it's deliberate: the contract's paths carry no version and callers
                // (gateway, order-service) don't send one.
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

    private static void AddSwagger(this IServiceCollection services)
    {
        services.ConfigureOptions<ConfigureSwaggerOptions>();
        services.AddSwaggerGen(options =>
        {
            // XML doc comments on controllers become operation descriptions.
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
        });
    }
}
