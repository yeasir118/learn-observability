using TinyShop.OrderService.Application.Abstractions.Persistence;
using TinyShop.OrderService.Application.Mappings;
using TinyShop.OrderService.Application.Models;

namespace TinyShop.OrderService.Application.Services;

/// <summary>Reading orders. Reads only order-db; calls no other service (contracts.md §5.3).</summary>
public interface IOrderQueries
{
    /// <summary>Returns the order, or <c>null</c> if there is none.</summary>
    Task<OrderDto?> GetByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a user's orders, newest first. The user isn't checked with user-service: filtering
    /// by an unknown user simply matches nothing.
    /// </summary>
    Task<IReadOnlyList<OrderDto>> ListByUserAsync(long userId, CancellationToken cancellationToken);
}

internal sealed class OrderQueries(IOrderRepository orders) : IOrderQueries
{
    public async Task<OrderDto?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        (await orders.GetByIdAsync(id, cancellationToken))?.ToDto();

    public async Task<IReadOnlyList<OrderDto>> ListByUserAsync(long userId, CancellationToken cancellationToken) =>
        [.. (await orders.ListByUserAsync(userId, cancellationToken)).Select(order => order.ToDto())];
}
