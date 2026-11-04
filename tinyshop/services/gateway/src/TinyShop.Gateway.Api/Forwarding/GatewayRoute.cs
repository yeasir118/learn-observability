namespace TinyShop.Gateway.Api.Forwarding;

/// <summary>One public route: the HTTP method, the route pattern, and the upstream that serves it.</summary>
/// <param name="Method">The HTTP method, such as GET.</param>
/// <param name="Pattern">The public route pattern, such as /api/users/{id}.</param>
/// <param name="Upstream">The name of the upstream's HttpClient, such as user-service.</param>
internal sealed record GatewayRoute(string Method, string Pattern, string Upstream);
