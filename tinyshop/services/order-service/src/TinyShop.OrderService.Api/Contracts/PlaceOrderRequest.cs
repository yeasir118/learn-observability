using System.ComponentModel.DataAnnotations;
using TinyShop.OrderService.Domain.Entities;

namespace TinyShop.OrderService.Api.Contracts;

/// <summary>The body of <c>POST /orders</c> (contracts.md §5.3).</summary>
/// <remarks>
/// The properties are nullable so a missing field is reported as missing: a plain <c>long</c>
/// would silently read as 0. [ApiController] answers 400 validation_failed when any rule fails,
/// before the action runs. Unknown fields are ignored (contracts.md §1).
/// </remarks>
public sealed record PlaceOrderRequest
{
    /// <summary>The user placing the order.</summary>
    [Required(ErrorMessage = "Is required.")]
    [Range(1L, long.MaxValue, ErrorMessage = "Must be greater than 0.")]
    public long? UserId { get; init; }

    /// <summary>The product being ordered.</summary>
    [Required(ErrorMessage = "Is required.")]
    [Range(1L, long.MaxValue, ErrorMessage = "Must be greater than 0.")]
    public long? ProductId { get; init; }

    /// <summary>How many, 1–100.</summary>
    [Required(ErrorMessage = "Is required.")]
    [Range(Order.MinQuantity, Order.MaxQuantity, ErrorMessage = "Must be between 1 and 100.")]
    public int? Quantity { get; init; }
}
