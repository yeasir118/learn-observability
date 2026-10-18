using TinyShop.ProductService.Application.Models;

namespace TinyShop.ProductService.Application.Services;

/// <summary>Product use cases, as called by the Api layer.</summary>
public interface IProductCatalogService
{
    /// <summary>Returns all products, ordered by ID.</summary>
    Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Returns the product with the given ID, or <c>null</c> if there is none.</summary>
    Task<ProductDto?> GetByIdAsync(long id, CancellationToken cancellationToken);
}
