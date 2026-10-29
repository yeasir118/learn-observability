using TinyShop.OrderService.Domain.Entities;

namespace TinyShop.OrderService.Application.Abstractions.Persistence;

/// <summary>
/// Stored orders. Declared here, implemented in Infrastructure.
/// </summary>
/// <remarks>
/// Implementations throw <see cref="TinyShop.ServiceDefaults.Abstractions.DataStoreUnavailableException"/> when the
/// data store can't be reached.
/// </remarks>
public interface IOrderRepository
{
    /// <summary>Starts tracking a new order; it's written by the next <see cref="SaveChangesAsync"/>.</summary>
    void Add(Order order);

    /// <summary>
    /// Writes every change to tracked orders (new ones, and status changes made through the
    /// entity) in one transaction. A new order gets its ID here.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Returns the order with the given ID, or <c>null</c> if there is none.</summary>
    Task<Order?> GetByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>Returns a user's orders, newest first.</summary>
    Task<IReadOnlyList<Order>> ListByUserAsync(long userId, CancellationToken cancellationToken);
}
