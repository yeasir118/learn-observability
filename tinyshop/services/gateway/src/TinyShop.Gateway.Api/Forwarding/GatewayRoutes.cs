using Microsoft.AspNetCore.Http;

namespace TinyShop.Gateway.Api.Forwarding;

/// <summary>
/// The public API: exactly the routes of contracts.md §4. Nothing else is forwarded; any other path
/// or method gets the gateway's own 404 or 405 from routing.
/// </summary>
internal static class GatewayRoutes
{
    /// <summary>The upstreams' HttpClient names, shared by the routes and the client registrations.</summary>
    public const string ProductService = "product-service";

    /// <inheritdoc cref="ProductService"/>
    public const string UserService = "user-service";

    /// <inheritdoc cref="ProductService"/>
    public const string OrderService = "order-service";

    /// <summary>What the public paths have and the internal ones don't: /api/products → /products.</summary>
    public const string PublicPrefix = "/api";

    /// <summary>The routes. GET /api/orders?userId= is the GET /api/orders route: a query isn't part of a route.</summary>
    public static IReadOnlyList<GatewayRoute> All { get; } =
    [
        new("GET", "/api/products", ProductService),
        new("GET", "/api/users/{id}", UserService),
        new("POST", "/api/orders", OrderService),
        new("GET", "/api/orders/{id}", OrderService),
        new("GET", "/api/orders", OrderService),
    ];

    /// <summary>
    /// The internal path for a public one, relative to the upstream's base address:
    /// /api/users/42 → users/42.
    /// </summary>
    /// <remarks>
    /// ToUriComponent keeps the path as it was sent (still percent-encoded). Path.Value is decoded:
    /// an encoded "%2F" would become "/" and change the path the service sees.
    /// </remarks>
    public static string ToInternalPath(PathString publicPath) =>
        publicPath.ToUriComponent()[(PublicPrefix.Length + 1)..];
}
