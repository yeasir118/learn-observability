using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TinyShop.ProductService.Api.OpenApi;

/// <summary>
/// Creates one OpenAPI document per API version. The versions are discovered from the
/// controllers' [ApiVersion] attributes, so adding a v2 controller adds a v2 document automatically.
/// </summary>
internal sealed class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "TinyShop Product Service",
                Version = description.ApiVersion.ToString(),
                Description = description.IsDeprecated
                    ? "This API version is deprecated."
                    : "Internal API for the product catalogue. See tinyshop/docs/contracts.md §5.1.",
            });
        }
    }
}
