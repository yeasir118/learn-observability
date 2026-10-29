using TinyShop.ServiceDefaults.Errors;

namespace TinyShop.ServiceDefaults;

/// <summary>What a service tells <see cref="ServiceDefaultsExtensions.AddServiceDefaults"/> about itself.</summary>
public sealed class ServiceDefaultsOptions
{
    /// <summary>The OpenAPI document's title, for example "TinyShop Product Service".</summary>
    public required string ApiTitle { get; init; }

    /// <summary>The OpenAPI document's description: what the API is for and where its contract is.</summary>
    public required string ApiDescription { get; init; }

    /// <summary>Adds the error codes only this service returns (see <see cref="ErrorCatalog.Add"/>).</summary>
    public Action<ErrorCatalog> AddErrorCodes { get; init; } = _ => { };
}
