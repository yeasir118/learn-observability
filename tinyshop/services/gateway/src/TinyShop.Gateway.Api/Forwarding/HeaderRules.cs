namespace TinyShop.Gateway.Api.Forwarding;

/// <summary>
/// Hop-by-hop headers (RFC 9110 §7.6.1) describe one connection, not the message, so a proxy
/// never copies them to the next hop (TS-014, decision 3).
/// </summary>
internal static class HeaderRules
{
    // Header names are case-insensitive: "Keep-Alive" and "keep-alive" are the same header.
    private static readonly HashSet<string> AlwaysHopByHop = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Keep-Alive",
        "Transfer-Encoding",
        "TE",
        "Trailer",
        "Upgrade",
        "Proxy-Authenticate",
        "Proxy-Authorization",
    };

    /// <summary>
    /// Whether <paramref name="name"/> is hop-by-hop: one of the fixed list, or named by the
    /// message's own Connection header.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <param name="connectionValues">
    /// The message's Connection header values. Each value can be a comma-separated list
    /// ("keep-alive, X-Hop").
    /// </param>
    public static bool IsHopByHop(string name, IEnumerable<string> connectionValues) =>
        AlwaysHopByHop.Contains(name)
        || connectionValues
            .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Contains(name, StringComparer.OrdinalIgnoreCase);
}
