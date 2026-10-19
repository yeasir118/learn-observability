namespace TinyShop.ProductService.Domain.Entities;

/// <summary>
/// A product that can be ordered. Prices are whole cents (contracts.md §1.2).
/// </summary>
public sealed class Product
{
    public const int NameMaxLength = 100;

    /// <summary>
    /// Used by EF Core to rebuild products that already exist in the database.
    /// </summary>
    private Product()
    {
        Name = string.Empty;
    }

    /// <summary>
    /// Creates a new product. Invalid values are rejected here, so an invalid product can't exist in memory.
    /// </summary>
    public Product(string name, long priceCents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, NameMaxLength, nameof(name));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(priceCents);

        Name = name;
        PriceCents = priceCents;
    }

    /// <summary>Assigned by the database.</summary>
    public long Id { get; private set; }

    public string Name { get; private set; }

    public long PriceCents { get; private set; }
}
