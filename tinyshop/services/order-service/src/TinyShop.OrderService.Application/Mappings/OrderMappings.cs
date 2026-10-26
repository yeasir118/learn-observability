using TinyShop.OrderService.Application.Models;
using TinyShop.OrderService.Domain.Entities;

namespace TinyShop.OrderService.Application.Mappings;

internal static class OrderMappings
{
    public static OrderDto ToDto(this Order order) => new(
        order.Id,
        order.UserId,
        order.ProductId,
        order.ProductName,
        order.UnitPriceCents,
        order.Quantity,
        order.TotalCents,
        // Enum values go over the wire as UPPER_SNAKE_CASE strings (contracts.md §1): "PAID".
        order.Status.ToString().ToUpperInvariant(),
        order.FailureReason,
        order.TransactionId,
        order.CreatedAt,
        order.UpdatedAt);
}
