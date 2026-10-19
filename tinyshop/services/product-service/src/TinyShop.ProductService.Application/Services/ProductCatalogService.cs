using Microsoft.Extensions.Logging;
using TinyShop.ProductService.Application.Abstractions.Persistence;
using TinyShop.ProductService.Application.Mappings;
using TinyShop.ProductService.Application.Models;

namespace TinyShop.ProductService.Application.Services;

internal sealed class ProductCatalogService(IProductRepository repository, ILogger<ProductCatalogService> logger)
    : IProductCatalogService
{
    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching all products");

        var products = await repository.ListAsync(cancellationToken);
        return [.. products.Select(product => product.ToDto())];
    }

    public async Task<ProductDto?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching product {ProductId}", id);

        var product = await repository.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            logger.LogInformation("Product {ProductId} not found", id);
        }

        return product?.ToDto();
    }
}
