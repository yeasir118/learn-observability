using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TinyShop.ServiceDefaults.OpenApi;

/// <summary>
/// Creates one OpenAPI document per API version. The versions are discovered from the
/// controllers' [ApiVersion] attributes, so adding a v2 controller adds a v2 document automatically.
/// </summary>
internal sealed class ConfigureSwaggerOptions(
    IApiVersionDescriptionProvider provider,
    ServiceDefaultsOptions serviceOptions,
    IWebHostEnvironment environment) : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = serviceOptions.ApiTitle,
                Version = description.ApiVersion.ToString(),
                Description = description.IsDeprecated
                    ? "This API version is deprecated."
                    : serviceOptions.ApiDescription,
            });
        }

        // XML doc comments on the controllers become operation descriptions. The controllers live
        // in the service's Api project, whose assembly name is the application name (not this
        // library's), and its .csproj writes the XML file next to the assembly.
        var xmlFile = $"{environment.ApplicationName}.xml";
        options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
    }
}
