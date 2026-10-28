using TinyShop.ServiceDefaults.Errors;

namespace TinyShop.ProductService.Api.Errors;

/// <summary>
/// The error codes only product-service returns (contracts.md §2.3). The codes every service
/// shares (validation_failed, not_found, ...) are in TinyShop.ServiceDefaults' <see cref="ErrorCodes"/>.
/// </summary>
internal static class ProductErrorCodes
{
    public const string ProductNotFound = "product_not_found";

    /// <summary>Adds this service's codes to its error catalog (passed to AddDefaultProblemDetails).</summary>
    public static void Register(ErrorCatalog catalog) =>
        catalog.Add(ProductNotFound, "Product not found");
}
