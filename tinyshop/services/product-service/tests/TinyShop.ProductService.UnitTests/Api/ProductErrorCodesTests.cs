using TinyShop.ProductService.Api.Errors;
using TinyShop.ServiceDefaults.Errors;

namespace TinyShop.ProductService.UnitTests.Api;

/// <summary>
/// Only what product-service adds to the shared error catalog. The customizer itself is tested
/// once, in TinyShop.ServiceDefaults.UnitTests.
/// </summary>
public class ProductErrorCodesTests
{
    [Fact]
    public void Register_AddsProductNotFoundWithItsTitle()
    {
        var catalog = new ErrorCatalog();

        ProductErrorCodes.Register(catalog);

        Assert.Equal("Product not found", catalog.TitleFor(ProductErrorCodes.ProductNotFound));
    }
}
