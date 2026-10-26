using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyShop.OrderService.Domain.Entities;
using TinyShop.OrderService.Domain.Enums;

namespace TinyShop.OrderService.Infrastructure.Persistence.Configurations;

/// <summary>
/// How <see cref="Order"/> maps to the database. Table and column names come from the
/// snake_case naming convention (orders, user_id, unit_price_cents), configured in OrderDatabaseOptions.cs.
/// </summary>
internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    /// <summary>Order IDs start here, so they never look like user or product IDs (contracts.md §3: 1001).</summary>
    private const long FirstOrderId = 1001;

    private const int StatusMaxLength = 16;

    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // The database enforces the same rules as the Domain entity, so no other path
        // (a manual SQL update, a future service) can store an inconsistent order.
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_orders_quantity_range",
                $"quantity BETWEEN {Order.MinQuantity} AND {Order.MaxQuantity}");
            table.HasCheckConstraint("ck_orders_unit_price_cents_positive", "unit_price_cents > 0");
            table.HasCheckConstraint("ck_orders_total_cents", "total_cents = unit_price_cents * quantity");
            table.HasCheckConstraint("ck_orders_status", "status IN ('PENDING', 'PAID', 'FAILED')");
            // A transaction ID exactly when PAID, a failure reason exactly when FAILED.
            table.HasCheckConstraint("ck_orders_transaction_id_when_paid",
                "(status = 'PAID') = (transaction_id IS NOT NULL)");
            table.HasCheckConstraint("ck_orders_failure_reason_when_failed",
                "(status = 'FAILED') = (failure_reason IS NOT NULL)");
        });

        builder.HasKey(order => order.Id);

        // GENERATED ALWAYS AS IDENTITY (contracts.md §1.1): no seed data here, so nothing ever
        // needs to insert an explicit ID.
        builder.Property(order => order.Id)
            .UseIdentityAlwaysColumn()
            .HasIdentityOptions(startValue: FirstOrderId);

        builder.Property(order => order.ProductName)
            .IsRequired()
            .HasMaxLength(Order.ProductNameMaxLength);

        // Stored as the same UPPER_SNAKE_CASE string the API returns ("PAID"), so the table reads
        // the same as the JSON, and reordering the enum in code can't change stored meanings.
        builder.Property(order => order.Status)
            .HasConversion(
                status => status.ToString().ToUpperInvariant(),
                value => Enum.Parse<OrderStatus>(value, true))
            .HasMaxLength(StatusMaxLength);

        builder.Property(order => order.FailureReason)
            .HasMaxLength(Order.FailureReasonMaxLength);

        builder.Property(order => order.TransactionId)
            .HasMaxLength(Order.TransactionIdMaxLength);

        // GET /orders?userId= : one user's orders, newest first.
        builder.HasIndex(order => new { order.UserId, order.CreatedAt })
            .IsDescending(false, true);
    }
}
