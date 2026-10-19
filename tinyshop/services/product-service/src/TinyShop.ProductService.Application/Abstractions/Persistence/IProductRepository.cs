using TinyShop.ProductService.Domain.Entities;

namespace TinyShop.ProductService.Application.Abstractions.Persistence;

/// <summary>
/// Read access to stored products. Declared here, implemented in Infrastructure: Application decides
/// what it needs, and doesn't know or care that the implementation uses EF Core and PostgreSQL.
/// </summary>
/// <remarks>
/// Implementations throw <see cref="Common.Exceptions.DataStoreUnavailableException"/> when the
/// data store can't be reached.
/// </remarks>
public interface IProductRepository
{
    /// <summary>Returns all products, ordered by <see cref="Product.Id"/>.</summary>
    Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Returns the product with the given ID, or <c>null</c> if there is none.</summary>
    Task<Product?> GetByIdAsync(long id, CancellationToken cancellationToken);
}
