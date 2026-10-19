namespace TinyShop.ProductService.Application.Models;

/// <summary>
/// A product as the application exposes it. Callers never receive the Domain entity itself,
/// so the entity can change without changing what callers see.
/// </summary>
public sealed record ProductDto(long Id, string Name, long PriceCents);
