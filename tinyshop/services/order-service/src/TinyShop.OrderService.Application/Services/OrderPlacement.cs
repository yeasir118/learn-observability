using Microsoft.Extensions.Logging;
using TinyShop.OrderService.Application.Abstractions.Dependencies;
using TinyShop.OrderService.Application.Abstractions.Persistence;
using TinyShop.OrderService.Application.Common.Exceptions;
using TinyShop.OrderService.Application.Mappings;
using TinyShop.OrderService.Application.Models;
using TinyShop.OrderService.Domain.Entities;
using TinyShop.ServiceDefaults.Abstractions;

namespace TinyShop.OrderService.Application.Services;

/// <summary>Places orders: the order flow of architecture.md §6.</summary>
/// <remarks>
/// Not named "OrderService": inside the TinyShop.OrderService namespace that name would resolve to
/// the namespace (error CS0118), as with product-service's ProductCatalogService.
/// </remarks>
public interface IOrderPlacement
{
    Task<PlaceOrderResult> PlaceAsync(PlaceOrderCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// The order flow. Each step's failures are decided by contracts.md §6:
/// <code>
/// 1. user-service: does the user exist?        no -> UserNotFound (422); fails -> exception (502/504)
/// 2. product-service: product name and price   no -> ProductNotFound (422); fails -> exception (502/504)
/// 3. order-db: insert the order as PENDING     fails -> exception (503), nothing charged
/// 4. payment-service: charge                   timeout -> stays PENDING (504); fails -> FAILED (502)
/// 5. order-db: PAID or FAILED                  fails -> StatusNotRecorded (503), order stays PENDING
/// </code>
/// </remarks>
internal sealed class OrderPlacement(
    IUserDirectory users,
    IProductCatalog products,
    IPaymentGateway payments,
    IOrderRepository orders,
    TimeProvider timeProvider,
    ILogger<OrderPlacement> logger) : IOrderPlacement
{
    /// <summary>The failure reason stored when payment-service fails (contracts.md §3).</summary>
    public const string PaymentUnavailableReason = "payment_unavailable";

    public async Task<PlaceOrderResult> PlaceAsync(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Placing order: user {UserId}, product {ProductId}, quantity {Quantity}",
            command.UserId, command.ProductId, command.Quantity);

        // 1. The user must exist. DependencyUnavailable/TimeoutException propagate.
        var user = await users.FindAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            logger.LogInformation("Order rejected: user {UserId} not found", command.UserId);
            return new PlaceOrderResult.UserNotFound(command.UserId);
        }

        // 2. The product must exist; its current name and price are snapshotted into the order.
        var product = await products.FindAsync(command.ProductId, cancellationToken);
        if (product is null)
        {
            logger.LogInformation("Order rejected: product {ProductId} not found", command.ProductId);
            return new PlaceOrderResult.ProductNotFound(command.ProductId);
        }

        // 3. Record the order BEFORE charging: the payment needs the order's ID, and if anything
        // goes wrong later there's a record of the attempt. If this insert fails, nothing has
        // been charged (DataStoreUnavailableException propagates: 503).
        var order = Order.Place(
            user.Id, product.Id, product.Name, product.PriceCents, command.Quantity, timeProvider.GetUtcNow());
        orders.Add(order);
        await orders.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Order {OrderId} created as PENDING, total {TotalCents} cents", order.Id, order.TotalCents);

        // 4. Charge.
        PaymentOutcome outcome;
        try
        {
            outcome = await payments.ChargeAsync(
                new ChargeRequest(order.Id, order.UserId, order.TotalCents), cancellationToken);
        }
        catch (PaymentTimeoutException exception)
        {
            // Unknown outcome: the payment may have happened after we stopped waiting.
            // Marking the order FAILED could be a lie, so it stays PENDING (a known gap: nothing
            // reconciles it later, contracts.md §8).
            logger.LogWarning(exception, "Payment for order {OrderId} timed out; order stays PENDING", order.Id);
            return new PlaceOrderResult.PaymentTimedOut(order.ToDto());
        }
        catch (PaymentUnavailableException exception)
        {
            // The charge never happened, so FAILED is the truth.
            logger.LogWarning(exception, "Payment for order {OrderId} unavailable; marking FAILED", order.Id);
            order.MarkFailed(PaymentUnavailableReason, timeProvider.GetUtcNow());
            return await RecordStatusAsync(order, dto => new PlaceOrderResult.PaymentUnavailable(dto), cancellationToken);
        }

        // 5. Record the business outcome.
        if (outcome.Approved)
        {
            order.MarkPaid(outcome.TransactionId!, timeProvider.GetUtcNow());
        }
        else
        {
            order.MarkFailed(outcome.DeclineReason!, timeProvider.GetUtcNow());
        }

        return await RecordStatusAsync(order, dto => new PlaceOrderResult.Placed(dto), cancellationToken);
    }

    /// <summary>
    /// Saves the order's new status. The order row already exists, so a database failure here
    /// isn't left to the generic 503 handler: the caller gets the order's ID with it (contracts.md §2.1).
    /// </summary>
    private async Task<PlaceOrderResult> RecordStatusAsync(
        Order order,
        Func<OrderDto, PlaceOrderResult> result,
        CancellationToken cancellationToken)
    {
        try
        {
            await orders.SaveChangesAsync(cancellationToken);
        }
        catch (DataStoreUnavailableException exception)
        {
            // The row still says PENDING. After an approved payment this means the customer was
            // charged for an order that looks unpaid: a known gap (contracts.md §8).
            logger.LogError(exception, "Order {OrderId} is {Status}, but saving it failed; the stored order stays PENDING",
                order.Id, order.Status);
            return new PlaceOrderResult.StatusNotRecorded(order.Id);
        }

        logger.LogInformation("Order {OrderId} is {Status}", order.Id, order.Status);
        return result(order.ToDto());
    }
}
