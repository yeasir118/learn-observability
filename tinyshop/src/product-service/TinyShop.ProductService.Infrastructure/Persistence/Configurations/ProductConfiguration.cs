using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyShop.ProductService.Domain.Entities;

namespace TinyShop.ProductService.Infrastructure.Persistence.Configurations;

/// <summary>
/// How <see cref="Product"/> maps to the database. Table and column names come from the
/// snake_case naming convention (products, id, name, price_cents), configured in DependencyInjection.cs.
/// </summary>
internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        // The database enforces the same rule as the Domain constructor, so no other path
        // (a manual SQL insert, a future service) can store an invalid price.
        builder.ToTable(table => table.HasCheckConstraint(
            "ck_products_price_cents_positive", "price_cents > 0"));

        builder.HasKey(product => product.Id);

        // bigint identity column. BY DEFAULT (not ALWAYS) so that seed rows can be inserted
        // with explicit IDs.
        builder.Property(product => product.Id)
            .UseIdentityByDefaultColumn();

        builder.Property(product => product.Name)
            .IsRequired()
            .HasMaxLength(Product.NameMaxLength);

        builder.Property(product => product.PriceCents)
            .IsRequired();

        // Seed data becomes INSERTs inside the migration. Product's constructor can't set Id and its
        // setters are private, so the rows are anonymous objects using the property names.
        // Seed rows need explicit IDs; the migration then moves the identity sequence past them.
        builder.HasData(
            new { Id = 1L, Name = "Laptop", PriceCents = 100_000L },
            new { Id = 2L, Name = "Keyboard", PriceCents = 10_000L },
            new { Id = 3L, Name = "Mouse", PriceCents = 5_000L },
            new { Id = 4L, Name = "Monitor", PriceCents = 30_000L },
            new { Id = 5L, Name = "Headphones", PriceCents = 15_000L });
    }
}
