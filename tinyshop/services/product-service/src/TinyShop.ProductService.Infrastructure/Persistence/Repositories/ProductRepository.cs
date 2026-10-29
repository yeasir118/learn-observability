using Microsoft.EntityFrameworkCore;
using TinyShop.ProductService.Application.Abstractions.Persistence;
using TinyShop.ProductService.Domain.Entities;
using TinyShop.ServiceDefaults.Data;

namespace TinyShop.ProductService.Infrastructure.Persistence.Repositories;

internal sealed class ProductRepository(ProductDbContext dbContext) : IProductRepository
{
    public Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken) =>
        DatabaseExceptionTranslator.ExecuteAsync<IReadOnlyList<Product>>(async () =>
            await dbContext.Products
                // Read-only query: skip change tracking.
                .AsNoTracking()
                .OrderBy(product => product.Id)
                .ToListAsync(cancellationToken));

    public Task<Product?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        DatabaseExceptionTranslator.ExecuteAsync(() =>
            dbContext.Products
                .AsNoTracking()
                .SingleOrDefaultAsync(product => product.Id == id, cancellationToken));
}
