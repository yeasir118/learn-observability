using Microsoft.EntityFrameworkCore;
using TinyShop.OrderService.Application.Abstractions.Persistence;
using TinyShop.OrderService.Domain.Entities;
using TinyShop.ServiceDefaults.Data;

namespace TinyShop.OrderService.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository(OrderDbContext dbContext) : IOrderRepository
{
    public void Add(Order order) => dbContext.Orders.Add(order);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        DatabaseExceptionTranslator.ExecuteAsync(() => dbContext.SaveChangesAsync(cancellationToken));

    public Task<Order?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        DatabaseExceptionTranslator.ExecuteAsync(() =>
            dbContext.Orders
                // Read-only query: skip change tracking.
                .AsNoTracking()
                .SingleOrDefaultAsync(order => order.Id == id, cancellationToken));

    public Task<IReadOnlyList<Order>> ListByUserAsync(long userId, CancellationToken cancellationToken) =>
        DatabaseExceptionTranslator.ExecuteAsync<IReadOnlyList<Order>>(async () =>
            await dbContext.Orders
                .AsNoTracking()
                .Where(order => order.UserId == userId)
                // Newest first; the ID breaks ties between orders created in the same millisecond.
                .OrderByDescending(order => order.CreatedAt)
                .ThenByDescending(order => order.Id)
                .ToListAsync(cancellationToken));
}
