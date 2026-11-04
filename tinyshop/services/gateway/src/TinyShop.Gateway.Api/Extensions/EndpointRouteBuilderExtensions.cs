using TinyShop.Gateway.Api.Forwarding;

namespace TinyShop.Gateway.Api.Extensions;

internal static class EndpointRouteBuilderExtensions
{
    /// <summary>Maps every public route to the forwarder, which sends it to its upstream.</summary>
    public static IEndpointRouteBuilder MapGatewayRoutes(this IEndpointRouteBuilder app)
    {
        foreach (var route in GatewayRoutes.All)
        {
            // MapMethods is MapGet/MapPost with the method as a parameter. The internal path comes
            // from the actual request (/api/users/42 → users/42), not from the pattern ({id}).
            app.MapMethods(route.Pattern, [route.Method], (HttpContext context, RequestForwarder forwarder) =>
                forwarder.ForwardAsync(context, route.Upstream, GatewayRoutes.ToInternalPath(context.Request.Path)));
        }

        return app;
    }
}
