namespace TinyShop.ProductService.Api.Contracts;

/// <summary>
/// Every collection is wrapped in an object (contracts.md §1), so fields such as pagination
/// metadata can be added later without breaking clients.
/// </summary>
public sealed record CollectionResponse<T>(IReadOnlyList<T> Items);
