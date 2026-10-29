using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;

namespace TinyShop.ServiceDefaults.Health;

/// <summary>
/// The health endpoints every TinyShop HTTP service exposes (contracts.md §5.4).
/// </summary>
/// <remarks>
/// The convention, the same as Microsoft's ServiceDefaults template:
/// <list type="bullet">
///   <item><c>/health/live</c> runs only checks tagged <see cref="LiveTag"/>. None are registered,
///   so it only proves the process answers HTTP. A liveness check must never depend on anything
///   outside the process, or a database outage would make the orchestrator restart healthy services.</item>
///   <item><c>/health/ready</c> runs every registered check. A service registers exactly the checks
///   that decide whether it can do its job: its own database, never the services it calls.</item>
/// </list>
/// So the layer that registers a check (Infrastructure) doesn't need to know any tag, and the
/// layers stay independent of this library.
/// </remarks>
public static class HealthEndpointExtensions
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";

    /// <summary>Tag for the rare check that belongs in liveness (it must check only the process itself).</summary>
    public const string LiveTag = "live";

    public static IEndpointRouteBuilder MapDefaultHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(LivePath, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(LiveTag),
        });

        // No predicate: every registered check.
        endpoints.MapHealthChecks(ReadyPath);

        return endpoints;
    }
}
