namespace TinyShop.OrderService.Domain.Enums;

/// <summary>
/// The lifecycle of an order (contracts.md §3). Pending is the only state an order can leave,
/// and only once:
/// <code>
///   Pending ──► Paid      payment approved
///      └──────► Failed    payment declined, or payment-service failed or couldn't be reached
/// </code>
/// An order whose payment timed out stays Pending: the payment may or may not have happened
/// (contracts.md §6, §8).
/// </summary>
public enum OrderStatus
{
    Pending,
    Paid,
    Failed,
}
