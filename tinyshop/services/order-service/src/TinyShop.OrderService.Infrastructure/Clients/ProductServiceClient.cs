using Microsoft.Extensions.Logging;
using TinyShop.OrderService.Application.Abstractions.Dependencies;
using TinyShop.OrderService.Application.Models;
using TinyShop.OrderService.Domain.Entities;

namespace TinyShop.OrderService.Infrastructure.Clients;

/// <summary>
/// Typed HttpClient for product-service (contracts.md §5.1). IHttpClientFactory supplies the
/// HttpClient, with its BaseAddress set from configuration (DependencyInjection).
/// </summary>
internal sealed class ProductServiceClient(HttpClient httpClient, ILogger<ProductServiceClient> logger) : IProductCatalog
{
    public const string DependencyName = "product-service";

    public async Task<ProductSummary?> FindAsync(long productId, CancellationToken cancellationToken)
    {
        var product = await DependencyHttp.GetAsync<ProductResponse>(
            httpClient,
            DependencyName,
            $"products/{productId}",
            notFoundCode: "product_not_found",
            // The name and price are snapshotted into the order, so they must fit its rules.
            isValid: body => body.Id == productId
                && !string.IsNullOrWhiteSpace(body.Name)
                && body.Name.Length <= Order.ProductNameMaxLength
                && body.PriceCents > 0,
            logger,
            cancellationToken);

        return product is null ? null : new ProductSummary(product.Id, product.Name!, product.PriceCents);
    }

    /// <summary>The Product resource as product-service sends it (contracts.md §3).</summary>
    private sealed record ProductResponse(long Id, string? Name, long PriceCents);
}
