using TinyShop.ServiceDefaults.Errors;

namespace TinyShop.Gateway.Api.Errors;

/// <summary>
/// The error codes only the gateway returns (contracts.md §2.3, §4): it couldn't get a response
/// from the internal service at all. Every other error passes through from the service unchanged.
/// </summary>
internal static class GatewayErrorCodes
{
    public const string UpstreamUnreachable = "upstream_unreachable";
    public const string UpstreamTimeout = "upstream_timeout";

    /// <summary>Adds the gateway's codes to its error catalog (passed to AddServiceDefaults).</summary>
    public static void Register(ErrorCatalog catalog) =>
        catalog
            // A 502 or 504 the gateway makes itself can only mean the internal service failed.
            .Add(UpstreamUnreachable, "Upstream unreachable", forStatus: StatusCodes.Status502BadGateway)
            .Add(UpstreamTimeout, "Upstream timeout", forStatus: StatusCodes.Status504GatewayTimeout);
}
