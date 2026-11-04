namespace TinyShop.Gateway.Api.Forwarding;

/// <summary>
/// Rewrites Location from the internal path to the public one (contracts.md §2.4 rule 3):
/// order-service answers 201 with /orders/1001, and the client must get /api/orders/1001.
/// </summary>
internal static class LocationRewriter
{
    /// <summary>
    /// Adds the public prefix to a path on the same host. Anything else (another host, a path that
    /// is already public, a path relative to the request) is returned as the service sent it.
    /// </summary>
    public static string ToPublic(string location)
    {
        var isRootPath = location.StartsWith('/')
            && !location.StartsWith("//", StringComparison.Ordinal);   // "//host/x" means another host
        var isAlreadyPublic = location.StartsWith(GatewayRoutes.PublicPrefix + "/", StringComparison.Ordinal);

        return isRootPath && !isAlreadyPublic ? GatewayRoutes.PublicPrefix + location : location;
    }
}
