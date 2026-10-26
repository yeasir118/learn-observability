using TinyShop.OrderService.Application.Models;

namespace TinyShop.OrderService.Application.Abstractions.Dependencies;

/// <summary>
/// Looks products up in product-service. Declared here, implemented in Infrastructure (over HTTP).
/// </summary>
/// <remarks>
/// Failures are translated exactly as for <see cref="IUserDirectory"/>.
/// </remarks>
public interface IProductCatalog
{
    /// <summary>Returns the product, or <c>null</c> if product-service says it doesn't exist.</summary>
    Task<ProductSummary?> FindAsync(long productId, CancellationToken cancellationToken);
}
